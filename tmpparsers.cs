using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LAP
{
    public partial class MainClass : Form
    {
        // ============================================================
        // ASYNCHRONOUS PARSER FOR UTMP / WTMP / BTMP
        // ============================================================
        public async Task ParseTmpLogsAsync(
            string inputFolder,
            string outputFolder,
            RichTextBox logBox,
            ToolStripProgressBar progressBar,
            ToolStripStatusLabel statusLabel)
        {
            try
            {
                if (!Directory.Exists(inputFolder))
                {
                    MessageBox.Show("Input folder does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (!Directory.Exists(outputFolder))
                {
                    MessageBox.Show("Output folder does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var tmpFiles = new DirectoryInfo(inputFolder)
                    .GetFiles("*", SearchOption.TopDirectoryOnly)
                    .Where(f =>
                        f.Name.StartsWith("utmp", StringComparison.OrdinalIgnoreCase) ||
                        f.Name.StartsWith("wtmp", StringComparison.OrdinalIgnoreCase) ||
                        f.Name.StartsWith("btmp", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (tmpFiles.Length == 0)
                {
                    MessageBox.Show("No Utmp/Wtmp/Btmp files found in the selected folder.", "No Logs Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                UpdateStatusLabelSafe(statusLabel, "Processing Utmp/Btmp/Wtmp logs...");
                UpdateProgress(progressBar, 0);
                await LogAsync(logBox, $"[INFO] Found {tmpFiles.Length} log files to process.\n");

                int processedFiles = 0;
                int totalRecords = 0;

                foreach (var file in tmpFiles)
                {
                    processedFiles++;
                    string outputFile = Path.Combine(outputFolder, $"{file.Name}.csv");
                    await LogAsync(logBox, $"[INFO] Parsing {file.Name}...\n");

                    using (var writer = new StreamWriter(outputFile, false, Encoding.Unicode))
                    {
                        string header = "Login type\tLogin Descr.\tPID\tTerm\tUsername\tHostname\tTermination status\tExit Status\tSession\tTimestamp\tDate\tTime\tMicroseconds\tIP Address";
                        await writer.WriteLineAsync(header);

                        using (FileStream fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (BinaryReader br = new BinaryReader(fs))
                        {
                            const int recordSize = 384;
                            long fileLength = fs.Length;
                            int recordCount = 0;

                            while (fs.Position + recordSize <= fileLength)
                            {
                                byte[] record = br.ReadBytes(recordSize);
                                if (record.Length < recordSize) break;

                                int loginType = BitConverter.ToInt32(record, 0);
                                string loginDescr = GetLoginTypeDescription(loginType);

                                // PID (uint32) – coerente con la struttura .utmp
                                uint pid = BitConverter.ToUInt32(record, 4);

                                string term = GetAsciiString(record, 8, 32);
                                string username = GetAsciiString(record, 44, 32);
                                string hostname = GetAsciiString(record, 76, 256);

                                short termStatus = BitConverter.ToInt16(record, 332);
                                short exitStatus = BitConverter.ToInt16(record, 334);
                                int session = BitConverter.ToInt32(record, 336);

                                uint timestamp = BitConverter.ToUInt32(record, 340);
                                string date = ConvertUnixTimestampToDate((long)timestamp); // riuso helper esistente (long)
                                string time = ConvertUnixTimestampToTime((long)timestamp);

                                uint microsecs = BitConverter.ToUInt32(record, 344);

                                // IPv4 dai 4 byte (se presenti; altrimenti 0.0.0.0)
                                string ipAddress = $"{SafeByte(record, 348)}.{SafeByte(record, 349)}.{SafeByte(record, 350)}.{SafeByte(record, 351)}";

                                string row = string.Join("\t", new[]
                                {
                                    loginType.ToString(),
                                    loginDescr,
                                    pid.ToString(),
                                    term,
                                    username,
                                    hostname,
                                    termStatus.ToString(),
                                    exitStatus.ToString(),
                                    session.ToString(),
                                    timestamp.ToString(),
                                    date,
                                    time,
                                    microsecs.ToString(),
                                    ipAddress
                                });

                                await writer.WriteLineAsync(row);
                                recordCount++;
                            }

                            totalRecords += recordCount;
                        }
                    }

                    int percent = (int)Math.Round(processedFiles * 100.0 / tmpFiles.Length);
                    UpdateProgress(progressBar, percent);
                }

                await LogAsync(logBox,
                    $"\n[OK] Parsing completed successfully.\n" +
                    $"[INFO] Total files processed: {processedFiles}\n" +
                    $"[INFO] Total records parsed: {totalRecords}\n");

                UpdateStatusLabelSafe(statusLabel, "Completed.");
                UpdateProgress(progressBar, 0);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An exception occurred:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatusLabelSafe(statusLabel, "Error.");
                UpdateProgress(progressBar, 0);
            }
        }

        private string GetLoginTypeDescription(int type)
        {
            return type switch
            {
                0 => "Empty",                  // No valid user accounting information
                1 => "Run Level",              // System runlevel changed
                2 => "Boot Time",              // Time of system boot
                3 => "New Time",               // Time after system clock changed
                4 => "Old Time",               // Time when system clock changed
                5 => "Process Spawned by Init",// Spawned by init process
                6 => "Login",                  // Session leader of a logged-in user
                7 => "User Process Start",     // Normal user process
                8 => "Process End",            // Terminated process
                9 => "Accounting",             // Accounting
                _ => "Unknown"
            };
        }

        private string GetAsciiString(byte[] data, int start, int length)
        {
            try
            {
                string s = Encoding.ASCII.GetString(data, start, length);
                return s.Replace('\0', ' ').Trim();
            }
            catch
            {
                return "";
            }
        }

        private byte SafeByte(byte[] data, int index)
        {
            if (index < 0 || index >= data.Length) return 0;
            return data[index];
        }

        private void UpdateStatusLabelSafe(ToolStripStatusLabel label, string text)
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
