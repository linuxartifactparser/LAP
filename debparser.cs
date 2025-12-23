using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;
using SharpCompress.Archives;
using SharpCompress.Archives.Tar;
using SharpCompress.Compressors.Xz;
using SharpCompress.Compressors.BZip2;

namespace LAP
{
    public static class DebParser
    {
        // ===================================================================
        // DTO per i file di CONTROL
        // ===================================================================
        public class ControlFile
        {
            public string FileName { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
        }

        // ===================================================================
        // Check veloce: header AR "!<arch>\n"
        // ===================================================================
        public static bool IsDebFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            try
            {
                using var fs = File.OpenRead(path);
                if (fs.Length < 8)
                    return false;

                byte[] hdr = new byte[8];
                fs.Read(hdr, 0, 8);
                return Encoding.ASCII.GetString(hdr) == "!<arch>\n";
            }
            catch
            {
                return false;
            }
        }

        // ===================================================================
        // API asincrona principale
        // ===================================================================
        public static Task<(string debianBinaryContent,
                            List<ControlFile> controlFiles,
                            List<string> dataTree)> GetAllAsync(string path)
        {
            return Task.Run(() => ParseDebInternal(path));
        }

        // ===================================================================
        // Parser interno
        // ===================================================================
        private static (string debianBinaryContent,
                        List<ControlFile> controlFiles,
                        List<string> dataTree) ParseDebInternal(string path)
        {
            string debianBinary = string.Empty;
            byte[]? controlBytes = null;
            string? controlName = null;
            byte[]? dataBytes = null;
            string? dataName = null;

            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: true);

            // Header AR globale
            byte[] global = br.ReadBytes(8);
            if (Encoding.ASCII.GetString(global) != "!<arch>\n")
                throw new InvalidDataException("Not a valid .deb (ar) file.");

            // Loop sui membri AR
            while (fs.Position < fs.Length)
            {
                byte[] hdr = br.ReadBytes(60);
                if (hdr.Length == 0)
                    break;
                if (hdr.Length < 60)
                    throw new InvalidDataException("Truncated ar header.");

                string fileId = Encoding.ASCII.GetString(hdr, 0, 16).Trim();
                string sizeField = Encoding.ASCII.GetString(hdr, 48, 10).Trim();

                long size = 0;
                long.TryParse(sizeField, out size);

                string name = fileId;
                if (name.EndsWith("/"))
                    name = name[..^1];

                byte[] data = br.ReadBytes((int)size);
                if (data.Length < size)
                    throw new InvalidDataException("Truncated ar member data.");

                // Padding a 1 byte se size dispari
                if (size % 2 != 0 && fs.Position < fs.Length)
                    br.ReadByte();

                if (name == "debian-binary")
                {
                    debianBinary = Encoding.ASCII.GetString(data);
                }
                else if (name.StartsWith("control.tar", StringComparison.OrdinalIgnoreCase))
                {
                    controlBytes = data;
                    controlName = name;
                }
                else if (name.StartsWith("data.tar", StringComparison.OrdinalIgnoreCase))
                {
                    dataBytes = data;
                    dataName = name;
                }
            }

            // ===================================================================
            // CONTROL (usiamo TarArchive di SharpCompress)
            // ===================================================================
            var controlFiles = new List<ControlFile>();
            if (controlBytes != null && controlName != null)
            {
                using var tarStream = DecompressToSeekable(controlBytes, controlName);
                using var archive = TarArchive.Open(tarStream);

                foreach (var entry in archive.Entries)
                {
                    if (entry.IsDirectory)
                        continue;

                    // Alcuni tar usano header speciali (pax/gnu longname) → skip se chiave vuota
                    if (string.IsNullOrWhiteSpace(entry.Key))
                        continue;

                    using var es = entry.OpenEntryStream();
                    using var ms = new MemoryStream();
                    es.CopyTo(ms);
                    var bytes = ms.ToArray();

                    string text;
                    try { text = Encoding.UTF8.GetString(bytes); }
                    catch { text = Encoding.ASCII.GetString(bytes); }

                    controlFiles.Add(new ControlFile
                    {
                        FileName = entry.Key,
                        Content = text
                    });
                }
            }

