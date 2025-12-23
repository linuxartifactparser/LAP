using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LAP
{
    /// <summary>
    /// Classe di supporto: singola entry normalizzata di sudo.log.
    /// </summary>
    public class SudoRecord
    {
        public string Date { get; set; } = string.Empty;   // es: "Oct 30"
        public string Time { get; set; } = string.Empty;   // es: "18:15:27"
        public string User1 { get; set; } = string.Empty;  // utente subito dopo il primo ':'

        public string Pwd { get; set; } = string.Empty;    // PWD=...
        public string User { get; set; } = string.Empty;   // USER=...
        public string Command { get; set; } = string.Empty;// COMMAND=...
        public string Env { get; set; } = string.Empty;    // ENV=...
        public string Other { get; set; } = string.Empty;  // altri campi KEY=VALUE

        public bool BecomeSuccess { get; set; } = false;   // true se contiene "BECOME-SUCCESS-"
    }

    /// <summary>
    /// Parser logico per sudo.log (no GUI).
    /// </summary>
    public static class SudoParser
    {
        // Esempio header:
        // Oct 30 18:15:27 : root : no tty ; PWD=/root ; USER=root ; COMMAND=/bin/bash -c env
        private static readonly Regex HeaderRegex = new Regex(
            @"^(?<month>Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)\s+" +
            @"(?<day>\d{1,2})\s+" +
            @"(?<time>\d{2}:\d{2}:\d{2})\s+:\s+" +
            @"(?<user1>[^:]+?)\s+:\s+" +
            @"(?<rest>.+)$",
            RegexOptions.Compiled);

        /// <summary>
        /// Effettua il parsing completo di un file sudo.log (già eventualmente fuso).
        /// Gestisce record su più righe (modalità B).
        /// </summary>
        public static async Task<List<SudoRecord>> ParseFileAsync(
            string logPath,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(logPath))
                throw new FileNotFoundException("sudo.log not found.", logPath);

            var records = new List<SudoRecord>();

            using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs, Encoding.UTF8);

            var builder = new StringBuilder();
            string? line;

            while ((line = await sr.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (IsHeaderLine(line))
                {
                    // chiudi entry precedente
                    if (builder.Length > 0)
                    {
                        var rec = ParseEntry(builder.ToString());
                        if (rec != null)
                            records.Add(rec);

                        builder.Clear();
                    }

                    builder.Append(line.TrimEnd());
                }
                else
                {
                    // continuation line
                    if (builder.Length > 0)
                    {
                        builder.Append(' ');
                        builder.Append(line.Trim());
                    }
                }
            }

            // ultima entry
            if (builder.Length > 0)
            {
                var rec = ParseEntry(builder.ToString());
                if (rec != null)
                    records.Add(rec);
            }

            return records;
        }

        private static bool IsHeaderLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            return HeaderRegex.IsMatch(line);
        }

        /// <summary>
        /// Effettua il parse di un singolo blocco (header + continuation già uniti).
        /// </summary>
        private static SudoRecord? ParseEntry(string entryText)
        {
            if (string.IsNullOrWhiteSpace(entryText))
                return null;

            var m = HeaderRegex.Match(entryText);
            if (!m.Success)
                return null;

            string date = $"{m.Groups["month"].Value} {m.Groups["day"].Value}";
            string time = m.Groups["time"].Value;
            string user1 = m.Groups["user1"].Value.Trim();
            string rest = m.Groups["rest"].Value.Trim();

            string pwd = string.Empty;
            string user = string.Empty;
            string command = string.Empty;
            string env = string.Empty;
            var others = new List<string>();

            var tokens = rest.Split(';');

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i].Trim();
                if (string.IsNullOrWhiteSpace(token))
                    continue;

                // primo token = TTY (no tty / TTY=xyz), che non vogliamo nel CSV
                if (i == 0)
                {
                    if (token.Equals("no tty", StringComparison.OrdinalIgnoreCase) ||
                        token.StartsWith("TTY=", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                int idx = token.IndexOf('=');
                if (idx < 0)
                {
                    // token senza '=', lo spostiamo in OTHER
                    others.Add(token);
                    continue;
                }

                string key = token[..idx].Trim();
                string val = token[(idx + 1)..].Trim();

                switch (key.ToUpperInvariant())
                {
                    case "PWD":
                        pwd = val;
                        break;

                    case "USER":
                        user = val;
                        break;

                    case "COMMAND":
                        // modalità B: qui dentro ci arriva già il comando con continuation line fuse
                        command = val;
                        break;

                    case "ENV":
                        env = string.IsNullOrEmpty(env) ? val : env + " ; " + val;
                        break;

                    default:
                        others.Add(token);
                        break;
                }
            }

            bool becomeSuccess = entryText.Contains("BECOME-SUCCESS-", StringComparison.OrdinalIgnoreCase);

            return new SudoRecord
            {
                Date = date,
                Time = time,
                User1 = user1,
                Pwd = pwd,
                User = user,
                Command = command,
                Env = env,
                Other = others.Count > 0 ? string.Join(" ; ", others) : string.Empty,
                BecomeSuccess = becomeSuccess
            };
        }

        /// <summary>
        /// Esporta il contenuto di un sudo.log in CSV.
        /// </summary>
        public static async Task ExportToCsvAsync(
            string logPath,
            string csvPath,
            string? dateFilter = null,
            CancellationToken cancellationToken = default)
        {
            var records = await ParseFileAsync(logPath, cancellationToken).ConfigureAwait(false);

            using var fs = new FileStream(csvPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var sw = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            // header CSV
            sw.WriteLine(string.Join(",",
                "Date",
                "Time",
                "User1",
                "PWD",
                "USER",
                "COMMAND",
                "ENV",
                "OTHER",
                "BECOME_SUCCESS"));

            foreach (var rec in records)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!string.IsNullOrWhiteSpace(dateFilter) &&
                    !string.Equals(rec.Date, dateFilter, StringComparison.Ordinal))
                {
                    continue;
                }

                string line = string.Join(",",
                    CsvEscape(rec.Date),
                    CsvEscape(rec.Time),
                    CsvEscape(rec.User1),
                    CsvEscape(rec.Pwd),
                    CsvEscape(rec.User),
                    CsvEscape(rec.Command),
                    CsvEscape(rec.Env),
                    CsvEscape(rec.Other),
                    rec.BecomeSuccess ? "X" : "");

                sw.WriteLine(line);
            }

            await sw.FlushAsync().ConfigureAwait(false);
        }

        private static string CsvEscape(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            bool needsQuotes =
                   value.Contains(',')
                || value.Contains('"')
                || value.Contains('\n')
                || value.Contains('\r');

            string v = value.Replace("\"", "\"\"");

            return needsQuotes ? $"\"{v}\"" : v;
        }
    }

    /// <summary>
    /// Parte GUI: metodo di istanza di MainClass che usa SudoParser.
    /// </summary>
    public partial class MainClass : Form
    {
        public async Task ParseSudoLogsAsync(
    string inputFolder,
    string outputFolder,
    RichTextBox richTextBox1,
    ToolStripProgressBar progressBar,
    ToolStripStatusLabel statusLabel)
        {
            void SafeAppend(string text)
            {
                if (richTextBox1.InvokeRequired)
                {
                    richTextBox1.Invoke(new Action(() => richTextBox1.AppendText(text)));
                }
                else
                {
                    richTextBox1.AppendText(text);
                }
            }

            void SafeStatus(string text)
            {
                if (statusLabel.GetCurrentParent().InvokeRequired)
                {
                    statusLabel.GetCurrentParent().Invoke(new Action(() => statusLabel.Text = text));
                }
                else
                {
                    statusLabel.Text = text;
                }
            }

            try
            {
                SafeAppend("Parsing sudo.log files...\n");

                progressBar.Style = ProgressBarStyle.Marquee;
                progressBar.MarqueeAnimationSpeed = 40;
                SafeStatus("Parsing sudo.log ...");

                var candidates = new List<string>();
                string main = Path.Combine(inputFolder, "sudo.log");
                if (File.Exists(main)) candidates.Add(main);

                string rotated = Path.Combine(inputFolder, "sudo.log.1");
                if (File.Exists(rotated)) candidates.Add(rotated);

                if (candidates.Count == 0)
                {
                    SafeAppend("No sudo.log files found.\n");
                    SafeStatus("No sudo logs found");
                    return;
                }

                string tempMerged = Path.GetTempFileName();

                using (var w = new StreamWriter(tempMerged, false, new UTF8Encoding(false)))
                {
                    foreach (var file in candidates)
                    {
                        using var r = new StreamReader(file);
                        while (!r.EndOfStream)
                        {
                            string? line = await r.ReadLineAsync();
                            await w.WriteLineAsync(line ?? "");
                        }
                    }
                }

                // parse logico
                var records = await SudoParser.ParseFileAsync(tempMerged);

                // CSV
                string outCsv = Path.Combine(outputFolder, "sudo_log.csv");
                await SudoParser.ExportToCsvAsync(tempMerged, outCsv);

                // mini-report
                int count = records.Count;
                string oldest = count > 0 ? $"{records[0].Date} {records[0].Time}" : "-";
                string newest = count > 0 ? $"{records[count - 1].Date} {records[count - 1].Time}" : "-";

                SafeAppend($"Completed. {count} entries parsed.\n");
                SafeAppend($"Oldest timestamp: {oldest}\n");
                SafeAppend($"Newest timestamp: {newest}\n");

                SafeStatus("Completed");

                try { File.Delete(tempMerged); } catch { }
            }
            catch (Exception ex)
            {
                SafeAppend("[ERROR] " + ex.Message + "\n");
                SafeStatus("Error");
            }
            finally
            {
                progressBar.Style = ProgressBarStyle.Blocks;
                progressBar.MarqueeAnimationSpeed = 0;
            }
        }

    }
}
