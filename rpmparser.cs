using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.IO.Compression;
using SharpCompress.Compressors.Xz;
using ZstdSharp;
using System.Threading.Tasks;

namespace LAP
{
    public class RpmDependency
    {
        public string Name;
        public string Version;
        public string Flags;
    }

    public static class RpmParser
    {
        // --------------------------------------------
        //  TAGS RPM (metadati principali)
        // --------------------------------------------

        private const int RPMTAG_NAME = 1000;
        private const int RPMTAG_VERSION = 1001;
        private const int RPMTAG_RELEASE = 1002;
        private const int RPMTAG_SUMMARY = 1004;
        private const int RPMTAG_DESCRIPTION = 1005;
        private const int RPMTAG_BUILDTIME = 1006;
        private const int RPMTAG_BUILDHOST = 1007;
        private const int RPMTAG_LICENSE = 1014;
        private const int RPMTAG_PACKAGER = 1015;
        private const int RPMTAG_SOURCE = 1018;
        private const int RPMTAG_URL = 1020;
        private const int RPMTAG_ARCH = 1022;
        private const int RPMTAG_SOURCERPM = 1044;

        // Dependencies
        private const int RPMTAG_REQUIRES = 1049;
        private const int RPMTAG_REQUIRESFLAGS = 1050;
        private const int RPMTAG_REQUIRESVERSION = 1051;

        private const int RPMTAG_PROVIDES = 1047;
        private const int RPMTAG_PROVIDESFLAGS = 1112;
        private const int RPMTAG_PROVIDESVERSION = 1113;

        private const int RPMTAG_CONFLICTS = 1054;
        private const int RPMTAG_CONFLICTSFLAGS = 1055;
        private const int RPMTAG_CONFLICTSVERSION = 1056;

        private const int RPMTAG_OBSOLETES = 1090;
        private const int RPMTAG_OBSOLETESFLAGS = 1114;
        private const int RPMTAG_OBSOLETESVERSION = 1115;

        // Script tags
        private const int RPMTAG_PREIN = 1023;
        private const int RPMTAG_POSTIN = 1024;
        private const int RPMTAG_PREUN = 1025;
        private const int RPMTAG_POSTUN = 1026;

        private const int RPMTAG_PREINPROG = 1085;
        private const int RPMTAG_POSTINPROG = 1086;
        private const int RPMTAG_PREUNPROG = 1087;
        private const int RPMTAG_POSTUNPROG = 1088;

        // File digests
        private const int RPMTAG_FILEDIGESTS = 1035;
        private const int RPMTAG_FILEDIGESTALGO = 5011;

        private const int RPM_LEAD_SIZE = 96;

        private static readonly byte[] RPM_LEAD_MAGIC = { 0xED, 0xAB, 0xEE, 0xDB };

        // ----------------------------------------------------------
        //  PUBLIC API
        // ----------------------------------------------------------

        public static bool IsRpmFile(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            byte[] magic = new byte[4];
            if (fs.Read(magic, 0, 4) != 4)
                return false;

            for (int i = 0; i < 4; i++)
                if (magic[i] != RPM_LEAD_MAGIC[i])
                    return false;

            return true;
        }

        public static (
            string Name,
            string Version,
            string Release,
            string Arch,
            string Summary,
            string Description,
            string License,
            string BuildTime,
            string Packager,
            string URL,
            string BuildHost,
            string Source,
            string SourceRPM,
            List<RpmDependency> Requires,
            List<RpmDependency> Provides,
            List<RpmDependency> Conflicts,
            List<RpmDependency> Obsoletes,
            string PreIn,
            string PostIn,
            string PreUn,
            string PostUn,
            string PreInProg,
            string PostInProg,
            string PreUnProg,
            string PostUnProg,
            string[] FileDigests,
            int FileDigestAlgorithm
        ) ExtractMetadata(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            // 1) LEAD
            br.ReadBytes(RPM_LEAD_SIZE);

            // 2) SIGNATURE HEADER
            SkipHeader(br);

            // 3) MAIN HEADER
            long mainHeaderPos = FindNextHeaderMagic(br);
            if (mainHeaderPos < 0)
                throw new Exception("Main RPM header not found.");

            br.BaseStream.Position = mainHeaderPos;

            return ReadMetadataHeader(br);
        }

        // ----------------------------------------------------------
        //  FILE LIST EXTRACTION
        // ----------------------------------------------------------

