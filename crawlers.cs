using Microsoft.VisualBasic.ApplicationServices;
using Microsoft.VisualBasic.Devices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LAP
{
    public partial class MainClass : Form
    {
        private readonly HashSet<string> _printedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private List<string> fileList = new();
        private readonly List<string> searchedFiles = new();
        private List<string> userList = new();
        private List<string> dirList = new();


        private async Task CrawlAsync(string startPath)
        {
            toolStripStatusLabel1.Text = "Crawling file system...";

            // 1️ — CRAWL DIRECTORY PRIMA DI FILES (popola dirList)
            try
            {
                var allDirs = Directory.EnumerateDirectories(startPath, "*", SearchOption.AllDirectories);

                foreach (var dir in allDirs)
                {
                    string normalizedDir = dir.Replace('\\', '/');
                    if (!dirList.Contains(normalizedDir))
                        dirList.Add(normalizedDir);
                }

                // Deduplicazione e ordinamento finale (case-insensitive)
                dirList = dirList
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception ex)
            {
                Invoke(new Action(() =>
                {
                    richTextBox2.SelectionColor = Color.Red;
                    richTextBox2.AppendText($"[ERROR] Directory enumeration failed: {ex.Message}\n");
                    richTextBox2.SelectionColor = Color.Black;
                }));
            }

            // 2️⃣ — FILE ENUMERATION (come prima)
            var allFiles = Directory.EnumerateFiles(startPath, "*", SearchOption.AllDirectories);
            fileList = allFiles.ToList();

            int total = fileList.Count;
            int processed = 0;

            // Disattiva aggiornamenti continui per evitare flickering
            Invoke(new Action(() => listView13.BeginUpdate()));

            foreach (var file in fileList)
            {
                string ext = Path.GetExtension(file).ToLower();

                // ----------------------------------------------
                // SEZIONE DEDICATA AI PARSER DI SERIE
                // ----------------------------------------------
                if (ext == ".service")
                    await ParseServiceFile(file);
                else if (ext == ".desktop")
                    await ParseDesktopFile(file);
                else if (ext == ".socket")
                    await ParseSocketFile(file);
                else if (ext == ".timer")
                    await ParseTimerFile(file);
                else if (Path.GetFileName(file).Equals("recently-used.xbel", StringComparison.OrdinalIgnoreCase) ||
                         Path.GetFileName(file).StartsWith("recently-used.xbel.", StringComparison.OrdinalIgnoreCase))
                    await ParseXbelFile(file);
                // ----------------------------------------------
                // FINE SEZIONE PARSER
                // ----------------------------------------------

                processed++;
                if (processed % 20 == 0)
                {
                    Invoke(new Action(() =>
                    {
                        toolStripProgressBar1.Value = Math.Min(processed * 100 / total, 100);
                    }));
                }
            }

            // Riattiva aggiornamenti e ridisegna
            Invoke(new Action(() =>
            {
                listView13.EndUpdate();
                toolStripProgressBar1.Value = 100;
            }));

            toolStripStatusLabel1.Text = "Crawl completed.";
        }

        //=====================================================

        private async Task ParseServiceFile(string filePath)
        {
            await Task.Run(() =>
            {
                try
                {
                    var lines = File.ReadAllLines(filePath);
                    if (lines.Length == 0) return; // file vuoto

                    string description = "", execStart = "", execReload = "", execStopPost = "";

                    // direttive che andranno nella colonna "Other Parameters"
                    var otherParamsKeys = new[]
                    {
                "Restart=", "Requires=", "Alias=", "SuccessAction=", "Environment="
            };

                    var otherParams = new List<string>();

                    foreach (var line in lines)
                    {
                        string trimmed = line.Trim();

                        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                            continue;

                        if (trimmed.StartsWith("Description=", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(description))
                            description = trimmed.Substring("Description=".Length).Trim();

                        else if (trimmed.StartsWith("ExecStart=", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(execStart))
                            execStart = trimmed.Substring("ExecStart=".Length).Trim();

                        else if (trimmed.StartsWith("ExecReload=", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(execReload))
                            execReload = trimmed.Substring("ExecReload=".Length).Trim();

                        else if (trimmed.StartsWith("ExecStopPost=", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(execStopPost))
                            execStopPost = trimmed.Substring("ExecStopPost=".Length).Trim();

                        else
                        {
                            foreach (var key in otherParamsKeys)
                            {
                                if (trimmed.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                                {
                                    string value = trimmed.Substring(key.Length).Trim();
                                    if (!string.IsNullOrEmpty(value))
                                        otherParams.Add($"{key.TrimEnd('=')}={value}");
                                    break;
                                }
                            }
                        }
                    }

                    // unisci i parametri extra
                    string otherParamsJoined = string.Join(", ", otherParams);

                    // scarta file inutili
                    if (string.IsNullOrEmpty(description) &&
                        string.IsNullOrEmpty(execStart) &&
                        string.IsNullOrEmpty(execReload) &&
                        string.IsNullOrEmpty(execStopPost) &&
                        string.IsNullOrEmpty(otherParamsJoined))
                    {
                        return;
                    }

                    string name = Path.GetFileName(filePath);

                    // aggiorna ListView sul thread UI
                    Invoke(new Action(() =>
                    {
                        var item = new ListViewItem(filePath);
                        item.SubItems.Add(name);
                        item.SubItems.Add(description);
                        item.SubItems.Add(execStart);
                        item.SubItems.Add(execReload);
                        item.SubItems.Add(execStopPost);
                        item.SubItems.Add(otherParamsJoined);

                        listView13.Items.Add(item);
                    }));
                }
                catch (Exception ex)
                {
                    Invoke(new Action(() =>
                    {
                        toolStripStatusLabel1.Text = $"Error reading: {Path.GetFileName(filePath)} ({ex.Message})";
                    }));
                }
            });
        }


        //=========================================================

        private async Task ParseDesktopFile(string filePath)
        {
            await Task.Run(() =>
            {
                try
                {
                    var lines = File.ReadAllLines(filePath);
                    if (lines.Length == 0) return; // file completamente vuoto

                    string name = "", exec = "", comment = "", categories = "",
                           tryExec = "", terminal = "", noDisplay = "", keywords = "";

                    foreach (var line in lines)
                    {
                        string trimmed = line.Trim();

                        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                            continue;

                        if (trimmed.StartsWith("Name=", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(name))
                            name = trimmed.Substring("Name=".Length).Trim();

                        else if (trimmed.StartsWith("Exec=", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(exec))
                            exec = trimmed.Substring("Exec=".Length).Trim();

                        else if (trimmed.StartsWith("Comment=", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(comment))
                            comment = trimmed.Substring("Comment=".Length).Trim();

                        else if (trimmed.StartsWith("Categories=", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(categories))
                            categories = trimmed.Substring("Categories=".Length).Trim();

                        else if (trimmed.StartsWith("TryExec=", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(tryExec))
                            tryExec = trimmed.Substring("TryExec=".Length).Trim();

                        else if (trimmed.StartsWith("Terminal=", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(terminal))
                            terminal = trimmed.Substring("Terminal=".Length).Trim();

                        else if (trimmed.StartsWith("NoDisplay=", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(noDisplay))
                            noDisplay = trimmed.Substring("NoDisplay=".Length).Trim();

                        else if (trimmed.StartsWith("Keywords=", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(keywords))
                            keywords = trimmed.Substring("Keywords=".Length).Trim();
                    }

                    // ✅ scarta file senza direttive utili
                    if (string.IsNullOrEmpty(name) &&
                        string.IsNullOrEmpty(exec) &&
                        string.IsNullOrEmpty(comment) &&
                        string.IsNullOrEmpty(categories) &&
                        string.IsNullOrEmpty(tryExec) &&
                        string.IsNullOrEmpty(terminal) &&
                        string.IsNullOrEmpty(noDisplay) &&
                        string.IsNullOrEmpty(keywords))
                    {
                        return;
                    }

                    string fileName = Path.GetFileName(filePath);

                    // aggiorna la ListView sul thread UI
                    Invoke(new Action(() =>
                    {
                        var item = new ListViewItem(filePath);
                        item.SubItems.Add(fileName);
                        item.SubItems.Add(name);
                        item.SubItems.Add(exec);
                        item.SubItems.Add(comment);
                        item.SubItems.Add(categories);
                        item.SubItems.Add(tryExec);
                        item.SubItems.Add(terminal);
                        item.SubItems.Add(noDisplay);
                        item.SubItems.Add(keywords);

                        listView12.Items.Add(item);
                    }));
                }
                catch (Exception ex)
                {
                    Invoke(new Action(() =>
                    {
                        toolStripStatusLabel1.Text = $"Error reading: {Path.GetFileName(filePath)} ({ex.Message})";
                    }));
                }
            });
        }



        //=========================================================

        private async Task ParseSocketFile(string filePath)
        {
            await Task.Run(() =>
            {
                try
                {
                    var lines = File.ReadAllLines(filePath);
                    if (lines.Length == 0) return; // file vuoto

                    string description = "", listenStream = "", fileDescriptorName = "",
                           service = "", socketMode = "", directoryMode = "",
                           execStartPost = "";

                    // direttive da includere in "Other Parameters"
                    var otherParamsKeys = new[]
                    {
                "ListenFIFO=", "SymLinks=", "PassCredentials=", "PassSecurity=",
                "ReceiveBuffer=", "ListenNetLink=", "ListenDatagram=", "Accept=",
                "ListenSequentialPacket=", "RemoveOnStop=", "MaxConnections=",
                "SendBuffer=", "SocketUser=", "SocketGroup=", "ConditionUser="
            };

                    var otherParams = new List<string>();

                    foreach (var line in lines)
                    {
                        string trimmed = line.Trim();

                        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                            continue;

                        if (trimmed.StartsWith("Description=", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(description))
                            description = trimmed.Substring("Description=".Length).Trim();

                        else if (trimmed.StartsWith("ListenStream=", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(listenStream))
                            listenStream = trimmed.Substring("ListenStream=".Length).Trim();

                        else if (trimmed.StartsWith("FileDescriptorName=", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(fileDescriptorName))
                            fileDescriptorName = trimmed.Substring("FileDescriptorName=".Length).Trim();

                        else if (trimmed.StartsWith("Service=", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(service))
                            service = trimmed.Substring("Service=".Length).Trim();

                        else if (trimmed.StartsWith("SocketMode=", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(socketMode))
                            socketMode = trimmed.Substring("SocketMode=".Length).Trim();

                        else if (trimmed.StartsWith("DirectoryMode=", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(directoryMode))
                            directoryMode = trimmed.Substring("DirectoryMode=".Length).Trim();

                        else if (trimmed.StartsWith("ExecStartPost=", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(execStartPost))
                            execStartPost = trimmed.Substring("ExecStartPost=".Length).Trim();

                        else
                        {
                            // controlla se la riga è una delle "Other Parameters"
                            foreach (var key in otherParamsKeys)
                            {
                                if (trimmed.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                                {
                                    string value = trimmed.Substring(key.Length).Trim();
                                    if (!string.IsNullOrEmpty(value))
                                        otherParams.Add($"{key.TrimEnd('=')}={value}");
                                    break;
                                }
                            }
                        }
                    }

                    // costruisci la stringa "Other Parameters"
                    string otherParamsJoined = string.Join(", ", otherParams);

                    // ✅ scarta file senza alcuna direttiva utile
                    if (string.IsNullOrEmpty(description) &&
                        string.IsNullOrEmpty(listenStream) &&
                        string.IsNullOrEmpty(fileDescriptorName) &&
                        string.IsNullOrEmpty(service) &&
                        string.IsNullOrEmpty(socketMode) &&
                        string.IsNullOrEmpty(directoryMode) &&
                        string.IsNullOrEmpty(execStartPost) &&
                        string.IsNullOrEmpty(otherParamsJoined))
                    {
                        return;
                    }

                    string fileName = Path.GetFileName(filePath);

                    // aggiorna la ListView sul thread UI
                    Invoke(new Action(() =>
                    {
                        var item = new ListViewItem(filePath);
                        item.SubItems.Add(fileName);
                        item.SubItems.Add(description);
                        item.SubItems.Add(listenStream);
                        item.SubItems.Add(fileDescriptorName);
                        item.SubItems.Add(service);
                        item.SubItems.Add(socketMode);
                        item.SubItems.Add(directoryMode);
                        item.SubItems.Add(otherParamsJoined);
                        item.SubItems.Add(execStartPost);

                        listView14.Items.Add(item);
                    }));
                }
                catch (Exception ex)
                {
                    Invoke(new Action(() =>
                    {
                        toolStripStatusLabel1.Text = $"Error reading: {Path.GetFileName(filePath)} ({ex.Message})";
                    }));
                }
            });
        }

        //=========================================================

        private async Task ParseTimerFile(string filePath)
        {
            await Task.Run(() =>
            {
                try
                {
                    var lines = File.ReadAllLines(filePath);
                    if (lines.Length == 0) return; // file completamente vuoto

                    string description = "", onCalendar = "";

                    // direttive da raccogliere nella colonna "Other Parameters"
                    var otherParamsKeys = new[]
                    {
                "RandomizedDelaySec=", "Persistent=", "AccuracySec=",
                "OnBootSec=", "OnUnitActiveSec=", "ConditionKernelCommandLine=",
                "OnStartupSec=", "ConditionPathExists=", "After=", "ConditionVirtualization="
            };

                    var otherParams = new List<string>();

                    foreach (var line in lines)
                    {
                        string trimmed = line.Trim();

                        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                            continue;

                        if (trimmed.StartsWith("Description=", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(description))
                            description = trimmed.Substring("Description=".Length).Trim();

                        else if (trimmed.StartsWith("OnCalendar=", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(onCalendar))
                            onCalendar = trimmed.Substring("OnCalendar=".Length).Trim();

                        else
                        {
                            foreach (var key in otherParamsKeys)
                            {
                                if (trimmed.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                                {
                                    string value = trimmed.Substring(key.Length).Trim();
                                    if (!string.IsNullOrEmpty(value))
                                        otherParams.Add($"{key.TrimEnd('=')}={value}");
                                    break;
                                }
                            }
                        }
                    }

                    string otherParamsJoined = string.Join(", ", otherParams);

                    //  Scarta file che non contengono alcuna direttiva utile
                    if (string.IsNullOrEmpty(description) &&
                        string.IsNullOrEmpty(onCalendar) &&
                        string.IsNullOrEmpty(otherParamsJoined))
                    {
                        return;
                    }

                    string fileName = Path.GetFileName(filePath);

                    // aggiorna ListView sul thread UI
                    Invoke(new Action(() =>
                    {
                        var item = new ListViewItem(filePath);
                        item.SubItems.Add(fileName);
                        item.SubItems.Add(description);
                        item.SubItems.Add(onCalendar);
                        item.SubItems.Add(otherParamsJoined);

                        listView15.Items.Add(item);
                    }));
                }
                catch (Exception ex)
                {
                    Invoke(new Action(() =>
                    {
                        toolStripStatusLabel1.Text = $"Error reading: {Path.GetFileName(filePath)} ({ex.Message})";
                    }));
                }
            });
        }

        //=========================================================

        private async Task ParseXbelFile(string filePath)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (new FileInfo(filePath).Length == 0)
                        return; // file vuoto

                    var xmlDoc = new System.Xml.XmlDocument();

                    var settings = new System.Xml.XmlReaderSettings
                    {
                        IgnoreComments = true,
                        IgnoreWhitespace = true,
                        CheckCharacters = false
                    };
                    using (var reader = System.Xml.XmlReader.Create(filePath, settings))
                    {
                        xmlDoc.Load(reader);
                    }

                    var bookmarks = xmlDoc.SelectNodes("//*[local-name()='bookmark']");
                    if (bookmarks == null || bookmarks.Count == 0)
                        return;

                    string xmlFileName = Path.GetFileName(filePath);

                    foreach (System.Xml.XmlNode bm in bookmarks)
                    {
                        try
                        {
                            string href = bm.Attributes?["href"]?.InnerText ?? "";
                            string added = bm.Attributes?["added"]?.InnerText ?? "";
                            string modified = bm.Attributes?["modified"]?.InnerText ?? "";
                            string visited = bm.Attributes?["visited"]?.InnerText ?? "";

                            string title = "";
                            string mimeType = "";
                            string appName = "";
                            string appExec = "";
                            string appCount = "";
                            string appTimestamp = "";

                            var titleNode = bm.SelectSingleNode("*[local-name()='title']");
                            if (titleNode != null)
                                title = titleNode.InnerText.Trim();

                            var mimeNode = bm.SelectSingleNode(".//*[local-name()='mime-type']");
                            if (mimeNode?.Attributes?["type"] != null)
                                mimeType = mimeNode.Attributes["type"].InnerText.Trim();

                            var appNode = bm.SelectSingleNode(".//*[local-name()='application']");
                            if (appNode?.Attributes != null)
                            {
                                appName = appNode.Attributes["name"]?.InnerText ?? "";
                                appExec = appNode.Attributes["exec"]?.InnerText ?? "";
                                appCount = appNode.Attributes["count"]?.InnerText ?? "";
                                appTimestamp = appNode.Attributes["timestamp"]?.InnerText ?? "";
                            }

                            if (string.IsNullOrEmpty(href) && string.IsNullOrEmpty(title))
                                continue;

                            Invoke(new Action(() =>
                            {
                                var item = new ListViewItem(filePath);
                                item.SubItems.Add(xmlFileName);
                                item.SubItems.Add(href);
                                item.SubItems.Add(added);
                                item.SubItems.Add(modified);
                                item.SubItems.Add(visited);
                                item.SubItems.Add(mimeType);
                                item.SubItems.Add(appName);
                                item.SubItems.Add(appExec);
                                item.SubItems.Add(appCount);
                                item.SubItems.Add(appTimestamp);
                                item.SubItems.Add(title);  // 👉 "Title" ora come ultima colonna

                                listView11.Items.Add(item);
                            }));
                        }
                        catch
                        {
                            continue; // ignora bookmark problematico
                        }
                    }
                }
                catch (Exception ex)
                {
                    Invoke(new Action(() =>
                    {
                        toolStripStatusLabel1.Text = $"Error reading: {Path.GetFileName(filePath)} ({ex.Message})";
                    }));
                }
            });
        }

        //=========================================================

        private async Task CheckAndPrintAsync(
              string startPath,
              string relativePath,
              Func<string, Task> printFunction,
              string description,
              string note)
        {
            try
            {
                // normalizza
                string cleanedRelative = relativePath.TrimStart('/', '\\');
                string fullPath = Path.Combine(
                    startPath,
                    cleanedRelative.Replace("/", Path.DirectorySeparatorChar.ToString())
                );

                //  registra l'atteso (una sola volta)
                if (!searchedFiles.Contains(relativePath, StringComparer.OrdinalIgnoreCase))
                    searchedFiles.Add(relativePath);

                if (File.Exists(fullPath))
                {
                    PrintBanner(relativePath, description, note, Color.Green);
                    await printFunction(fullPath);
                }
                // se non esiste, non stampiamo adesso: ci pensa CheckMissingFiles()
            }
            catch (Exception ex)
            {
                AppendTextColorSafe(richTextBox2, $"[ERROR] {relativePath}: {ex.Message}\n", Color.DarkRed);
            }
        }

        //=========================================================
        private void PrintBanner(string filePath, string description, string note, Color color)
        {
            string banner =
        $@"-----------------------------------------------------------------------------------------
File:         {filePath}
Description:  {description}
Note:         {note}
-----------------------------------------------------------------------------------------{Environment.NewLine}{Environment.NewLine}";

            AppendTextColorAndStyle(richTextBox2, banner, color, FontStyle.Bold);
        }

        //=========================================================

        private void AppendTextSafe(RichTextBox box, string text)
        {
            if (box.InvokeRequired)
            {
                box.Invoke(new Action(() => box.AppendText(text)));
            }
            else
            {
                box.AppendText(text);
            }
        }

        //=========================================================
        private async Task PrintNoCommBase(string filePath)
        {
            try
            {
                string[] lines = await File.ReadAllLinesAsync(filePath);
                var filtered = lines
                    .Where(line => !line.TrimStart().StartsWith("#") && !string.IsNullOrWhiteSpace(line))
                    .ToList();

                if (filtered.Count > 0)
                {
                    foreach (string line in filtered)
                        AppendTextSafe(richTextBox2, line + Environment.NewLine);
                }
                else
                {
                    AppendTextSafe(richTextBox2, "[INFO] No non-comment lines found\n");
                }

                AppendTextSafe(richTextBox2, Environment.NewLine);
            }
            catch (Exception ex)
            {
                AppendTextSafe(richTextBox2, $"[ERROR] Cannot read {filePath}: {ex.Message}{Environment.NewLine}");
            }
        }


        //=========================================================
        private async Task PrintNoComm(string filePath)
        {
            try
            {
                string[] lines = await File.ReadAllLinesAsync(filePath);

                // 🔹 Filtra righe non vuote e senza commenti
                var filtered = lines
                    .Where(line => !line.TrimStart().StartsWith("#") && !string.IsNullOrWhiteSpace(line))
                    .Select(line => Regex.Replace(line.Trim(), @"\s+", " ")) // normalizza spazi multipli
                    .ToList();

                if (filtered.Count == 0)
                {
                    AppendTextColorSafe(richTextBox2, "[INFO] File found and parsed but no non-commented lines found\n\n", Color.Gray);
                    return;
                }

                // 🔹 Divide ogni riga in colonne
                var splitLines = filtered.Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToList();

                // 🔹 Calcola la larghezza massima per ogni colonna
                int maxCols = splitLines.Max(arr => arr.Length);
                int[] colWidths = new int[maxCols];

                foreach (var arr in splitLines)
                {
                    for (int i = 0; i < arr.Length; i++)
                    {
                        int len = arr[i].Length;
                        if (len > colWidths[i])
                            colWidths[i] = len;
                    }
                }

                // 🔹 Stampa le righe con colonne allineate
                foreach (var arr in splitLines)
                {
                    var sb = new StringBuilder("\t"); // indentazione iniziale
                    for (int i = 0; i < arr.Length; i++)
                    {
                        sb.Append(arr[i].PadRight(colWidths[i] + 2)); // +2 per spaziatura tra colonne
                    }

                    AppendTextColorSafe(richTextBox2, sb.ToString().TrimEnd() + Environment.NewLine, Color.Black);
                }

                AppendTextColorSafe(richTextBox2, Environment.NewLine, Color.Black);
            }
            catch (Exception ex)
            {
                AppendTextColorSafe(richTextBox2, $"[ERROR] Cannot read {filePath}: {ex.Message}{Environment.NewLine}", Color.DarkRed);
            }
        }

      

        //=========================================================
        private void AppendTextColorSafe(RichTextBox box, string text, Color color)
        {
            if (box.InvokeRequired)
            {
                box.Invoke(new Action(() => AppendTextColorSafe(box, text, color)));
            }
            else
            {
                int start = box.TextLength;
                box.AppendText(text);
                int end = box.TextLength;

                box.Select(start, end - start);
                box.SelectionColor = color;
                box.SelectionLength = 0;
                box.SelectionStart = box.TextLength;
                box.SelectionColor = box.ForeColor;
            }
        }

        //=========================================================

        private void PrintBigBanner()
        {
            // Azzera eventuali contenuti precedenti
            richTextBox2.Clear();

            string topBottomLine = "  ********************************************************************************";
            string titleLine = "\n  *                       QUICK ARTIFACT CRAWLER                                 *" + Environment.NewLine;
            string body =
        @"  *                                                                              *
  *   Sparse Linux OS artifacts in clear text are shown here, with no comments   *
  *   Also, content of sensitive folders can be shown in this view.              *
  *   This will also indicate you whether some important artifacts have been     *
  *   collected, or are missing from the collection.                             *
  *                                                                              *" + Environment.NewLine;

            // Riga superiore (azzurra)
            AppendTextColorSafe(richTextBox2, topBottomLine, Color.Blue);

            // Riga del titolo (bold rosso scuro)
            AppendTextColorAndStyle(richTextBox2, titleLine, Color.DarkRed, FontStyle.Bold);

            // Corpo del banner (verde scuro)
            AppendTextColorSafe(richTextBox2, body, Color.DarkGreen);

            // Riga inferiore (azzurra)
            AppendTextColorSafe(richTextBox2, topBottomLine, Color.Blue);

            AppendTextColorSafe(richTextBox2, Environment.NewLine, Color.Black);
            richTextBox2.AppendText("\n");
        }

        //=========================================================

        private void AppendTextColorAndStyle(RichTextBox box, string text, Color color, FontStyle style)
        {
            if (box.InvokeRequired)
            {
                box.Invoke(new Action(() => AppendTextColorAndStyle(box, text, color, style)));
            }
            else
            {
                int start = box.TextLength;
                box.AppendText(text);
                int end = box.TextLength;

                box.Select(start, end - start);
                box.SelectionColor = color;
                box.SelectionFont = new Font(box.Font, style);

                box.SelectionLength = 0;
                box.SelectionStart = box.TextLength;
                box.SelectionColor = box.ForeColor;
                box.SelectionFont = box.Font; // reset stile
            }
        }

        //=========================================================

        private async Task CheckAndPrintFolderAsync(
     string startPath,
     string relativeFolder,
     Action<string> PrintFileNames,
     string comment1,
     string comment2)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(startPath) || string.IsNullOrWhiteSpace(relativeFolder))
                        return;

                    // Normalizza percorsi
                    string normStart = startPath.Replace('\\', '/').TrimEnd('/');
                    string fullPath = Path.Combine(startPath, relativeFolder.TrimStart('/', '\\'));
                    string normalizedFullPath = Path.GetFullPath(fullPath).Replace('\\', '/');

                    // Trova i file presenti in questa directory (o sotto-directory)
                    var matchingFiles = fileList
                        .Select(f => f.Replace('\\', '/'))
                        .Where(f => f.StartsWith(normalizedFullPath + "/", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    // -------------------- BANNER --------------------
                    Invoke(new Action(() =>
                    {
                        AppendTextColorAndStyle(richTextBox2,
                            "-----------------------------------------------------------------------------------------\n",
                            Color.MediumPurple,
                            FontStyle.Bold);

                        AppendTextColorAndStyle(richTextBox2, $"Folder:       {relativeFolder}\n", Color.MediumPurple, FontStyle.Bold);
                        AppendTextColorAndStyle(richTextBox2, $"Description:  {comment1}\n", Color.MediumPurple, FontStyle.Bold);
                        AppendTextColorAndStyle(richTextBox2, $"Note:         {comment2}\n", Color.MediumPurple, FontStyle.Bold);

                        AppendTextColorAndStyle(richTextBox2,
                            "-----------------------------------------------------------------------------------------\n\n",
                            Color.MediumPurple,
                            FontStyle.Bold);
                    }));

                    // -------------------- ESITI --------------------
                    bool existsAsDirectory = dirList.Any(d =>
                        d.Equals(normalizedFullPath, StringComparison.OrdinalIgnoreCase) ||
                        d.Equals(normalizedFullPath + "/", StringComparison.OrdinalIgnoreCase));

                    if (matchingFiles.Count > 0)
                    {
                        // Cartella esistente con file interni
                        Invoke(new Action(() => PrintFileNames(normalizedFullPath)));
                    }
                    else if (existsAsDirectory)
                    {
                        // Cartella esistente ma senza file
                        Invoke(new Action(() =>
                        {
                            AppendTextColorSafe(richTextBox2,
                                "    This folder is present but it does not contain any file.\n\n",
                                Color.Gray);
                        }));
                    }
                    else
                    {
                        // Cartella non esistente
                        Invoke(new Action(() =>
                        {
                            AppendTextColorSafe(richTextBox2,
                                $"    {relativeFolder} ...PATH NOT FOUND\n\n",
                                Color.Red);
                        }));
                    }

                    // Footer per separazione visuale
                    Invoke(new Action(() =>
                    {
                        AppendTextColorAndStyle(richTextBox2, "\n", Color.MediumPurple, FontStyle.Bold);
                    }));
                }
                catch (Exception ex)
                {
                    Invoke(new Action(() =>
                    {
                        AppendTextColorSafe(richTextBox2,
                            $"Error during folder check: {ex.Message}\n",
                            Color.Red);
                    }));
                }
            });
        }


        //=========================================================

        private void PrintFileNames(string folderFullPath)
        {
            try
            {
                string normalizedFolder = folderFullPath.Replace('\\', '/').TrimEnd('/');

                // Trova i file appartenenti a questa directory
                var filesInFolder = fileList
                    .Select(f => f.Replace('\\', '/'))
                    .Where(f => f.StartsWith(normalizedFolder + "/", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (filesInFolder.Count == 0)
                {
                    richTextBox2.SelectionColor = Color.Gray;
                    richTextBox2.AppendText("    This folder is present but it does not contain any file.\n\n");
                    richTextBox2.SelectionColor = Color.Black;
                    return;
                }

                foreach (var absFile in filesInFolder)
                {
                    string fileName = Path.GetFileName(absFile);
                    richTextBox2.SelectionColor = Color.Black;
                    richTextBox2.AppendText($"    - {fileName}\n");
                }

                richTextBox2.AppendText("\n");
            }
            catch (Exception ex)
            {
                richTextBox2.SelectionColor = Color.Red;
                richTextBox2.AppendText($"Error while listing files: {ex.Message}\n");
                richTextBox2.SelectionColor = Color.Black;
            }
        }


        //=========================================================

        private async Task CheckMissingFilesAsync()
        {
            toolStripStatusLabel1.Text = "Checking for files not found...";

            await Task.Run(() =>
            {
                // Root della collection
                string normStart = textBox8.Text.Replace('\\', '/').TrimEnd('/');

                // Set di file realmente presenti
                var foundSet = new HashSet<string>(
                    fileList.Select(p => p.Replace('\\', '/')),
                    StringComparer.OrdinalIgnoreCase
                );

                var missing = new List<string>();

                // 🔥 ESCLUDI TUTTE LE DIRECTORY
                // (dirList contiene i percorsi di TUTTE le directory trovate dal crawler)
                var searchedFilesOnlyFiles = searchedFiles
                    .Select(s => s.Replace('\\', '/'))
                    .Where(s => !dirList.Contains(s))     // <-- questa riga esclude *tutte* le directory
                    .ToList();

                // Determina quali FILE sono mancanti
                foreach (var searched in searchedFilesOnlyFiles)
                {
                    string normalized = searched.Replace('\\', '/').Trim();

                    bool exists = foundSet.Any(found =>
                        found.EndsWith(normalized.TrimStart('/'), StringComparison.OrdinalIgnoreCase));

                    if (!exists)
                        missing.Add(normalized);
                }

                // Calcolo maxLen (relativo)
                int maxLen = missing.Count > 0
                    ? missing.Select(abs =>
                    {
                        string rel = abs.StartsWith(normStart, StringComparison.OrdinalIgnoreCase)
                            ? abs.Substring(normStart.Length)
                            : abs;

                        if (!rel.StartsWith("/"))
                            rel = "/" + rel;

                        return rel.Length;
                    }).Max()
                    : 0;

                // ---------------- HEADER ----------------
                richTextBox2.Invoke(new Action(() =>
                {
                    AppendTextColorAndStyle(
                        richTextBox2,
                        "\n============================================================\n" +
                        "[INFO] Files NOT found during scan:\n" +
                        "============================================================\n\n",
                        Color.DarkRed,
                        FontStyle.Bold
                    );
                }));

                // Nessun file mancante
                if (missing.Count == 0)
                {
                    richTextBox2.Invoke(new Action(() =>
                    {
                        AppendTextColorSafe(richTextBox2, "[INFO] All expected files were found.\n", Color.DarkGreen);
                    }));
                    return;
                }

                // ---------------- STAMPA FILE MANCANTI ----------------
                foreach (var absPath in missing.OrderBy(f => f))
                {
                    string normAbs = absPath.Replace('\\', '/');

                    // Conversione a relativo
                    string relative = normAbs.StartsWith(normStart, StringComparison.OrdinalIgnoreCase)
                        ? normAbs.Substring(normStart.Length)
                        : normAbs;

                    if (!relative.StartsWith("/"))
                        relative = "/" + relative;

                    string padded = relative.PadRight(maxLen + 2);

                    richTextBox2.Invoke(new Action(() =>
                    {
                        AppendTextColorSafe(richTextBox2, padded, Color.Black);
                        AppendTextColorSafe(richTextBox2, "....... ", Color.Gray);
                        AppendTextColorSafe(richTextBox2, "NOT FOUND\n", Color.DarkRed);
                    }));
                }
            });

            toolStripStatusLabel1.Text = "Artifact check completed.";
        }

        //=========================================================

        private List<string> EnumerateUsersFromCollection(List<string> fileList, string startPath)
        {
            var users = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // Normalizza separatori e scansiona
                foreach (var file in fileList)
                {
                    string normalized = file.Replace('\\', '/');

                    // cerca pattern "/home/<utente>/" o "/home/<utente>\"
                    int idx = normalized.IndexOf("/home/", StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        string sub = normalized.Substring(idx + "/home/".Length);
                        string[] parts = sub.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0)
                            users.Add(parts[0]);
                    }
                }

                // aggiunge l’utente di default “root”
               // users.Add("root");
            }
            catch (Exception ex)
            {
                AppendTextColorSafe(richTextBox2, $"[ERROR] Enumerating users: {ex.Message}\n", Color.DarkRed);
            }

            return users.OrderBy(u => u).ToList();
        }

        //=========================================================

        private void PrintEnumeratedUsers(List<string> fileList, string startPath)
        {
            userList = EnumerateUsersFromCollection(fileList, startPath);

            AppendTextColorAndStyle(
                richTextBox2,
                "\n============================\n" +
                "Enumerated Users:\n" +
                "============================\n\n",
                Color.DarkOrange,
                FontStyle.Bold);

            if (userList.Count == 0)
            {
                AppendTextColorSafe(richTextBox2, "No users found.\n", Color.DarkRed);
            }
            else
            {
                foreach (var user in userList)
                    AppendTextColorSafe(richTextBox2, $" - {user}\n", Color.Black);
            }

            AppendTextColorSafe(richTextBox2, " - root\n", Color.Black);
            AppendTextColorSafe(richTextBox2, "\n", Color.Black);
        }


        //=========================================================

        private async Task RunArtifactChecksAsync(string startPath)
        {
            toolStripStatusLabel1.Text = "Checking specific artifacts...";

            // Questi verranno registrati SEMPRE in searchedFiles, anche se non esistono.

            await CheckAndPrintFolderAsync(startPath,
                    "/var/www/html",
                    PrintFileNames,
                    "Folder containing files related to the web server (FILE LIST, NO CONTENT)",
                    "Threat Actors usually place WebShells in this location");

            await CheckAndPrintFolderAsync(startPath,
                    "/var/tmp",
                    PrintFileNames,
                    "Temp location (FILE LIST, NO CONTENT)",
                    "");

            await CheckAndPrintFolderAsync(startPath,
                    "/tmp",
                    PrintFileNames,
                    "Temp location (FILE LIST, NO CONTENT)",
                    "");

            await CheckAndPrintFolderAsync(startPath,
                    "/var/spool/at",
                    PrintFileNames,
                    "Folder containing AT jobs (deprecated - FILE LIST, NO CONTENT)",
                    "If files are found they need to be manually inspected (possible ASEP)");

            await CheckAndPrintFolderAsync(startPath,
                    "/var/spool/cron/atjobs",
                    PrintFileNames,
                    "List of queued cron jobs set with AT (deprecated - FILE LIST, NO CONTENT)",
                    "If files are found they need to be manually inspected (possible ASEP)");

            await CheckAndPrintAsync(startPath,
                    "/etc/anacrontab",
                    PrintNoComm,
                    "System wide asynchronous crontab",
                    "This is just for asynchronous activities");

            await CheckAndPrintAsync(startPath,
                    "/etc/.wgetrc",
                    PrintNoComm,
                    "Hidden Wget config file (per-user configuration)",
                    "By default, all lines are commented. Created the first time Wget is used.");

            await CheckAndPrintAsync(startPath,
                    "/etc/resolv.conf",
                    PrintNoComm,
                    "Contains IP vs domain mapping, comparable to the Windows \"Hosts\" artifact",
                    "");

            await CheckAndPrintAsync(startPath,
                    "/etc/hosts",
                    PrintNoComm,
                    "Lists static hostname mappings to IP addresses",
                    "by default it's 127.0.0.1 localhost, etc etc");

            await CheckAndPrintAsync(startPath,
                    "/etc/fstab",
                    PrintNoComm,
                    "Defines how disk partitions and storage devices are mounted at boot", "");

            await CheckAndPrintAsync(startPath,
                    "/etc/sudoers",
                    PrintNoComm,
                    "List of users who are entitled to run commands with SUDO", "");

            await CheckAndPrintAsync(startPath,
                    "/etc/hosts.allow",
                    PrintNoComm,
                    "List of hosts that are allowed to access the system", "This is an old tactic to allow backdoors");


            await CheckAndPrintAsync(startPath,
                    "/etc/hosts.deny",
                    PrintNoComm,
                    "List of hosts that are not allowed to access the system", "By default, this should be blank");

            await CheckAndPrintAsync(startPath,
                    "/etc/crontab",
                    PrintNoComm,
                    "System wide crontab", "");

            await CheckAndPrintAsync(startPath,
                    "/etc/anacrontab",
                    PrintNoComm,
                    "System wide asynchronous crontab", "This is just for anynchronous activities");

            await CheckAndPrintAsync(startPath,
                    "/etc/wgetrc",
                    PrintNoComm,
                    "This is the Wget config file - proxies can be set up through its modification", "By default, all the lines in this file are commented. This file is created the very first time Wget is used");

            await CheckAndPrintAsync(startPath,
                    "/etc/.wgetrc",
                    PrintNoComm,
                    "This is the Wget config file - proxies can be set up through its modification", "By default, all the lines in this file are commented. This file is created the very first time Wget is used");

            await CheckAndPrintAsync(startPath,
                   "/etc/modules",
                   PrintNoComm,
                   "List of modules to load at boot time (obsolete, replaced by /etc/modules-load.d)", "By default, all the lines in this file are commented.");

            await CheckAndPrintAsync(startPath,
                   "/etc/os-release",
                   PrintNoComm,
                   "distribution / filesystem version", "");

            await CheckAndPrintAsync(startPath,
                   "/etc/lsb-release",
                   PrintNoComm,
                   "distribution / filesystem version", "");

            await CheckAndPrintAsync(startPath,
                   "/etc/machine-id",
                   PrintNoComm,
                   "ID of this computer", "this is useful to match the random folder name under \"/var/log/journal\" with the machine ID");

            await CheckAndPrintAsync(startPath,
                   "/etc/timezone",
                   PrintNoComm,
                  "Timezone setting for this computer", "");

            await CheckAndPrintAsync(startPath,
                   "/var/lib/NetworkManager/NetworkManager-intern.conf",
                   PrintNoComm,
                   "Config file for the NetworkManager service", "By default, this should be blank");

            await CheckAndPrintAsync(startPath,
                   "/var/lib/NetworkManager/NetworkManager.state",
                   PrintNoComm,
                   "Tells which network interfaces are enabled and which are not", "");

            await CheckAndPrintAsync(startPath,
                   "/var/lib/NetworkManager/seen-bssids",
                   PrintNoComm,
                   "Wireless networks seen", "");

            await CheckAndPrintAsync(startPath,
                   "/var/lib/NetworkManager/timestamps",
                   PrintNoComm,
                   "Timestamps for the DHCP leases", "Decode Unix timestamps using the Contextmenu");

            await CheckAndPrintAsync(startPath,
                   "/etc/ld.so.preload",
                   PrintNoComm,
                  "Library Preloader", "This is a sensitive target for the \"Dynamic Linker Hijacking\" attack technique");

            await CheckAndPrintAsync(startPath,
                   "/etc/environment",
                   PrintNoComm,
                  "Paths for binary files", "");

            await CheckAndPrintAsync(startPath,
                   "/etc/network/if-up.d/upstart",
                   PrintNoComm,
                  "Content of this file runs when the network is up", "By default, this file should not be present at all");

            await CheckAndPrintAsync(startPath,
                   "/etc/update-motd.d/00-header",
                   PrintNoComm,
                  "Mot of the day file", "This can be backdoored by adding bash code");

            await CheckAndPrintAsync(startPath,
                   "/etc/sysconfig/iptables",
                   PrintNoComm,
                  "Persistent Iptables rules", "");

            await CheckAndPrintAsync(startPath,
                   "/proc/sys/kernel/randomize_va_space",
                   PrintNoComm,
                  "File that tells if ASLR is enabled or not", "0 = No Randomization, 1=Conservative Randomization, 2=Full Randomization");

            await CheckAndPrintAsync(startPath,
                   "/proc/cmdline",
                   PrintNoComm,
                  "Command line of the boot loader", "In case of malicious manipulation, this could lead to where the bootkit is");

            await CheckAndPrintAsync(startPath,
                   "/proc/partitions",
                   PrintNoComm,
                  "List of partitions for this Linux OS", "");

            await CheckAndPrintAsync(startPath,
                   "/proc/swaps",
                   PrintNoComm,
                  "List of swap files", "");

            await CheckAndPrintAsync(startPath,
                   "/proc/mounts",
                   PrintNoComm,
                  "List of mounts", "");

            await CheckAndPrintAsync(startPath,
                   "/proc/uptime",
                   PrintNoComm,
                  "System Uptime", "Decode with ContextMenu, first value is Uptime (seconds), second value is Idle");

            await CheckAndPrintAsync(startPath,
                   "/proc/version",
                   PrintNoComm,
                  "Kernel and OS version", "");

            await CheckAndPrintAsync(startPath,
                   "/boot/grub/grub.cfg",
                   PrintNoComm,
                  "GRUB Boot configuration file", "Pay attention to \"insmod\" and \"initrd\"");

            await CheckAndPrintAsync(startPath, "/root/.ssh/known_hosts", PrintNoComm,
                " * ROOT USER * - SSH known hosts for this user",
                "May contain remote systems the user connected to");


            await CheckAndPrintAsync(startPath, "/root/.ssh/authorized_keys", PrintNoComm,
                " * ROOT USER * - Public SSH keys for \"quick\" authentication - SSH protocols 1.3 and 1.5",
                "Adversary may modify this file to maintain persistence on the victim host");


            await CheckAndPrintAsync(startPath, "/root/.ssh/authorized_keys2", PrintNoComm,
                " * ROOT USER * - Public SSH keys for \"quick\" authentication SSH protocols 2.x",
                "Adversary may modify this file to maintain persistence on the victim host");


            await CheckAndPrintAsync(startPath, "/root/.wget-hsts", PrintNoComm,
                " * ROOT USER * - Wget HSTS cache",
                "Contains hostnames and timestamps of visited sites");


            await CheckAndPrintAsync(startPath, "/root/.bash_history", PrintNoComm,
                " * ROOT USER * - History of commands run via bash",
                "");

            await CheckAndPrintAsync(startPath, "/root/.sh_history", PrintNoComm,
                " * ROOT USER * - History of commands run via shell",
                "");

            await CheckAndPrintAsync(startPath, "/root/.python_history", PrintNoComm,
                " * ROOT USER * - History of Python shell",
                "");


            await CheckAndPrintAsync(startPath, "/root/.sqlite_history", PrintNoComm,
                " * ROOT USER * - History of SQLite Commands",
                "");

            await CheckAndPrintAsync(startPath, "/root/.bash_logout", PrintNoComm,
                " * ROOT USER * - Script that runs when Bash is closed",
                "May contain (legitimate or suspicious) commands that run when bash is closed");


            await CheckAndPrintAsync(startPath, "/root/.lesshist", PrintNoComm,
                " * ROOT USER * - History for the \"Less\" command",
                "");

            await CheckAndPrintAsync(startPath, "/root/.lesshst", PrintNoComm,
                " * ROOT USER * - History for the \"Less\" command",
                "");

            await CheckAndPrintAsync(startPath, "/root/.histfile", PrintNoComm,
                " * ROOT USER * - History for the \"Less\" command",
                "");

            await CheckAndPrintAsync(startPath, "/root/.curlrc", PrintNoComm,
                " * ROOT USER * - Contains settings for the User Agent of Curl and settings for HTTP header (referrer etc.)",
                "This file is created the first time \"Curl\" is used");

            await CheckAndPrintAsync(startPath, "/root/.bashrc", PrintNoCommBase,
                " * ROOT USER * - Script that is executed when a user logs in (terminal configs). It contains the HISTFILE and HISTFILESIZE variables of \"bash_history\"",
                "HISTFILE=0 may be an antiforensics. Also, this can be used as persistence mechanism by adding commands");

            await CheckAndPrintAsync(startPath, "/root/.profile", PrintNoCommBase,
                " * ROOT USER * - Configuration file for the \"Bourne Shell\" (.sh)",
                "");

            await CheckAndPrintAsync(startPath, "/root/wget-hsts", PrintNoComm,
                " * ROOT USER * - Wget HSTS cache",
                "Contains hostnames and timestamps of visited sites");

            await CheckAndPrintAsync(startPath, "/root/bash_history", PrintNoComm,
                " * ROOT USER * - History of commands run via bash",
                "");

            await CheckAndPrintAsync(startPath, "/root/sh_history", PrintNoComm,
                " * ROOT USER * - History of commands run via shell",
                "");

            await CheckAndPrintAsync(startPath, "/root/python_history", PrintNoComm,
                " * ROOT USER * - History of Python shell",
                "");

            await CheckAndPrintAsync(startPath, "/root/sqlite_history", PrintNoComm,
                " * ROOT USER * - History of SQLite Commands",
                "");

            await CheckAndPrintAsync(startPath, "/root/bash_logout", PrintNoComm,
                " * ROOT USER * - Script that runs when Bash is closed",
                "May contain (legitimate or suspicious) commands that run when bash is closed");

            await CheckAndPrintAsync(startPath, "/root/lesshist", PrintNoComm,
                " * ROOT USER * - History for the \"Less\" command",
                "");

            await CheckAndPrintAsync(startPath, "/root/lesshst", PrintNoComm,
                " * ROOT USER * - History for the \"Less\" command",
                "");

            await CheckAndPrintAsync(startPath, "/root/histfile", PrintNoComm,
                " * ROOT USER * - History for the \"Less\" command",
                "");

            await CheckAndPrintAsync(startPath, "/root/curlrc", PrintNoComm,
                " * ROOT USER * - Contains settings for the User Agent of Curl and settings for HTTP header (referrer etc.)",
                "This file is created the first time \"Curl\" is used");

            await CheckAndPrintAsync(startPath, "/root/bashrc", PrintNoComm,
                " * ROOT USER * - Script that is executed when a user logs in (terminal configs). It contains the HISTFILE and HISTFILESIZE variables of \"bash_history\"",
                "HISTFILE=0 may be an antiforensics. Also, this can be used as persistence mechanism by adding commands");

            await CheckAndPrintAsync(startPath, "/root/profile", PrintNoCommBase,
                " * ROOT USER * - Configuration file for the \"Bourne Shell\" (.sh)",
                "");

            foreach (var user in userList)
            {
                string path = $"/home/{user}/.ssh/known_hosts";
                await CheckAndPrintAsync(startPath, path, PrintNoComm,
                    "SSH known hosts for this user",
                    "May contain remote systems the user connected to");
            }

            foreach (var user in userList)
            {
                string path = $"/home/{user}/.ssh/authorized_keys";
                await CheckAndPrintAsync(startPath, path, PrintNoComm,
                    "Public SSH keys for \"quick\" authentication - SSH protocols 1.3 and 1.5",
                    "Adversary may modify this file to maintain persistence on the victim host");
            }

            foreach (var user in userList)
            {
                string path = $"/home/{user}/.ssh/authorized_keys2";
                await CheckAndPrintAsync(startPath, path, PrintNoComm,
                    "Public SSH keys for \"quick\" authentication SSH protocols 2.x",
                    "Adversary may modify this file to maintain persistence on the victim host");
            }

            foreach (var user in userList)
            {
                string hsts = $"/home/{user}/.wget-hsts";
                await CheckAndPrintAsync(startPath, hsts, PrintNoComm,
                    "Wget HSTS cache",
                    "Contains hostnames and timestamps of visited sites");
            }

            foreach (var user in userList)
            {
                string hsts = $"/home/{user}/.bash_history";
                await CheckAndPrintAsync(startPath, hsts, PrintNoComm,
                    "History of commands run via bash",
                    "");
            }

            foreach (var user in userList)
            {
                string hsts = $"/home/{user}/.sh_history";
                await CheckAndPrintAsync(startPath, hsts, PrintNoComm,
                    "History of commands run via shell",
                    "");
            }

            foreach (var user in userList)
            {
                string hsts = $"/home/{user}/.python_history";
                await CheckAndPrintAsync(startPath, hsts, PrintNoComm,
                    "History of Python shell",
                    "");
            }

            foreach (var user in userList)
            {
                string hsts = $"/home/{user}/.sqlite_history";
                await CheckAndPrintAsync(startPath, hsts, PrintNoComm,
                    "History of SQLite Commands",
                    "");
            }

            foreach (var user in userList)
            {
                string hsts = $"/home/{user}/.bash_logout";
                await CheckAndPrintAsync(startPath, hsts, PrintNoComm,
                    "Script that runs when Bash is closed",
                    "May contain (legitimate or suspicious) commands that run when bash is closed");
            }

            foreach (var user in userList)
            {
                string hsts = $"/home/{user}/.lesshist";
                await CheckAndPrintAsync(startPath, hsts, PrintNoComm,
                    "History for the \"Less\" command",
                    "");
            }

            foreach (var user in userList)
            {
                string hsts = $"/home/{user}/.lesshst";
                await CheckAndPrintAsync(startPath, hsts, PrintNoComm,
                    "History for the \"Less\" command",
                    "");
            }

            foreach (var user in userList)
            {
                string hsts = $"/home/{user}/.histfile";
                await CheckAndPrintAsync(startPath, hsts, PrintNoComm,
                    "History for the \"Less\" command",
                    "");
            }

            foreach (var user in userList)
            {
                string hsts = $"/home/{user}/.curlrc";
                await CheckAndPrintAsync(startPath, hsts, PrintNoComm,
                    "Contains settings for the User Agent of Curl and settings for HTTP header (referrer etc.)",
                    "This file is created the first time \"Curl\" is used");
            }

            foreach (var user in userList)
            {
                string hsts = $"/home/{user}/.bashrc";
                await CheckAndPrintAsync(startPath, hsts, PrintNoCommBase,
                    "Script that is executed when a user logs in (terminal configs). It contains the HISTFILE and HISTFILESIZE variables of \"bash_history\"",
                    "HISTFILE=0 may be an antiforensics. Also, this can be used as persistence mechanism by adding commands");
            }

            foreach (var user in userList)
            {
                string hsts = $"/home/{user}/.profile";
                await CheckAndPrintAsync(startPath, hsts, PrintNoCommBase,
                    "Configuration file for the \"Bourne Shell\" (.sh)",
                    "");
            }

            foreach (var user in userList)
            {
                string hsts = $"/home/{user}/wget-hsts";
                await CheckAndPrintAsync(startPath, hsts, PrintNoComm,
                    "Wget HSTS cache",
                    "Contains hostnames and timestamps of visited sites");
            }

            foreach (var user in userList)
            {
                string hsts = $"/home/{user}/bash_history";
                await CheckAndPrintAsync(startPath, hsts, PrintNoComm,
                    "History of commands run via bash",
                    "");
            }

            foreach (var user in userList)
            {
                string hsts = $"/home/{user}/sh_history";
                await CheckAndPrintAsync(startPath, hsts, PrintNoComm,
                    "History of commands run via shell",
                    "");
            }

            foreach (var user in userList)
            {
                string hsts = $"/home/{user}/python_history";
                await CheckAndPrintAsync(startPath, hsts, PrintNoComm,
                    "History of Python shell",
                    "");
            }

            foreach (var user in userList)
            {
                string hsts = $"/home/{user}/sqlite_history";
                await CheckAndPrintAsync(startPath, hsts, PrintNoComm,
                    "History of SQLite Commands",
                    "");
            }

            foreach (var user in userList)
            {
                string hsts = $"/home/{user}/bash_logout";
                await CheckAndPrintAsync(startPath, hsts, PrintNoComm,
                    "Script that runs when Bash is closed",
                    "May contain (legitimate or suspicious) commands that run when bash is closed");
            }

            foreach (var user in userList)
            {
                string hsts = $"/home/{user}/lesshist";
                await CheckAndPrintAsync(startPath, hsts, PrintNoComm,
                    "History for the \"Less\" command",
                    "");
            }

            foreach (var user in userList)
            {
                string hsts = $"/home/{user}/lesshst";
                await CheckAndPrintAsync(startPath, hsts, PrintNoComm,
                    "History for the \"Less\" command",
                    "");
            }

            foreach (var user in userList)
            {
                string hsts = $"/home/{user}/histfile";
                await CheckAndPrintAsync(startPath, hsts, PrintNoComm,
                    "History for the \"Less\" command",
                    "");
            }

            foreach (var user in userList)
            {
                string hsts = $"/home/{user}/curlrc";
                await CheckAndPrintAsync(startPath, hsts, PrintNoComm,
                    "Contains settings for the User Agent of Curl and settings for HTTP header (referrer etc.)",
                    "This file is created the first time \"Curl\" is used");
            }

            foreach (var user in userList)
            {
                string hsts = $"/home/{user}/bashrc";
                await CheckAndPrintAsync(startPath, hsts, PrintNoCommBase,
                    "Script that is executed when a user logs in (terminal configs). It contains the HISTFILE and HISTFILESIZE variables of \"bash_history\"",
                    "HISTFILE=0 may be an antiforensics. Also, this can be used as persistence mechanism by adding commands");
            }

            foreach (var user in userList)
            {
                string hsts = $"/home/{user}/profile";
                await CheckAndPrintAsync(startPath, hsts, PrintNoCommBase,
                    "Configuration file for the \"Bourne Shell\" (.sh)",
                    "");
            }
            //=================================================
        }

        //=========================================================
    }
}