            // ===================================================================
            // DATA (solo nomi, sempre via TarArchive)
            // ===================================================================
            var dataTree = new List<string>();
            if (dataBytes != null && dataName != null)
            {
                using var tarStream = DecompressToSeekable(dataBytes, dataName);
                using var archive = TarArchive.Open(tarStream);

                var entries = new List<(string path, bool isDir)>();

                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key))
                        continue;

                    entries.Add((entry.Key, entry.IsDirectory));
                }

                dataTree = BuildTreeView(entries);
            }

            return (debianBinary, controlFiles, dataTree);
        }

        // ===================================================================
        // Rilevazione compressione
        // ===================================================================
        private enum DebCompression
        {
            None,
            GZip,
            XZ,
            BZip2
        }

        private static DebCompression DetectDebCompression(byte[] buf)
        {
            // GZIP
            if (buf.Length >= 2 &&
                buf[0] == 0x1F && buf[1] == 0x8B)
                return DebCompression.GZip;

            // XZ
            if (buf.Length >= 6 &&
                buf[0] == 0xFD && buf[1] == 0x37 &&
                buf[2] == 0x7A && buf[3] == 0x58 &&
                buf[4] == 0x5A && buf[5] == 0x00)
                return DebCompression.XZ;

            // BZIP2 ("BZh")
            if (buf.Length >= 3 &&
                buf[0] == 0x42 && buf[1] == 0x5A && buf[2] == 0x68)
                return DebCompression.BZip2;

            return DebCompression.None;
        }

        // ===================================================================
        // Decompressione → SEMPRE MemoryStream seekable
        // ===================================================================
        private static MemoryStream DecompressToSeekable(byte[] buffer, string fileName)
        {
            // TAR non compresso
            if (fileName.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
                return new MemoryStream(buffer);

            var kind = DetectDebCompression(buffer);

            Stream baseStream = new MemoryStream(buffer);
            Stream decompressor;

            switch (kind)
            {
                case DebCompression.GZip:
                    decompressor = new GZipStream(
                        baseStream,
                        CompressionMode.Decompress,
                        leaveOpen: false);
                    break;

                case DebCompression.XZ:
                    decompressor = new XZStream(baseStream);
                    break;

                case DebCompression.BZip2:
                    decompressor = new BZip2Stream(
                        baseStream,
                        SharpCompress.Compressors.CompressionMode.Decompress,
                        decompressConcatenated: false);
                    break;

                case DebCompression.None:
                default:
                    // Proviamo a trattarlo comunque come TAR raw
                    return new MemoryStream(buffer);
            }

            var ms = new MemoryStream();
            using (decompressor)
            {
                decompressor.CopyTo(ms);
            }

            ms.Position = 0;
            return ms;
        }

        // ===================================================================
        // TREE VIEW (DATA)
        // ===================================================================
        private static List<string> BuildTreeView(List<(string path, bool isDir)> entries)
        {
            var lines = new List<string>();
            var normalized = new List<(string path, bool isDir)>();

            foreach (var e in entries)
            {
                var p = e.path.Replace('\\', '/').Trim('/');
                if (!string.IsNullOrEmpty(p))
                    normalized.Add((p, e.isDir));
            }

            normalized.Sort((a, b) =>
                string.Compare(a.path, b.path, StringComparison.OrdinalIgnoreCase));

            var printed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var e in normalized)
            {
                var parts = e.path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;

                string current = "";

                for (int depth = 0; depth < parts.Length; depth++)
                {
                    string part = parts[depth];
                    current = depth == 0 ? part : $"{current}/{part}";

                    if (!printed.Add(current))
                        continue;

                    bool isLast = depth == parts.Length - 1;
                    bool isDir = isLast ? e.isDir : true;

                    string indent = new string(' ', depth * 2);
                    string prefix = depth == 0 ? "" : "├── ";

                    lines.Add($"{indent}{prefix}{part}{(isDir ? "/" : "")}");
                }
            }

            return lines;
        }
    }
}