        public static List<string> ExtractFileList(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            // Lead
            br.ReadBytes(RPM_LEAD_SIZE);

            // Signature
            SkipHeader(br);

            // Main header
            long mainHeaderPos = FindNextHeaderMagic(br);
            if (mainHeaderPos < 0)
                throw new Exception("Main header not found.");

            br.BaseStream.Position = mainHeaderPos;

            // Move pointer to end-of-header
            _ = ReadMetadataHeader(br);

            long payloadStart = FindPayloadStart(br);
            if (payloadStart < 0)
                throw new Exception("Payload magic not found.");

            br.BaseStream.Position = payloadStart;

            var kind = DetectPayloadKind(br);
            if (kind == PayloadKind.Unknown)
                throw new Exception("Unsupported payload format.");

            byte[] cpioData = DecompressPayload(br, kind);

            using var ms = new MemoryStream(cpioData);
            using var cpioBr = new BinaryReader(ms);

            return ExtractCpioNewc(cpioBr);
        }

        // ----------------------------------------------------------
        //  HEADER PARSING
        // ----------------------------------------------------------

        private static void SkipHeader(BinaryReader br)
        {
            byte[] magic = br.ReadBytes(3);
            byte ver = br.ReadByte();

            if (magic.Length != 3 ||
                magic[0] != 0x8E || magic[1] != 0xAD || magic[2] != 0xE8 ||
                ver != 0x01)
                throw new Exception("Invalid RPM header magic.");

            br.ReadBytes(4);

            int indexCount = ReadInt32BE(br);
            int storeSize = ReadInt32BE(br);

            long indexStart = br.BaseStream.Position;
            long storeStart = indexStart + indexCount * 16;

            br.BaseStream.Seek(storeStart + storeSize, SeekOrigin.Begin);
        }

        private static long FindNextHeaderMagic(BinaryReader br)
        {
            byte[] magic = { 0x8E, 0xAD, 0xE8 };

            while (br.BaseStream.Position < br.BaseStream.Length - 4)
            {
                long pos = br.BaseStream.Position;

                if (br.ReadByte() == magic[0] &&
                    br.ReadByte() == magic[1] &&
                    br.ReadByte() == magic[2] &&
                    br.ReadByte() == 0x01)
                {
                    return pos;
                }

                br.BaseStream.Position = pos + 1;
            }

            return -1;
        }

        // ----------------------------------------------------------
        //  MAIN HEADER: METADATA + DEPENDENCIES + SCRIPTS + DIGESTS
        // ----------------------------------------------------------

