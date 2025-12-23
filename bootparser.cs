using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LAP
{
    public partial class MainClass : Form
    {
        public async Task ParseBootLogsAsync(
        string inputFolder,
        string outputFolder,
        RichTextBox logBox,
        ToolStripProgressBar progressBar,
        ToolStripStatusLabel statusLabel)
        {
            void Log(string msg) => logBox?.AppendText(msg + Environment.NewLine);
            void SetStatus(string msg) { if (statusLabel != null) statusLabel.Text = msg; }
            void SetProgress(int value) { if (progressBar != null) progressBar.Value = Math.Max(0, Math.Min(100, value)); }

            SetProgress(0);
            SetStatus("Starting boot.log parsing...");
            Log("=== Boot.log parsing started ===");

            try
            {
                // Validate input paths
                if (string.IsNullOrWhiteSpace(inputFolder) || !Directory.Exists(inputFolder))
                {
                    MessageBox.Show("The specified source folder does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (string.IsNullOrWhiteSpace(outputFolder) || !Directory.Exists(outputFolder))
                {
                    MessageBox.Show("The specified destination folder does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Find all boot.log* files
                var allFiles = Directory.GetFiles(inputFolder, "boot.log*");
                var currentBoot = allFiles.Where(f => string.Equals(Path.GetFileName(f), "boot.log", StringComparison.OrdinalIgnoreCase));
                var datedBoots = allFiles
                    .Where(f => !string.Equals(Path.GetFileName(f), "boot.log", StringComparison.OrdinalIgnoreCase))
                    .Select(f => new
                    {
                        Path = f,
                        Ok = TryParseDateSuffix(Path.GetFileName(f), out var dt),
                        Date = TryParseDateSuffix(Path.GetFileName(f), out var dt2) ? dt2 : DateTime.MinValue
                    })
                    .OrderByDescending(x => x.Date)
                    .ThenBy(x => x.Path)
                    .Select(x => x.Path);

                var orderedFiles = currentBoot.Concat(datedBoots).ToList();
                if (orderedFiles.Count == 0)
                {
                    MessageBox.Show("No boot.log files were found in the selected folder.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var ansiRegex = new Regex(@"\x1B\[[0-9;]*m", RegexOptions.Compiled);
                var sepRegex = new Regex(@"^-{2,}\s+[A-Za-z]{3}\s[A-Za-z]{3}\s\d{1,2}\s\d{2}:\d{2}:\d{2}\s[A-Z]+\s\d{4}\s+-{2,}$", RegexOptions.Compiled);

                // Each detected boot section → key = "Current Boot", "12 October 2025_1", ...
                var bootSections = new List<(string Label, List<string> Lines)>();

                // For bootlog-stats.csv
                var allLinesForStats = new List<string>();

                // Info printed in UI about reboots
                var rebootDates = new List<string>();

                int total = orderedFiles.Count;
                for (int idx = 0; idx < total; idx++)
                {
                    string file = orderedFiles[idx];
                    string filename = Path.GetFileName(file);
                    string baseLabel = GetColumnBaseLabel(filename);

                    string[] rawLines = await File.ReadAllLinesAsync(file).ConfigureAwait(true);
                    var cleaned = rawLines
                        .Select(l => ansiRegex.Replace(l, string.Empty).Replace("\r", ""))
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .ToList();

                    // --- Split into boot sections ---
                    var sections = new List<List<string>>();
                    var currentSection = new List<string>();

                    foreach (var line in cleaned)
                    {
                        if (sepRegex.IsMatch(line))
                        {
                            string dateText = line.Replace("-", "").Trim();
                            rebootDates.Add(dateText);

                            if (currentSection.Count > 0)
                            {
                                sections.Add(currentSection);
                                currentSection = new List<string>();
                            }
                            currentSection.Add(line); // keep separator
                        }
                        else
                        {
                            currentSection.Add(line);
                        }
                    }

                    if (currentSection.Count > 0)
                        sections.Add(currentSection);

                    if (sections.Count == 0)
                        sections.Add(cleaned);

                    // --- Assign labels (with _ instead of -) ---
                    for (int s = 0; s < sections.Count; s++)
                    {
                        string label =
                            (sections.Count == 1)
                            ? baseLabel
                            : $"{baseLabel}_{s + 1}";   // <<< underscore notation

                        bootSections.Add((label, sections[s]));
                        allLinesForStats.AddRange(sections[s]);
                    }

                    SetProgress((int)Math.Round(((idx + 1) / (double)total) * 100.0));
                    SetStatus($"Processed {idx + 1}/{total}: {filename}");
                    Log($"File analyzed: {filename} (sections: {sections.Count})");
                    await Task.Yield();
                }

                // =====================================================================
                //  NEW LOGIC for bootlog.csv (global unique messages with occurrence map)
                // =====================================================================

                // Final map: message → in which sections it appears
                var messageMap = new Dictionary<string, HashSet<string>>();

                // Keep messages in global first-seen order
                var messageOrder = new List<string>();

                foreach (var section in bootSections)
                {
                    string sectionLabel = section.Label;

                    foreach (string line in section.Lines)
                    {
                        // Skip separator lines in output CSV
                        if (sepRegex.IsMatch(line))
                            continue;

                        if (!messageMap.ContainsKey(line))
                        {
                            messageMap[line] = new HashSet<string>();
                            messageOrder.Add(line);
                        }

                        messageMap[line].Add(sectionLabel);
                    }
                }

                // Column list for CSV
                var headerColumns = new List<string>();
                headerColumns.Add("Message");
                headerColumns.AddRange(bootSections.Select(s => s.Label).Distinct());

                string finalCsv = Path.Combine(outputFolder, "bootlog.csv");
                using (var sw = new StreamWriter(finalCsv, false))
                {
                    sw.WriteLine(string.Join(",", headerColumns.Select(CsvQuote)));

                    foreach (string msg in messageOrder)
                    {
                        var row = new List<string>();
                        row.Add(CsvQuote(msg));

                        foreach (var sec in headerColumns.Skip(1))
                        {
                            if (messageMap[msg].Contains(sec))
                                row.Add(CsvQuote(sec));
                            else
                                row.Add("");
                        }

                        sw.WriteLine(string.Join(",", row));
                    }
                }

                // =====================================================================
                //  bootlog-stats.csv (unchanged)
                // =====================================================================
                var stats = allLinesForStats
                    .Where(l => !sepRegex.IsMatch(l))
                    .GroupBy(l => l)
                    .Select(g => new { Message = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ThenBy(x => x.Message)
                    .ToList();

                string statsCsv = Path.Combine(outputFolder, "bootlog-stats.csv");
                using (var swStats = new StreamWriter(statsCsv, false))
                {
                    swStats.WriteLine("Count,Message");
                    foreach (var item in stats)
                    {
                        swStats.WriteLine($"{item.Count},{CsvQuote(item.Message)}");
                    }
                }

                SetProgress(100);
                SetStatus("Parsing completed.");
                Log($"Parsing completed. Files saved:\n{finalCsv}\n{statsCsv}");
                Log("");

                foreach (var date in rebootDates.Distinct())
                    Log($"[INFO] Computer rebooted: {date}");
            }
            catch (Exception ex)
            {
                SetStatus("Error during parsing.");
                Log($"[ERROR] {ex.Message}");
                MessageBox.Show($"Error during parsing: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetProgress(0);
                Log("=== Boot.log parsing finished ===");
            }
        }


        // ===== Helpers =====

        private static bool TryParseDateSuffix(string fileName, out DateTime date)
        {
            date = DateTime.MinValue;
            var parts = fileName.Split('-');
            if (parts.Length < 2) return false;
            string yyyymmdd = parts.Last();
            return DateTime.TryParseExact(yyyymmdd, "yyyyMMdd", CultureInfo.InvariantCulture,
                                          DateTimeStyles.None, out date);
        }

        private static string GetColumnBaseLabel(string fileName)
        {
            if (string.Equals(fileName, "boot.log", StringComparison.OrdinalIgnoreCase))
                return "Current Boot";

            if (TryParseDateSuffix(fileName, out var dt))
                return dt.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture);

            return fileName;
        }

        private static string CsvQuote(string s)
        {
            if (s == null) return "";
            string safe = s.Replace("\"", "\"\"");
            return $"\"{safe}\"";
        }
    }
}
