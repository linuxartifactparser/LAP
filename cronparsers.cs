using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LAP
{
    public partial class MainClass : Form
    {
        // ============================================================
        // ASYNCHRONOUS PARSER FOR CRON LOGS
        // ============================================================
        public async Task ParseCronLogsAsync(
            string inputFolder,
            string outputFolder,
            RichTextBox logBox,
            ToolStripProgressBar progressBar,
            ToolStripStatusLabel statusLabel)
        {
            try
            {
                if (!Directory.Exists(outputFolder))
                {
                    MessageBox.Show("Output folder does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var cronFiles = new DirectoryInfo(inputFolder)
                    .GetFiles("*", SearchOption.TopDirectoryOnly)
                    .Where(f => f.Name.StartsWith("cron", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (cronFiles.Length == 0)
                {
                    MessageBox.Show("No cron log files found in the selected folder.", "No Logs Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                UpdateStatusLabelCronSafe(statusLabel, "Parsing cron logs...");
                UpdateProgress(progressBar, 0);
                await LogAsync(logBox, $"[INFO] Found {cronFiles.Length} cron log files to process.\n");

                string outputFile = Path.Combine(outputFolder, "cron.csv");
                var entries = new List<CronEntry>();

                int totalLines = 0;
                int validLines = 0;
                int processedFiles = 0;

                foreach (var file in cronFiles)
                {
                    processedFiles++;
                    await LogAsync(logBox, $"[INFO] Parsing {file.Name}...\n");

                    string[] lines = await File.ReadAllLinesAsync(file.FullName, Encoding.UTF8);
                    totalLines += lines.Length;

                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var entry = ParseCronLine(line);
                        if (entry != null)
                        {
                            entries.Add(entry);
                            validLines++;
                        }
                    }

                    int percent = (int)Math.Round(processedFiles * 100.0 / cronFiles.Length);
                    UpdateProgress(progressBar, percent);
                }

                // Sort entries by DateTime
                var sorted = entries.OrderBy(e => e.DateTime).ToList();

                using (var writer = new StreamWriter(outputFile, false, Encoding.Unicode))
                {
                    string header = "Date\tTime\tHostname\tProcess/Service\tPID\tMessage";
                    await writer.WriteLineAsync(header);

                    foreach (var e in sorted)
                    {
                        string row = $"{e.Date}\t{e.Time}\t{e.Hostname}\t{e.Process}\t{e.Pid}\t{e.Message}";
                        await writer.WriteLineAsync(row);
                    }
                }

                await LogAsync(logBox,
                    $"\n[OK] Parsing completed successfully.\n" +
                    $"[INFO] Total files processed: {processedFiles}\n" +
                    $"[INFO] Total lines read: {totalLines}\n" +
                    $"[INFO] Total valid entries: {validLines}\n" +
                    $"[INFO] CSV saved to: {outputFile}\n");

                UpdateStatusLabelCronSafe(statusLabel, "Completed.");
                UpdateProgress(progressBar, 0);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An exception occurred:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatusLabelCronSafe(statusLabel, "Error.");
                UpdateProgress(progressBar, 0);
            }
        }

        // ============================================================
        // PARSING HELPERS
        // ============================================================
        private CronEntry? ParseCronLine(string line)
        {
            // Supporta sia CRON[1234]: che CRON:
            var regexWithPid = new Regex(@"^([A-Z][a-z]{2})\s+(\d{1,2})\s+(\d{2}:\d{2}:\d{2})\s+(\S+)\s+([A-Za-z0-9_\-\/]+)\[(\d+)\]:\s+(.*)$");
            var regexNoPid = new Regex(@"^([A-Z][a-z]{2})\s+(\d{1,2})\s+(\d{2}:\d{2}:\d{2})\s+(\S+)\s+([A-Za-z0-9_\-\/]+):\s+(.*)$");

            Match m;
            bool hasPid;

            if (regexWithPid.IsMatch(line))
            {
                m = regexWithPid.Match(line);
                hasPid = true;
            }
            else if (regexNoPid.IsMatch(line))
            {
                m = regexNoPid.Match(line);
                hasPid = false;
            }
            else
            {
                return null;
            }

            string monthStr = m.Groups[1].Value;
            string dayStr = m.Groups[2].Value.PadLeft(2, '0');
            string time = m.Groups[3].Value;
            string hostname = m.Groups[4].Value;
            string process = m.Groups[5].Value;
            string pid = hasPid ? m.Groups[6].Value : "-";
            string message = hasPid ? m.Groups[7].Value.Trim() : m.Groups[6].Value.Trim();

            // Aggiunge l’anno corrente per ordinamento corretto
            int year = DateTime.UtcNow.Year;
            string combined = $"{monthStr} {dayStr} {year} {time}";

            if (!DateTime.TryParseExact(combined, "MMM dd yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime dt))
                return null;

            return new CronEntry
            {
                Date = $"{monthStr} {dayStr}",
                Time = time,
                Hostname = hostname,
                Process = process,
                Pid = pid,
                Message = message,
                DateTime = dt
            };
        }

        private class CronEntry
        {
            public string Date { get; set; } = "";
            public string Time { get; set; } = "";
            public string Hostname { get; set; } = "";
            public string Process { get; set; } = "";
            public string Pid { get; set; } = "";
            public string Message { get; set; } = "";
            public DateTime DateTime { get; set; }
        }

        private void UpdateStatusLabelCronSafe(ToolStripStatusLabel label, string text)
        {
            if (label == null) return;
            var parent = label.GetCurrentParent();
            if (parent?.InvokeRequired == true)
                parent.Invoke((MethodInvoker)(() => label.Text = text));
            else
                label.Text = text;
        }

        // N.B.: UpdateProgress e LogAsync vengono riutilizzati dal modulo logparsers.cs
    }
}