        private static (
            string Name,
            string Version,
            string Release,
            string Arch,
            string Summary,
            string Description,
            string License,
            string BuildTime,
            string Packager,
            string URL,
            string BuildHost,
            string Source,
            string SourceRPM,
            List<RpmDependency> Requires,
            List<RpmDependency> Provides,
            List<RpmDependency> Conflicts,
            List<RpmDependency> Obsoletes,
            string PreIn,
            string PostIn,
            string PreUn,
            string PostUn,
            string PreInProg,
            string PostInProg,
            string PreUnProg,
            string PostUnProg,
            string[] FileDigests,
            int FileDigestAlgorithm
        ) ReadMetadataHeader(BinaryReader br)
        {
            // Header magic already read
            br.ReadBytes(3);
            br.ReadByte();
            br.ReadBytes(4);

            int indexCount = ReadInt32BE(br);
            int storeSize = ReadInt32BE(br);

            long indexStart = br.BaseStream.Position;
            long storeStart = indexStart + indexCount * 16;

            // Offsets
            int nameOfs = -1, verOfs = -1, relOfs = -1, archOfs = -1;
            int sumOfs = -1, descOfs = -1, licOfs = -1, packOfs = -1, urlOfs = -1;
            int bhOfs = -1, srcOfs = -1, srcRpmOfs = -1;

            int buildOfs = -1, buildType = -1;

            // Dependencies
            string[] reqNames = null, reqVers = null, provNames = null, provVers = null;
            string[] confNames = null, confVers = null, obsNames = null, obsVers = null;
            int[] reqFlags = null, provFlags = null, confFlags = null, obsFlags = null;

            // Scripts
            string preIn = null, postIn = null, preUn = null, postUn = null;
            string preInProg = null, postInProg = null, preUnProg = null, postUnProg = null;

            // Digests
            string[] digests = null;
            int digestAlgo = 0;

            // Parse INDEX TABLE
            for (int i = 0; i < indexCount; i++)
            {
                int tag = ReadInt32BE(br);
                int type = ReadInt32BE(br);
                int offset = ReadInt32BE(br);
                int count = ReadInt32BE(br);

                switch (tag)
                {
                    case RPMTAG_NAME: nameOfs = offset; break;
                    case RPMTAG_VERSION: verOfs = offset; break;
                    case RPMTAG_RELEASE: relOfs = offset; break;
                    case RPMTAG_ARCH: archOfs = offset; break;
                    case RPMTAG_SUMMARY: sumOfs = offset; break;
                    case RPMTAG_DESCRIPTION: descOfs = offset; break;
                    case RPMTAG_LICENSE: licOfs = offset; break;
                    case RPMTAG_PACKAGER: packOfs = offset; break;
                    case RPMTAG_URL: urlOfs = offset; break;
                    case RPMTAG_BUILDHOST: bhOfs = offset; break;
                    case RPMTAG_SOURCE: srcOfs = offset; break;
                    case RPMTAG_SOURCERPM: srcRpmOfs = offset; break;

                    case RPMTAG_BUILDTIME:
                        buildOfs = offset;
                        buildType = type;
                        break;

                    // Dependencies
                    case RPMTAG_REQUIRES:
                        reqNames = ReadStringArray(br, storeStart, offset, count);
                        break;
                    case RPMTAG_REQUIRESFLAGS:
                        reqFlags = ReadInt32Array(br, storeStart, offset, count);
                        break;
                    case RPMTAG_REQUIRESVERSION:
                        reqVers = ReadStringArray(br, storeStart, offset, count);
                        break;

                    case RPMTAG_PROVIDES:
                        provNames = ReadStringArray(br, storeStart, offset, count);
                        break;
                    case RPMTAG_PROVIDESFLAGS:
                        provFlags = ReadInt32Array(br, storeStart, offset, count);
                        break;
                    case RPMTAG_PROVIDESVERSION:
                        provVers = ReadStringArray(br, storeStart, offset, count);
                        break;

                    case RPMTAG_CONFLICTS:
                        confNames = ReadStringArray(br, storeStart, offset, count);
                        break;
                    case RPMTAG_CONFLICTSFLAGS:
                        confFlags = ReadInt32Array(br, storeStart, offset, count);
                        break;
                    case RPMTAG_CONFLICTSVERSION:
                        confVers = ReadStringArray(br, storeStart, offset, count);
                        break;

                    case RPMTAG_OBSOLETES:
                        obsNames = ReadStringArray(br, storeStart, offset, count);
                        break;
                    case RPMTAG_OBSOLETESFLAGS:
                        obsFlags = ReadInt32Array(br, storeStart, offset, count);
                        break;
                    case RPMTAG_OBSOLETESVERSION:
                        obsVers = ReadStringArray(br, storeStart, offset, count);
                        break;

                    // Scripts
                    case RPMTAG_PREIN:
                        preIn = ReadStringAt(br, storeStart, offset);
                        break;
                    case RPMTAG_POSTIN:
                        postIn = ReadStringAt(br, storeStart, offset);
                        break;
                    case RPMTAG_PREUN:
                        preUn = ReadStringAt(br, storeStart, offset);
                        break;
                    case RPMTAG_POSTUN:
                        postUn = ReadStringAt(br, storeStart, offset);
                        break;

                    case RPMTAG_PREINPROG:
                        preInProg = ReadStringAt(br, storeStart, offset);
                        break;
                    case RPMTAG_POSTINPROG:
                        postInProg = ReadStringAt(br, storeStart, offset);
                        break;
                    case RPMTAG_PREUNPROG:
                        preUnProg = ReadStringAt(br, storeStart, offset);
                        break;
                    case RPMTAG_POSTUNPROG:
                        postUnProg = ReadStringAt(br, storeStart, offset);
                        break;

                    // Digests
                    case RPMTAG_FILEDIGESTS:
                        digests = ReadStringArray(br, storeStart, offset, count);
                        break;

                    case RPMTAG_FILEDIGESTALGO:
                        {
                            long old = br.BaseStream.Position;
                            br.BaseStream.Position = storeStart + offset;
                            digestAlgo = ReadInt32BE(br);
                            br.BaseStream.Position = old;
                        }
                        break;
                }
            }

            // Read string fields
            string name = ReadStringAt(br, storeStart, nameOfs);
            string version = ReadStringAt(br, storeStart, verOfs);
            string release = ReadStringAt(br, storeStart, relOfs);
            string arch = ReadStringAt(br, storeStart, archOfs);
            string summary = ReadStringAt(br, storeStart, sumOfs);
            string desc = ReadStringAt(br, storeStart, descOfs);
            string license = ReadStringAt(br, storeStart, licOfs);
            string packager = ReadStringAt(br, storeStart, packOfs);
            string url = ReadStringAt(br, storeStart, urlOfs);
            string buildHost = ReadStringAt(br, storeStart, bhOfs);
            string source = ReadStringAt(br, storeStart, srcOfs);
            string sourceRpm = ReadStringAt(br, storeStart, srcRpmOfs);

            // Build time
            string buildTime = null;
            if (buildOfs >= 0 && buildType == 4)
            {
                long old = br.BaseStream.Position;
                br.BaseStream.Seek(storeStart + buildOfs, SeekOrigin.Begin);

                int unix = ReadInt32BE(br);
                buildTime = DateTimeOffset.FromUnixTimeSeconds(unix)
                                          .UtcDateTime
                                          .ToString("yyyy-MM-dd HH:mm:ss 'UTC'");

                br.BaseStream.Position = old;
            }

            // Move pointer to end-of-store
            br.BaseStream.Position = storeStart + storeSize;

            // Dep lists
            var reqList = ExtractDependencyBlock(reqNames, reqFlags, reqVers);
            var provList = ExtractDependencyBlock(provNames, provFlags, provVers);
            var confList = ExtractDependencyBlock(confNames, confFlags, confVers);
            var obsList = ExtractDependencyBlock(obsNames, obsFlags, obsVers);

            return (
                name, version, release, arch,
                summary, desc, license,
                buildTime, packager, url,
                buildHost, source, sourceRpm,
                reqList, provList, confList, obsList,
                preIn, postIn, preUn, postUn,
                preInProg, postInProg, preUnProg, postUnProg,
                digests, digestAlgo
            );
        }

