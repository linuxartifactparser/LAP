using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LAP
{
    public partial class MainClass : Form
    {
        // =====================================================================
        // UNIVERSAL SYSLOG PARSER — per cron, auth, secure, messages, sudo, ecc.
        // =====================================================================
        public async Task ParseSyslogAsync(
            string inputFolder,
            string outputFolder,
            RichTextBox logBox,
            ToolStripProgressBar progressBar,
            ToolStripStatusLabel statusLabel,
            string logType,
            bool includeGz)
        {
            try
            {
                if (!Directory.Exists(outputFolder))
                {
                    MessageBox.Show("Output folder does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Trova tutti i file che iniziano con logType
                var files = new DirectoryInfo(inputFolder)
                    .GetFiles("*", SearchOption.TopDirectoryOnly)
                    .Where(f => f.Name.StartsWith(logType, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (files.Count == 0)
                {
                    MessageBox.Show($"No {logType} log files found in the selected folder.",
                        "No Logs Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                await LogSysAsync(logBox, logType, $"Found {files.Count} files to process.");
                UpdateStatusLabelSysSafe(statusLabel, $"Parsing {logType} logs...");
                UpdateProgress(progressBar, 0);

                // =============================
                // PREPARAZIONE CSV MULTIPLI
                // =============================
                const int MAX_ROWS = 1_000_000;
                int currentPart = 1;
                int rowsInCurrentFile = 0;

                StreamWriter writer = null;
                string currentCsvFile = "";

                void OpenNextCsv()
                {
                    writer?.Dispose();

                    string partName = $"{logType}_part_{currentPart:000}.csv";
                    currentCsvFile = Path.Combine(outputFolder, partName);

                    writer = new StreamWriter(currentCsvFile, false, Encoding.Unicode);
                    writer.WriteLine("Date\tTime\tHostname\tProcess/Service\tPID\tMessage");

                    rowsInCurrentFile = 0;
                    currentPart++;
                }

                OpenNextCsv();

                int processedFiles = 0;
                int totalLines = 0;

                // =============================
                // PARSING FILE PER FILE
                // =============================
                foreach (var file in files)
                {
                    processedFiles++;
                    await LogSysAsync(logBox, logType, $"Parsing {file.Name}...");

                    bool isGz = file.Extension.Equals(".gz", StringComparison.OrdinalIgnoreCase);

                    if (isGz && !includeGz)
                    {
                        await LogSysAsync(logBox, logType, $"Skipping .gz file (checkbox disabled): {file.Name}");
                        continue;
                    }

                    using Stream fileStream = file.OpenRead();
                    Stream readStream = fileStream;

                    if (isGz)
                        readStream = new GZipStream(fileStream, CompressionMode.Decompress);

                    using (var reader = new StreamReader(readStream, Encoding.UTF8))
                    {
                        string line;
                        while ((line = await reader.ReadLineAsync()) != null)
                        {
                            totalLines++;

                            var entry = ParseSyslogLineUniversal(line);

                            if (entry == null)
                                entry = MakeMalformedEntry(line);

                            // SPLITTING
                            if (rowsInCurrentFile >= MAX_ROWS)
                                OpenNextCsv();

                            string row =
                                $"{entry.Date}\t{entry.Time}\t{entry.Hostname}\t{entry.Process}\t{entry.Pid}\t{entry.Message}";

                            writer.WriteLine(row);
                            rowsInCurrentFile++;
                        }
                    }

                    int percent = (int)Math.Round(processedFiles * 100.0 / files.Count);
                    UpdateProgress(progressBar, percent);
                }

                writer?.Dispose();

                await LogSysAsync(logBox, logType,
                    $"Completed.\n" +
                    $"Processed files: {processedFiles}\n" +
                    $"Total lines: {totalLines}\n" +
                    $"CSV parts generated: {currentPart - 1}\n");

                UpdateStatusLabelSysSafe(statusLabel, "Completed.");
                UpdateProgress(progressBar, 0);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Exception:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatusLabelSysSafe(statusLabel, "Error.");
                UpdateProgress(progressBar, 0);
            }
        }

        // =====================================================================
        // UNIVERSAL SYSLOG REGEX PARSER
        // =====================================================================
        private SyslogEntry? ParseSyslogLineUniversal(string line)
        {
            // Regex più robuste
            var withPid = new Regex(@"^([A-Z][a-z]{2})\s+(\d{1,2})\s+(\d\d:\d\d:\d\d)\s+(\S+)\s+([^\[\]:]+)\[(\d+)\]:\s+(.*)$");
            var noPid = new Regex(@"^([A-Z][a-z]{2})\s+(\d{1,2})\s+(\d\d:\d\d:\d\d)\s+(\S+)\s+([^\[\]:]+):\s+(.*)$");

            Match m;

            if (withPid.IsMatch(line))
            {
                m = withPid.Match(line);
                return MakeEntryFromMatch(m, hasPid: true);
            }
            else if (noPid.IsMatch(line))
            {
                m = noPid.Match(line);
                return MakeEntryFromMatch(m, hasPid: false);
            }

            return null;
        }

        // ========================
        // CREAZIONE ENTRIES
        // ========================
        private SyslogEntry MakeEntryFromMatch(Match m, bool hasPid)
        {
            string month = m.Groups[1].Value;
            string day = m.Groups[2].Value.PadLeft(2, '0');
            string time = m.Groups[3].Value;
            string hostname = m.Groups[4].Value;
            string process = m.Groups[5].Value.Trim();
            string pid = hasPid ? m.Groups[6].Value : "-";
            string message = hasPid ? m.Groups[7].Value.Trim() : m.Groups[6].Value.Trim();

            // =========================================
            // CORREZIONE: rimozione PID duplicato nel messaggio
            // =========================================
            if (hasPid && message.StartsWith($"[{pid}]"))
            {
                // Rimuove "[PID]" e l'eventuale spazio successivo
                message = message.Substring(pid.Length + 2).TrimStart();
            }

            return new SyslogEntry
            {
                Date = $"{month} {day}",
                Time = time,
                Hostname = hostname,
                Process = process,
                Pid = pid,
                Message = message
            };
        }

        private SyslogEntry MakeMalformedEntry(string raw)
        {
            return new SyslogEntry
            {
                Date = "",
                Time = "",
                Hostname = "",
                Process = "-",
                Pid = "-",
                Message = raw
            };
        }

        private class SyslogEntry
        {
            public string Date { get; set; }
            public string Time { get; set; }
            public string Hostname { get; set; }
            public string Process { get; set; }
            public string Pid { get; set; }
            public string Message { get; set; }
        }

        // =====================================================================
        // UI HELPERS
        // =====================================================================
        private async Task LogSysAsync(RichTextBox box, string type, string msg)
        {
            if (box.InvokeRequired)
                box.Invoke((MethodInvoker)(() => box.AppendText($"[{type}] {msg}\n")));
            else
                box.AppendText($"[{type}] {msg}\n");

            await Task.Yield();
        }

        private void UpdateStatusLabelSysSafe(ToolStripStatusLabel label, string text)
        {
            if (label == null) return;
            var parent = label.GetCurrentParent();
            if (parent?.InvokeRequired == true)
                parent.Invoke((MethodInvoker)(() => label.Text = text));
            else
                label.Text = text;
        }
    }
}