        // ----------------------------------------------------------
        //  STRING / INT READERS
        // ----------------------------------------------------------

        private static string ReadStringAt(BinaryReader br, long storeStart, int offset)
        {
            if (offset < 0) return null;

            long old = br.BaseStream.Position;
            br.BaseStream.Position = storeStart + offset;

            using var ms = new MemoryStream();
            int b;
            while ((b = br.ReadByte()) != 0)
                ms.WriteByte((byte)b);

            string s = Encoding.UTF8.GetString(ms.ToArray());
            br.BaseStream.Position = old;
            return s;
        }

        // ----------------------------------------------------------
        private static int ReadInt32BE(BinaryReader br)
        {
            byte[] b = br.ReadBytes(4);
            return (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
        }

        // ----------------------------------------------------------

        private static string[] ReadStringArray(BinaryReader br, long storeStart, int offset, int count)
        {
            if (offset < 0) return null;

            long old = br.BaseStream.Position;
            br.BaseStream.Position = storeStart + offset;

            var list = new List<string>();

            for (int i = 0; i < count; i++)
            {
                using var ms = new MemoryStream();
                byte b;
                while ((b = br.ReadByte()) != 0)
                    ms.WriteByte(b);

                list.Add(Encoding.UTF8.GetString(ms.ToArray()));
            }

            br.BaseStream.Position = old;
            return list.ToArray();
        }

        // ----------------------------------------------------------
        private static int[] ReadInt32Array(BinaryReader br, long storeStart, int offset, int count)
        {
            if (offset < 0) return null;

            long old = br.BaseStream.Position;
            br.BaseStream.Position = storeStart + offset;

            int[] arr = new int[count];
            for (int i = 0; i < count; i++)
                arr[i] = ReadInt32BE(br);

            br.BaseStream.Position = old;
            return arr;
        }

        // ----------------------------------------------------------
        //  DEPENDENCY BUILDING
        // ----------------------------------------------------------

        private static string DependencyFlagsToString(int flags)
        {
            bool lt = (flags & 0x02) != 0;
            bool eq = (flags & 0x04) != 0;
            bool gt = (flags & 0x08) != 0;

            if (lt && eq) return "<=";
            if (gt && eq) return ">=";
            if (lt) return "<";
            if (gt) return ">";
            if (eq) return "=";
            return "";
        }

        // ----------------------------------------------------------
        private static List<RpmDependency> ExtractDependencyBlock(string[] names, int[] flags, string[] versions)
        {
            var list = new List<RpmDependency>();
            if (names == null) return list;

            for (int i = 0; i < names.Length; i++)
            {
                list.Add(new RpmDependency
                {
                    Name = names[i],
                    Version = (versions != null && i < versions.Length) ? versions[i] : "",
                    Flags = DependencyFlagsToString(
                        (flags != null && i < flags.Length) ? flags[i] : 0
                    )
                });
            }

            return list;
        }

        // ----------------------------------------------------------
        //  PAYLOAD DETECTION
        // ----------------------------------------------------------

        private enum PayloadKind
        {
            Unknown,
            CpioRaw,
            Gzip,
            Xz,
            Zstd
        }

        private static readonly byte[] ZstdMagic = { 0x28, 0xB5, 0x2F, 0xFD };
        private static readonly byte[] XzMagic = { 0xFD, 0x37, 0x7A, 0x58, 0x5A };
        private static readonly byte[] GzMagic = { 0x1F, 0x8B };
        private static readonly byte[] CpioMagic = { 0x30, 0x37, 0x30, 0x37, 0x30, 0x31 }; // "070701"

        private static long FindPayloadStart(BinaryReader br)
        {
            long start = br.BaseStream.Position;

            if (DetectPayloadKind(br) != PayloadKind.Unknown)
                return start;

            br.BaseStream.Position = start;

            var q = new Queue<byte>();
            long pos = start;

            while (pos < br.BaseStream.Length)
            {
                int b = br.BaseStream.ReadByte();
                if (b < 0) break;

                q.Enqueue((byte)b);
                if (q.Count > 6)
                    q.Dequeue();

                if (EndsWith(q, ZstdMagic)) return pos - 3;
                if (EndsWith(q, XzMagic)) return pos - 4;
                if (EndsWith(q, GzMagic)) return pos - 1;
                if (EndsWith(q, CpioMagic)) return pos - 5;

                pos++;
            }

            return -1;
        }
        // ----------------------------------------------------------
        private static bool EndsWith(Queue<byte> q, byte[] pattern)
        {
            if (q.Count < pattern.Length) return false;
            var arr = q.ToArray();
            int offset = arr.Length - pattern.Length;

            for (int i = 0; i < pattern.Length; i++)
                if (arr[offset + i] != pattern[i])
                    return false;

            return true;
        }
        // ----------------------------------------------------------
        private static PayloadKind DetectPayloadKind(BinaryReader br)
        {
            long pos = br.BaseStream.Position;
            byte[] probe = br.ReadBytes(8);
            br.BaseStream.Position = pos;

            if (probe.Length >= 6 &&
                probe[0] == CpioMagic[0] && probe[1] == CpioMagic[1] &&
                probe[2] == CpioMagic[2] && probe[3] == CpioMagic[3] &&
                probe[4] == CpioMagic[4] && probe[5] == CpioMagic[5])
                return PayloadKind.CpioRaw;

            if (probe.Length >= 4 &&
                probe[0] == ZstdMagic[0] && probe[1] == ZstdMagic[1] &&
                probe[2] == ZstdMagic[2] && probe[3] == ZstdMagic[3])
                return PayloadKind.Zstd;

            if (probe.Length >= 5 &&
                probe[0] == XzMagic[0] && probe[1] == XzMagic[1] &&
                probe[2] == XzMagic[2] && probe[3] == XzMagic[3] &&
                probe[4] == XzMagic[4])
                return PayloadKind.Xz;

            if (probe.Length >= 2 &&
                probe[0] == GzMagic[0] && probe[1] == GzMagic[1])
                return PayloadKind.Gzip;

            return PayloadKind.Unknown;
        }
        // ----------------------------------------------------------
        private static byte[] DecompressPayload(BinaryReader br, PayloadKind kind)
        {
            switch (kind)
            {
                case PayloadKind.CpioRaw:
                    using (var ms = new MemoryStream())
                    {
                        br.BaseStream.CopyTo(ms);
                        return ms.ToArray();
                    }

                case PayloadKind.Gzip:
                    using (var gz = new GZipStream(br.BaseStream, CompressionMode.Decompress))
                    using (var ms = new MemoryStream())
                    {
                        gz.CopyTo(ms);
                        return ms.ToArray();
                    }

                case PayloadKind.Xz:
                    using (var xz = new XZStream(br.BaseStream))
                    using (var ms = new MemoryStream())
                    {
                        xz.CopyTo(ms);
                        return ms.ToArray();
                    }

                case PayloadKind.Zstd:
                    using (var zs = new DecompressionStream(br.BaseStream))
                    using (var ms = new MemoryStream())
                    {
                        zs.CopyTo(ms);
                        return ms.ToArray();
                    }

                default:
                    throw new Exception("Unsupported payload type.");
            }
        }

        // ----------------------------------------------------------
        //  CPIO newc PARSER
        // ----------------------------------------------------------

        private static List<string> ExtractCpioNewc(BinaryReader br)
        {
            var files = new List<string>();

            while (true)
            {
                byte[] header = br.ReadBytes(110);
                if (header.Length < 110)
                    break;

                if (header[0] != (byte)'0' || header[1] != (byte)'7' ||
                    header[2] != (byte)'0' || header[3] != (byte)'7' ||
                    header[4] != (byte)'0' || header[5] != (byte)'1')
                    break;

                string nameSizeHex = Encoding.ASCII.GetString(header, 94, 8);
                int nameSize = Convert.ToInt32(nameSizeHex, 16);

                string fileSizeHex = Encoding.ASCII.GetString(header, 54, 8);
                int fileSize = Convert.ToInt32(fileSizeHex, 16);

                byte[] nameBytes = br.ReadBytes(nameSize);
                string filename = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');

                if (filename == "TRAILER!!!")
                    break;

                files.Add(filename);

                int consumed = 110 + nameSize;
                int pad1 = (4 - (consumed % 4)) % 4;
                if (pad1 > 0)
                    br.ReadBytes(pad1);

                if (fileSize > 0)
                    br.ReadBytes(fileSize);

                int pad2 = (4 - (fileSize % 4)) % 4;
                if (pad2 > 0)
                    br.ReadBytes(pad2);
            }

            return files;
        }


        // =====================================================================
        //  ASYNC WRAPPERS (aggiunti – non modificano il parser)
        // =====================================================================

        public static Task<bool> IsRpmFileAsync(string path)
        {
            return Task.Run(() => IsRpmFile(path));
        }

        public static Task<(
            string Name,
            string Version,
            string Release,
            string Arch,
            string Summary,
            string Description,
            string License,
            string BuildTime,
            string Packager,
            string URL,
            string BuildHost,
            string Source,
            string SourceRPM,
            List<RpmDependency> Requires,
            List<RpmDependency> Provides,
            List<RpmDependency> Conflicts,
            List<RpmDependency> Obsoletes,
            string PreIn,
            string PostIn,
            string PreUn,
            string PostUn,
            string PreInProg,
            string PostInProg,
            string PreUnProg,
            string PostUnProg,
            string[] FileDigests,
            int FileDigestAlgorithm
        )> ExtractMetadataAsync(string path)
        {
            return Task.Run(() => ExtractMetadata(path));
        }

        public static Task<List<string>> ExtractFileListAsync(string path)
        {
            return Task.Run(() => ExtractFileList(path));
        }

        public static Task<(
            // metadata
            string Name,
            string Version,
            string Release,
            string Arch,
            string Summary,
            string Description,
            string License,
            string BuildTime,
            string Packager,
            string URL,
            string BuildHost,
            string Source,
            string SourceRPM,
            List<RpmDependency> Requires,
            List<RpmDependency> Provides,
            List<RpmDependency> Conflicts,
            List<RpmDependency> Obsoletes,
            string PreIn,
            string PostIn,
            string PreUn,
            string PostUn,
            string PreInProg,
            string PostInProg,
            string PreUnProg,
            string PostUnProg,
            string[] FileDigests,
            int FileDigestAlgorithm,
            // extra
            List<string> FileList
        )> GetAllAsync(string path)
        {
            return Task.Run(() =>
            {
                var meta = ExtractMetadata(path);
                var files = ExtractFileList(path);

                return (
                    meta.Name,
                    meta.Version,
                    meta.Release,
                    meta.Arch,
                    meta.Summary,
                    meta.Description,
                    meta.License,
                    meta.BuildTime,
                    meta.Packager,
                    meta.URL,
                    meta.BuildHost,
                    meta.Source,
                    meta.SourceRPM,
                    meta.Requires,
                    meta.Provides,
                    meta.Conflicts,
                    meta.Obsoletes,
                    meta.PreIn,
                    meta.PostIn,
                    meta.PreUn,
                    meta.PostUn,
                    meta.PreInProg,
                    meta.PostInProg,
                    meta.PreUnProg,
                    meta.PostUnProg,
                    meta.FileDigests,
                    meta.FileDigestAlgorithm,
                    files
                );
            });
        }
    }
}
