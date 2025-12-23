using Be.Windows.Forms;
using ScintillaNET;
using System.Security.Cryptography;
using System.Text;

namespace LAP
{
    public partial class MainClass : Form
    {
        private HexBox hexBox, hexBox2;
        public bool isELF, fileLoaded;
        private Scintilla scintilla1;

        public MainClass()
        {
            InitializeComponent();

            listView3.SelectedIndexChanged += listView3_SelectedIndexChanged;
            listView4.SelectedIndexChanged += listView4_SelectedIndexChanged;
            listView9.ItemSelectionChanged += listView9_ItemSelectionChanged;
            comboBox2.SelectedIndexChanged += comboBox2_SelectedIndexChanged;
            treeView1.BeforeExpand += treeView1_BeforeExpand;
            treeView1.BeforeCollapse += treeView1_BeforeCollapse;
            treeView1.AfterSelect += treeView1_AfterSelect;


            hexBox = new HexBox
            {
                Dock = DockStyle.Fill,         // riempie lo spazio disponibile
                Font = new Font("Consolas", 10),
                StringViewVisible = true,      // mostra colonna ASCII a destra
                LineInfoVisible = true,        // mostra offset a sinistra
                VScrollBarVisible = true,      // abilita scrollbar verticale
                ReadOnly = true,               // per ora sola lettura
                InfoForeColor = Color.Red,
                BackColor = Color.White,
                ForeColor = Color.Black
            };

            hexBox2 = new HexBox
            {
                Dock = DockStyle.Fill,         // riempie lo spazio disponibile
                Font = new Font("Consolas", 10),
                StringViewVisible = true,      // mostra colonna ASCII a destra
                LineInfoVisible = true,        // mostra offset a sinistra
                VScrollBarVisible = true,      // abilita scrollbar verticale
                ReadOnly = true,               // per ora sola lettura
                InfoForeColor = Color.DarkGreen,
                BackColor = Color.White,
                ForeColor = Color.Black
            };

            hexBox2.ContextMenuStrip = contextMenuStrip5;
            scintilla1 = new Scintilla();

            scintilla1.Dock = DockStyle.Fill;       // Occupa tutto il panel
            scintilla1.WrapMode = WrapMode.Word;
            scintilla1.IndentationGuides = IndentView.LookBoth;
            scintilla1.ContextMenuStrip = contextMenuStrip3;
            scintilla1.Margins[0].Width = 30;       // Margine numeri di linea


            scintilla1.SetFoldMarginColor(true, Color.LightGray);
            scintilla1.SetFoldMarginHighlightColor(true, Color.LightGray);

            // Enable code folding
            scintilla1.SetProperty("fold", "1");
            scintilla1.SetProperty("fold.compact", "1");

            scintilla1.AssignCmdKey(Keys.Control | Keys.U, Command.Undo);
            scintilla1.AssignCmdKey(Keys.Control | Keys.R, Command.Redo);
            scintilla1.AssignCmdKey(Keys.Control | Keys.A, Command.SelectAll);


            // Configure a margin to display folding symbols
            scintilla1.Margins[3].Type = MarginType.Symbol;
            scintilla1.Margins[3].Mask = Marker.MaskFolders;
            scintilla1.Margins[3].Sensitive = true;
            scintilla1.Margins[3].Width = 5;

            // Set colors for all folding markers
            for (int i = 25; i <= 31; i++)
            {
                scintilla1.Markers[i].SetForeColor(Color.Black); // styles for [+] and [-]
                scintilla1.Markers[i].SetBackColor(Color.LightGray); // styles for [+] and [-]
            }

            // Configure folding markers with respective symbols
            scintilla1.Markers[Marker.Folder].Symbol = true ? MarkerSymbol.CirclePlus : MarkerSymbol.BoxPlus;
            scintilla1.Markers[Marker.FolderOpen].Symbol = true ? MarkerSymbol.CircleMinus : MarkerSymbol.BoxMinus;
            scintilla1.Markers[Marker.FolderEnd].Symbol = true ? MarkerSymbol.CirclePlusConnected : MarkerSymbol.BoxPlusConnected;
            scintilla1.Markers[Marker.FolderMidTail].Symbol = MarkerSymbol.TCorner;
            scintilla1.Markers[Marker.FolderOpenMid].Symbol = true ? MarkerSymbol.CircleMinusConnected : MarkerSymbol.BoxMinusConnected;
            scintilla1.Markers[Marker.FolderSub].Symbol = MarkerSymbol.VLine;
            scintilla1.Markers[Marker.FolderTail].Symbol = MarkerSymbol.LCorner;

            // Enable automatic folding
            scintilla1.AutomaticFold = (AutomaticFold.Show | AutomaticFold.Click | AutomaticFold.Change);

            scintilla1.Styles[Style.Default].Font = "Consolas";
            scintilla1.Styles[Style.Default].Size = 10;
            scintilla1.LexerName = "bash";

            scintilla1.Styles[Style.BraceLight].BackColor = Color.LightGray;
            scintilla1.Styles[Style.BraceLight].ForeColor = Color.Red;
            scintilla1.Styles[Style.BraceBad].ForeColor = Color.LightGreen;

            scintilla1.Styles[Style.Cpp.Comment].ForeColor = Color.Green;
            scintilla1.Styles[Style.Cpp.CommentLine].ForeColor = Color.DarkGreen;
            scintilla1.Styles[Style.Perl.CommentLine].ForeColor = Color.Green;
            scintilla1.Styles[Style.Cpp.Word].ForeColor = Color.Red;
            scintilla1.Styles[Style.Cpp.Number].ForeColor = Color.Orange;
            scintilla1.Styles[Style.Perl.Number].ForeColor = Color.Orange;
            scintilla1.Styles[Style.Perl.Regex].ForeColor = Color.Violet;
            scintilla1.Styles[Style.Cpp.Operator].ForeColor = Color.DarkBlue;
            scintilla1.Styles[Style.Perl.Operator].ForeColor = Color.DarkBlue;
            scintilla1.Styles[Style.Cpp.String].ForeColor = Color.DarkRed;
            scintilla1.Styles[Style.Perl.String].ForeColor = Color.DarkRed;

            panel1.Controls.Add(hexBox);
            panel2.Controls.Add(scintilla1);
            panel3.Controls.Add(hexBox2);
        }



        //============================================
        // scegli Path 1
        //============================================

        private void button1_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select the folder containing Linux log files";
                folderDialog.ShowNewFolderButton = false;

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    textBox1.Text = folderDialog.SelectedPath;
                    richTextBox1.AppendText($"[INFO] Input folder selected: {folderDialog.SelectedPath}\n");
                }
                else
                {
                    richTextBox1.AppendText("[INFO] Input folder selection cancelled.\n");
                }
            }
        }


        //============================================
        // scegli Path 2
        //============================================

        private void button2_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select the folder where results will be saved";
                folderDialog.ShowNewFolderButton = true;

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    textBox2.Text = folderDialog.SelectedPath;
                    richTextBox1.AppendText($"[INFO] Output folder selected: {folderDialog.SelectedPath}\n");
                }
                else
                {
                    richTextBox1.AppendText("[INFO] Output folder selection cancelled.\n");
                }
            }
        }


        //============================================
        // pulsante PARSE
        //============================================
        private async void button3_Click(object sender, EventArgs e)
        {
            string inputFolder = textBox1.Text.Trim();
            string outputFolder = textBox2.Text.Trim();
            string selected = comboBox1.SelectedItem?.ToString();

            if (string.IsNullOrEmpty(inputFolder) || string.IsNullOrEmpty(outputFolder) || string.IsNullOrEmpty(selected))
            {
                richTextBox1.AppendText("All fields must be selected before starting the parsing.\n");
                return;
            }

            if (selected == "Audit.log")
            {
                await ParseAuditAsync(inputFolder, outputFolder, richTextBox1, toolStripProgressBar1);
            }

            if (selected == "Utmp - Wtmp - Btmp")
            {
                await ParseTmpLogsAsync(inputFolder, outputFolder, richTextBox1, toolStripProgressBar1, toolStripStatusLabel1);
            }

            if (selected == "Cron")
            {
                await ParseCronLogsAsync(inputFolder, outputFolder, richTextBox1, toolStripProgressBar1, toolStripStatusLabel1);
            }

            if (selected == "Auth")
                await ParseSyslogAsync(inputFolder, outputFolder, richTextBox1,
                    toolStripProgressBar1, toolStripStatusLabel1, "auth", checkBox3.Checked);

            if (selected == "Authlog")
                await ParseSyslogAsync(inputFolder, outputFolder, richTextBox1,
                    toolStripProgressBar1, toolStripStatusLabel1, "authlog", checkBox3.Checked);

            if (selected == "Messages")
                await ParseSyslogAsync(inputFolder, outputFolder, richTextBox1,
                    toolStripProgressBar1, toolStripStatusLabel1, "messages", checkBox3.Checked);

            if (selected == "Secure")
                await ParseSyslogAsync(inputFolder, outputFolder, richTextBox1,
                    toolStripProgressBar1, toolStripStatusLabel1, "secure", checkBox3.Checked);

            if (selected == "Sudo")
            {
                await ParseSudoLogsAsync(inputFolder, outputFolder, richTextBox1,
                    toolStripProgressBar1, toolStripStatusLabel1);
            }

            if (selected == "Boot.log")
            {
                await ParseBootLogsAsync(inputFolder, outputFolder, richTextBox1, toolStripProgressBar1, toolStripStatusLabel1);
            }

        }


        //============================================
        // apre file in hex viewer
        //============================================

        private void button4_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Select a file for the Hex Viewer";
                openFileDialog.Filter = "All files (*.*)|*.*";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string filePath = openFileDialog.FileName;
                        byte[] fileBytes = File.ReadAllBytes(filePath);
                        textBox3.Text = filePath;

                        // Reset variabili di stato
                        listView3.Items.Clear();
                        listView4.Items.Clear();
                        richTextBox3.Clear();
                        richTextBox5.Clear();
                        richTextBox7.Clear();
                        label3.Text = "MD5 : ";
                        label4.Text = "SHA256 : ";
                        linkLabel1.Text = "";
                        isELF = false;
                        fileLoaded = false;

                        // --- Controllo signature ELF (0x7F 45 4C 46) ---
                        if (fileBytes.Length >= 4)
                        {
                            if (fileBytes[0] == 0x7F &&
                                fileBytes[1] == 0x45 && // 'E'
                                fileBytes[2] == 0x4C && // 'L'
                                fileBytes[3] == 0x46)   // 'F'
                            {
                                isELF = true;
                            }
                        }

                        if (fileBytes.Length > 0)
                        {
                            fileLoaded = true;
                        }

                        if (isELF)
                        {
                            LoadELFSections();
                            LoadELFSections2();
                            AnalyzeElf();
                            //   ListLinkedLibraries();
                        }
                        if (!isELF)
                        {
                            MessageBox.Show(
                                "Opened file is not an ELF. Some features will not be available.",
                                "Warning",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning
                            );
                        }

                        // --- Visualizza il file nell’HexBox ---
                        hexBox.ByteProvider = new DynamicByteProvider(fileBytes);
                        string md5Hash, sha256Hash;
                        using (var md5 = MD5.Create())
                        using (var sha256 = SHA256.Create())
                        using (var stream = File.OpenRead(filePath))
                        {
                            md5Hash = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                            stream.Position = 0;
                            sha256Hash = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                        }

                        // --- Prepara il link VirusTotal ---
                        string vtLink = $"https://www.virustotal.com/gui/file/{sha256Hash.ToUpper()}";
                        label3.Text = "MD5 : " + md5Hash;
                        label4.Text = "SHA256 : " + sha256Hash;
                        linkLabel1.Text = vtLink;



                    }
                    catch (Exception ex)
                    {
                        fileLoaded = false; // in caso di errore, resetta lo stato
                        MessageBox.Show($"Error during file opening:\n{ex.Message}",
                                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        //============================================
        // Pulsante del shadow parser
        //============================================

        private async void button6_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Select /etc/shadow file";
                ofd.Filter = "Shadow file|shadow*|All files|*.*";
                ofd.InitialDirectory = "/etc";

                if (ofd.ShowDialog() != DialogResult.OK)
                    return;

                textBox4.Text = ofd.FileName;
                listView2.Items.Clear();

                try
                {
                    await ParseShadowAsync(ofd.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error reading shadow file:\n" + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }


        //============================================
        // Pulsante che estrae le stringhe
        //============================================

        private async void button7_Click(object sender, EventArgs e)
        {
            if (!fileLoaded)
            {
                MessageBox.Show("No file currently loaded.", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!checkBox1.Checked && !checkBox2.Checked)
            {
                MessageBox.Show("Select at least one string type (ASCII or Unicode).", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string filePath = textBox3.Text;
            if (!File.Exists(filePath))
            {
                MessageBox.Show("File not found:\n" + filePath, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            byte[] data;
            try
            {
                data = File.ReadAllBytes(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading file:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // --- Reset UI ---
            listView3.Items.Clear();
            toolStripProgressBar1.Value = 0;
            toolStripProgressBar1.Maximum = 100;
            toolStripStatusLabel1.Text = "Looking for strings.....";
            button7.Enabled = false;

            int minLen = (int)numericUpDown1.Value;

            var progress = new Progress<int>(value =>
            {
                try
                {
                    toolStripProgressBar1.Value = Math.Min(value, 100);
                }
                catch
                {
                    // Ignora eventuali errori se il controllo non è più valido
                }
            });

            List<ListViewItem> results = new List<ListViewItem>();

            try
            {
                results = await Task.Run(() =>
                {
                    List<ListViewItem> found = new List<ListViewItem>();

                    void AddString(int offset, int length, string type, string text)
                    {
                        var item = new ListViewItem(offset.ToString());
                        item.SubItems.Add(length.ToString());
                        item.SubItems.Add(type);
                        item.SubItems.Add(text);
                        lock (found) found.Add(item);
                    }

                    int total = data.Length;
                    int chunk = total / 100;
                    if (chunk == 0) chunk = 1;
                    int lastReported = 0;

                    // --- ASCII ---
                    if (checkBox1.Checked)
                    {
                        int i = 0;
                        while (i < total)
                        {
                            if (data[i] >= 0x20 && data[i] <= 0x7E)
                            {
                                int start = i;
                                while (i < total && data[i] >= 0x20 && data[i] <= 0x7E)
                                    i++;

                                int length = i - start;
                                if (length >= minLen)
                                {
                                    string s = Encoding.ASCII.GetString(data, start, length);
                                    AddString(start, length, "A", s);
                                }
                            }
                            else i++;

                            if (i - lastReported >= chunk)
                            {
                                lastReported = i;
                                ((IProgress<int>)progress).Report(i * 100 / total);
                            }
                        }
                    }

                    // --- Unicode (UTF-16LE) ---
                    if (checkBox2.Checked)
                    {
                        int i = 0;
                        while (i < total - 1)
                        {
                            if (data[i] >= 0x20 && data[i] <= 0x7E && data[i + 1] == 0x00)
                            {
                                int start = i;
                                i += 2;
                                while (i < total - 1 &&
                                       data[i] >= 0x20 && data[i] <= 0x7E && data[i + 1] == 0x00)
                                {
                                    i += 2;
                                }

                                int length = i - start;
                                if (length / 2 >= minLen)
                                {
                                    string s = Encoding.Unicode.GetString(data, start, length);
                                    AddString(start, length, "U", s);
                                }
                            }
                            else i += 2;

                            if (i - lastReported >= chunk)
                            {
                                lastReported = i;
                                ((IProgress<int>)progress).Report(i * 100 / total);
                            }
                        }
                    }

                    ((IProgress<int>)progress).Report(100);
                    return found;
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during string extraction:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // --- Aggiornamento UI finale ---
            listView3.BeginUpdate();
            listView3.Items.AddRange(results.ToArray());
            listView3.EndUpdate();

            toolStripStatusLabel1.Text = "Completed.";
            toolStripProgressBar1.Value = 0;
            button7.Enabled = true;

            if (results.Count == 0)
            {
                MessageBox.Show("No strings found with the specified parameters.",
                    "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show($"{results.Count} strings extracted successfully.",
                    "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        //============================================================
        // Event handler per la selezione della riga nella listview
        //============================================================

        private void listView3_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Se non c’è nulla selezionato, non fare nulla
            if (listView3.SelectedItems.Count == 0)
                return;

            try
            {
                var item = listView3.SelectedItems[0];

                // Colonna 0 = Offset, colonna 1 = Length
                if (!int.TryParse(item.SubItems[0].Text, out int offset))
                    return;

                if (!int.TryParse(item.SubItems[1].Text, out int length))
                    return;

                // Se l’hex viewer è vuoto, esci
                if (hexBox.ByteProvider == null)
                    return;

                // Imposta la selezione
                hexBox.SelectionStart = offset;
                hexBox.SelectionLength = length;

                // Porta l’area selezionata in vista
                hexBox.ScrollByteIntoView(offset);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while highlighting selection:\n{ex.Message}",
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //============================================
        // Pulsante del passwd parser
        //============================================
        private async void button8_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Select /etc/passwd file";
                ofd.Filter = "Passwd file|passwd*|All files|*.*";
                ofd.InitialDirectory = "/etc";

                if (ofd.ShowDialog() != DialogResult.OK)
                    return;

                textBox5.Text = ofd.FileName;
                listView8.Items.Clear();

                try
                {
                    await ParsePasswdAsync(ofd.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error reading passwd file:\n" + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        //============================================
        // Seleziona il percorso di /proc
        //============================================
        private async void button11_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select the /proc directory (Linux)";
                fbd.RootFolder = Environment.SpecialFolder.Desktop;
                fbd.ShowNewFolderButton = false;

                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    procPath = fbd.SelectedPath;

                    if (!Directory.Exists(procPath))
                    {
                        MessageBox.Show("The selected directory does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    listView9.Items.Clear();
                    richTextBox4.Clear();
                    toolStripStatusLabel1.Text = "Parsing /proc ...";
                    toolStripProgressBar1.Value = 0;

                    var entries = await ParseProcDirectoryAsync(procPath);
                    DisplayProcEntries(entries);

                    // Ordina la ListView per PID (numeric ascending)
                    Invoke(new Action(() =>
                    {
                        listView9.ListViewItemSorter = new ListViewNumericComparer(0);
                        listView9.Sort();
                    }));

                    toolStripStatusLabel1.Text = "Parsing completed.";

                    // Breve ritardo visivo, poi resetta la progress bar
                    await Task.Delay(800);
                    toolStripProgressBar1.Value = 0;
                    toolStripStatusLabel1.Text = "Ready.";
                }
            }
        }

        //============================================
        // Esporta le cose di /proc in CSV
        //============================================

        private void button5_Click(object sender, EventArgs e)
        {
            try
            {
                if (procResults == null || procResults.Count == 0)
                {
                    MessageBox.Show("No /proc reconstruction was done so far.",
                        "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (var fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Select destination folder for CSV export";
                    fbd.ShowNewFolderButton = true;

                    if (fbd.ShowDialog() != DialogResult.OK)
                        return;

                    string destRoot = fbd.SelectedPath;
                    string procDir = Path.Combine(destRoot, "proc");
                    Directory.CreateDirectory(procDir);

                    // ============================================================
                    //  PS.CSV — elenco processi
                    // ============================================================
                    var psRows = procResults.Select(e => new string[]
                    {
                e.PID.ToString(),
                e.Comm,
                e.CmdLine
                    });

                    WriteCsvFile(Path.Combine(procDir, "process_list.csv"),
                        new[] { "PID", "Comm", "CmdLine" },
                        psRows);



                    // ============================================================
                    //  FDS.CSV — file descriptors (con correlazioni)
                    // ============================================================
                    var fdRows = procResults
                        .Where(e => e.FileDescriptors != null && e.FileDescriptors.Count > 0)
                        .SelectMany(e => e.FileDescriptors.Select(fd =>
                        {
                            FileDescriptorInfo? match = fdLookupResults
                                .FirstOrDefault(info => info.FD == fd.FDNumber &&
                                                        info.Handler.Equals(fd.Target, StringComparison.OrdinalIgnoreCase));

                            return new string[]
                            {
                        e.PID.ToString(),
                        e.Comm,
                        fd.FDNumber.ToString(),
                        fd.Target,
                        match?.CorrelationInode ?? string.Empty,
                        match?.CorrelationValue ?? string.Empty,
                        match?.LegitimateUse ?? string.Empty,
                        match?.MaliciousUse ?? string.Empty
                            };
                        }));

                    WriteCsvFile(Path.Combine(procDir, "file_descriptors.csv"),
                        new[]
                        {
                    "PID",
                    "Comm",
                    "FD",
                    "Handler",
                    "Correlation Inode",
                    "Correlation Value",
                    "Legitimate Use",
                    "Malicious Use (TIP!)"
                        },
                        fdRows);

                    // ============================================================
                    // UNIX_SOCKETS.CSV — socket UNIX locali
                    // ============================================================
                    var unixRows = procResults
                       .Where(e => e.UnixSockets != null && e.UnixSockets.Count > 0)
                       .SelectMany(e => e.UnixSockets.Select(u => new string[]
                          {
                            e.PID.ToString(),
                            e.Comm,
                            u.Inode,
                            GetUnixSocketType(int.TryParse(u.Type, out var t) ? t : -1),
                            GetUnixSocketState(int.TryParse(u.State, out var s) ? s : -1),
                            u.Path
                           }));

                    WriteCsvFile(Path.Combine(procDir, "unix_sockets.csv"),
                        new[] { "PID", "Comm", "Inode", "Type", "State", "Path" },
                        unixRows);

                    // ============================================================
                    //  ENVARS.CSV — variabili d’ambiente
                    // ============================================================
                    var envRows = procResults
                        .Where(e => e.Environment != null && e.Environment.Count > 0)
                        .SelectMany(e => e.Environment.Select(kv => new string[]
                        {
                    e.PID.ToString(),
                    e.Comm,
                    kv.Key,
                    kv.Value
                        }));

                    WriteCsvFile(Path.Combine(procDir, "environment_vars.csv"),
                        new[] { "PID", "Comm", "Name", "Value" },
                        envRows);

                    // ============================================================
                    //  ARP.CSV — tabella ARP
                    // ============================================================
                    var arpRows = procResults
                        .Where(e => e.ArpTable != null && e.ArpTable.Count > 0)
                        .SelectMany(e => e.ArpTable.Select(a => new string[]
                        {
                    e.PID.ToString(),
                    e.Comm,
                    a.IPAddress,
                    a.HWType,
                    a.Flags,
                    a.MACAddress,
                    a.Mask,
                    a.Device
                        }));

                    WriteCsvFile(Path.Combine(procDir, "arp_table.csv"),
                        new[] { "PID", "Comm", "IPAddress", "HWType", "Flags", "MACAddress", "Mask", "Device" },
                        arpRows);

                    // ============================================================
                    //  Notifica completamento
                    // ============================================================
                    MessageBox.Show("Export completed successfully!",
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during CSV export:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        //============================================
        // Seleziona il percorso del DB sqlite
        //============================================

        private void button9_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Select SQLite database file";
                ofd.Filter = "SQLite Databases (*.sqlite;*.sqlite3;*.db;History)|*.sqlite;*.sqlite3;*.db;History|All files (*.*)|*.*";
                ofd.RestoreDirectory = true;
                ofd.CheckFileExists = true;
                ofd.CheckPathExists = true;

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    textBox6.Text = ofd.FileName;
                    richTextBox6.AppendText($"[+] Database selected: {ofd.FileName}\n");
                }
                else
                {
                    richTextBox6.AppendText("[!] No database selected.\n");
                }
            }
        }

        //=================================================================
        // Seleziona il percorso dei risultati di DB sqlite extractor
        //=================================================================

        private void button10_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select the folder where CSV results will be saved";
                fbd.RootFolder = Environment.SpecialFolder.Desktop;
                fbd.ShowNewFolderButton = true;

                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    textBox7.Text = fbd.SelectedPath;
                    richTextBox6.AppendText($"[+] Output folder selected: {fbd.SelectedPath}\n");
                }
                else
                {
                    richTextBox6.AppendText("[!] No output folder selected.\n");
                }
            }
        }

        //=================================================================
        // LANCIA QUERY SQL
        //=================================================================
        private async void button12_Click(object sender, EventArgs e)
        {
            try
            {
                string dbType = comboBox2.SelectedItem?.ToString() ?? string.Empty;
                string dbPath = textBox6.Text.Trim();
                string outputBasePath = textBox7.Text.Trim();

                //  Validazione input
                if (string.IsNullOrEmpty(dbType))
                {
                    MessageBox.Show("Please select a database type first.", "Missing selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
                {
                    MessageBox.Show("Please select a valid database file.", "Missing database", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (string.IsNullOrEmpty(outputBasePath) || !Directory.Exists(outputBasePath))
                {
                    MessageBox.Show("Please select a valid output folder.", "Missing output path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                //  Inizializzazione interfaccia
                toolStripProgressBar1.Value = 0;
                toolStripStatusLabel1.Text = $"Extracting from {dbType}...";
                richTextBox6.AppendText($"[+] Starting extraction for {dbType}...\n");

                //  Switch per tipo database
                switch (dbType)
                {

                    case "RPM DNF Package Database":
                        await ExtractDnfHistoryAsync(dbPath, outputBasePath);
                        break;


                    case "RPM YUM Package Database":
                        await ExtractYumHistoryAsync(dbPath, outputBasePath);
                        break;

                    case "RPM DB History":
                        await ExtractRpmDbHistoryAsync(dbPath, outputBasePath);
                        break;

                    default:
                        MessageBox.Show($"No extraction logic defined yet for '{dbType}'.", "Not implemented", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        toolStripStatusLabel1.Text = "Ready";
                        toolStripProgressBar1.Value = 0;
                        return;
                }

                // Stato finale
                toolStripProgressBar1.Value = 0; // reset progress bar
                toolStripStatusLabel1.Text = "Ready";
                richTextBox6.AppendText("[OK] Extraction completed successfully.\n\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error during extraction:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                richTextBox6.AppendText("[!] Extraction failed: " + ex.Message + "\n");
                toolStripStatusLabel1.Text = "Error during extraction";
                toolStripProgressBar1.Value = 0; // reset anche in caso di errore
            }
        }

        //=================================================================
        // Pulsante per /etc/group lookup
        //=================================================================

        private void button15_Click(object sender, EventArgs e)
        {
            if (listView8.Items.Count == 0)
            {
                MessageBox.Show("No /etc/passwd parsing was done, no elements in table.",
                    "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Seleziona file /etc/group o group-
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Select /etc/group file";
                ofd.Filter = "Group file|group*|All files|*.*";
                ofd.InitialDirectory = "/etc";

                if (ofd.ShowDialog() != DialogResult.OK)
                    return;

                string groupFile = ofd.FileName;

                // Crea dizionario GID → GroupName
                Dictionary<string, string> gidToGroup = new Dictionary<string, string>();

                try
                {
                    foreach (string line in File.ReadAllLines(groupFile))
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                            continue;

                        // Formato: group_name:password:GID:members
                        string[] f = line.Split(':');
                        if (f.Length < 3)
                            continue;

                        string groupName = f[0].Trim();
                        string gid = f[2].Trim();

                        if (!gidToGroup.ContainsKey(gid))
                            gidToGroup.Add(gid, groupName);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error reading group file:\n" + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                //  Lookup e sostituzione in listView8
                listView8.BeginUpdate();
                try
                {
                    foreach (ListViewItem item in listView8.Items)
                    {
                        if (item.SubItems.Count < 3)
                            continue;

                        string gid = item.SubItems[2].Text.Trim();
                        if (string.IsNullOrEmpty(gid))
                            continue;

                        if (gidToGroup.TryGetValue(gid, out string groupName))
                        {
                            item.SubItems[3].Text = groupName;
                        }
                    }
                }
                finally
                {
                    listView8.EndUpdate();
                }
            }
        }

        //=================================================================
        // Starts the crawler
        //=================================================================
        private async void button13_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select the root directory to crawl";
                fbd.RootFolder = Environment.SpecialFolder.Desktop;
                fbd.ShowNewFolderButton = false;

                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    textBox8.Text = fbd.SelectedPath;
                    fileList.Clear();
                    userList.Clear();
                    searchedFiles.Clear();
                    listView11.Items.Clear();
                    listView12.Items.Clear();
                    listView13.Items.Clear();
                    listView14.Items.Clear();
                    listView15.Items.Clear();
                    richTextBox2.Clear();
                    toolStripProgressBar1.Value = 0;
                    toolStripStatusLabel1.Text = "Crawling started...";

                    try
                    {
                        _printedFiles.Clear();
                        PrintBigBanner();
                        await CrawlAsync(fbd.SelectedPath);
                        PrintEnumeratedUsers(fileList, fbd.SelectedPath);
                        await RunArtifactChecksAsync(fbd.SelectedPath);
                        await CheckMissingFilesAsync();
                        toolStripProgressBar1.Value = 100;
                        toolStripStatusLabel1.Text = "Crawling completed.";
                        await Task.Delay(500);
                        toolStripProgressBar1.Value = 0;  //  reset finale

                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error during crawl: " + ex.Message, "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        toolStripStatusLabel1.Text = "Error during crawl.";
                    }
                }
            }
        }



        //=================================================================
        // Apre la tree view
        //=================================================================

        private void button14_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select a folder to display its structure";
                fbd.ShowNewFolderButton = false;

                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    string selectedPath = fbd.SelectedPath;
                    textBox9.Text = selectedPath;

                    treeView1.BeginUpdate();
                    treeView1.Nodes.Clear();

                    // Crea nodo radice con conteggio
                    TreeNode rootNode = new TreeNode(GetFolderDisplayName(selectedPath))
                    {
                        Tag = selectedPath,
                        ImageKey = "folderico",
                        SelectedImageKey = "openfolderico"
                    };

                    treeView1.Nodes.Add(rootNode);
                    LoadSubNodes(rootNode);

                    treeView1.EndUpdate();
                    rootNode.Expand();
                }
            }
        }

        private void contextMenuStrip1_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {

        }

        //======================================================
        // Helpers per stampare gli Unix sockets
        //======================================================

        private string GetUnixSocketType(int type)
        {
            return type switch
            {
                1 => "STREAM",
                2 => "DGRAM",
                3 => "RAW",
                4 => "RDM",
                5 => "SEQPACKET",
                _ => $"UNKNOWN({type})"
            };
        }

        //======================================================
        // Helpers per stampare gli Unix sockets
        //======================================================

        private string GetUnixSocketState(int state)
        {
            return state switch
            {
                1 => "FREE",
                2 => "LISTENING",
                3 => "CONNECTING",
                4 => "CONNECTED",
                5 => "DISCONNECTING",
                _ => $"UNKNOWN({state})"
            };
        }

        //=================================================================
        // Pulsante per /proc/module
        //=================================================================
        private async void button18_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Select /proc/modules file";
                ofd.Filter = "Modules file|modules*|All Files|*.*";
                ofd.InitialDirectory = "/proc";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    textBox12.Text = ofd.FileName;
                    await ParseProcModulesAsync(ofd.FileName);
                }
            }
        }

        //=================================
        // ESPORTA TUTTO
        //=================================

        private void exportAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (contextMenuStrip2.SourceControl is not System.Windows.Forms.ListView lv || lv.Items.Count == 0)
                return;

            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select destination folder for CSV export";
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    string filePath = Path.Combine(fbd.SelectedPath, lv.Tag + "Exported_all.csv");
                    ExportListViewToCsv(lv, filePath, selectedOnly: false);
                    MessageBox.Show($"All rows exported:\n{filePath}", "Export Completed",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }


        //=================================
        // Scintilla UNDO
        //=================================
        private void undoCtrlUToolStripMenuItem_Click(object sender, EventArgs e)
        {
            scintilla1.Undo();
        }

        //=================================
        // Scintilla from Unix DateTime
        //=================================

        private void fromUnixDateTToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string sel = scintilla1.SelectedText;

            if (string.IsNullOrWhiteSpace(sel))
            {
                MessageBox.Show("No text selected.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string converted = ConvertUnixTimestamp(sel);

            if (string.IsNullOrWhiteSpace(converted))
            {
                MessageBox.Show("Not a Unix DateTime format.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            scintilla1.ReplaceSelection(converted);
        }

        //=================================
        // Scintilla Linux Permissions
        //=================================

        private void linuxPermissionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string sel = scintilla1.SelectedText;

            if (string.IsNullOrWhiteSpace(sel))
            {
                MessageBox.Show("No text selected.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string decoded = ConvertLinuxPermission(sel);

            if (string.IsNullOrWhiteSpace(decoded))
            {
                MessageBox.Show("The selected text is not a Linux permission.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Esempio: "700 (User: rwx Group: --- World: ---)"
            string final = $"{sel} ({decoded})";

            scintilla1.ReplaceSelection(final);
        }


        //=================================
        // Scintilla Linux Permissions (4 digits)
        //=================================

        private void linuxPermissionsExplainedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string sel = scintilla1.SelectedText;

            if (string.IsNullOrWhiteSpace(sel))
            {
                MessageBox.Show("No text selected.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string decoded = ConvertLinuxPermissionFourDigits(sel);

            if (string.IsNullOrWhiteSpace(decoded))
            {
                MessageBox.Show("The selected text is not a 4-digit Linux permission.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string final = $"{sel} ({decoded})";

            scintilla1.ReplaceSelection(final);
        }
        //=====================================================================
        private void uptimeIdleSecondsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string sel = scintilla1.SelectedText;

            if (string.IsNullOrWhiteSpace(sel))
            {
                MessageBox.Show("No text selected.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string decoded = ConvertUptimeToReadable(sel);

            if (string.IsNullOrWhiteSpace(decoded))
            {
                MessageBox.Show("Not a valid uptime/idle time",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Sostituisce il valore selezionato con la forma leggibile
            scintilla1.ReplaceSelection(decoded);
        }

        //=================================
        // RPM parser
        //=================================
        private async void button19_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select an RPM package",
                Filter = "RPM files (*.rpm)|*.rpm|All files (*.*)|*.*"
            };

            if (ofd.ShowDialog() != DialogResult.OK)
                return;

            string path = ofd.FileName;
            textBox13.Text = path;
            richTextBox9.Clear();

            if (!RpmParser.IsRpmFile(path))
            {
                richTextBox9.AppendText("Not a valid RPM file.\n");
                return;
            }

            try
            {
                // PROGRESS BAR
                toolStripProgressBar1.Style = ProgressBarStyle.Marquee;
                toolStripProgressBar1.MarqueeAnimationSpeed = 40;

                // --- PARSING COMPLETO ---
                var result = await RpmParser.GetAllAsync(path);

                //====================================================
                //  METADATA
                //====================================================
                AppendGreenBanner(richTextBox9, "METADATA SECTION");

                AppendColored(richTextBox9, " - Name: ", result.Name, Color.DarkBlue, Color.Black);
                AppendColored(richTextBox9, " - Version: ", result.Version, Color.DarkBlue, Color.Black);
                AppendColored(richTextBox9, " - Release: ", result.Release, Color.DarkBlue, Color.Black);
                AppendColored(richTextBox9, " - Architecture: ", result.Arch, Color.DarkBlue, Color.Black);

                AppendColored(richTextBox9, " - Summary: ", result.Summary, Color.DarkBlue, Color.Black);

                string descOneLine = result.Description?.Replace("\r", " ").Replace("\n", " ");
                AppendColored(richTextBox9, " - Description: ", descOneLine, Color.DarkBlue, Color.Green);

                AppendColored(richTextBox9, " - License: ", result.License, Color.DarkBlue, Color.Black);
                AppendColored(richTextBox9, " - BuildTime: ", result.BuildTime, Color.DarkBlue, Color.Black);
                AppendColored(richTextBox9, " - Packager: ", result.Packager, Color.DarkBlue, Color.Black);
                AppendColored(richTextBox9, " - URL: ", result.URL, Color.DarkBlue, Color.Black);
                AppendColored(richTextBox9, " - BuildHost: ", result.BuildHost, Color.DarkBlue, Color.Black);
                AppendColored(richTextBox9, " - Source: ", result.Source, Color.DarkBlue, Color.Black);
                AppendColored(richTextBox9, " - SourceRPM: ", result.SourceRPM, Color.DarkBlue, Color.Black);

                //====================================================
                //  FILE LIST
                //====================================================
                AppendGreenBanner(richTextBox9, "FILE LIST");

                int index = 1;
                foreach (var f in result.FileList)
                {
                    AppendColored(richTextBox9, $" {index}) File : ",
                        f, Color.Crimson, Color.DarkViolet);
                    index++;
                }

                //====================================================
                //  DEPENDENCIES
                //====================================================
                AppendGreenBanner(richTextBox9, "DEPENDENCIES");

                foreach (var d in result.Requires)
                    AppendColored(richTextBox9, "Requires: ",
                        $"{d.Name} {d.Flags} {d.Version}", Color.DarkBlue, Color.DarkOrange);

                foreach (var d in result.Provides)
                    AppendColored(richTextBox9, "Provides: ",
                        $"{d.Name} {d.Flags} {d.Version}", Color.DarkSlateBlue, Color.DarkGreen);

                foreach (var d in result.Conflicts)
                    AppendColored(richTextBox9, "Conflicts: ",
                        $"{d.Name} {d.Flags} {d.Version}", Color.DarkRed, Color.OrangeRed);

                foreach (var d in result.Obsoletes)
                    AppendColored(richTextBox9, "Obsoletes: ",
                        $"{d.Name} {d.Flags} {d.Version}", Color.DarkGoldenrod, Color.PaleGoldenrod);

                //====================================================
                //  SCRIPTS
                //====================================================
                AppendGreenBanner(richTextBox9, "SCRIPTS");

                AppendColored(richTextBox9, "\n PRE INSTALL SCRIPT: ", result.PreIn, Color.DarkBlue, Color.DarkOliveGreen);
                AppendColored(richTextBox9, " PRE INSTALL INTERPRETER: ", result.PreInProg, Color.DarkBlue, Color.DarkOliveGreen);

                AppendColored(richTextBox9, "\n POST INSTALL SCRIPT: ", result.PostIn, Color.DarkBlue, Color.DarkOliveGreen);
                AppendColored(richTextBox9, " POST INSTALL INTERPRETER: ", result.PostInProg, Color.DarkBlue, Color.DarkOliveGreen);

                AppendColored(richTextBox9, "\n PRE UNINSTALL SCRIPT: ", result.PreUn, Color.DarkBlue, Color.DarkOliveGreen);
                AppendColored(richTextBox9, " PRE UNINSTALL INTERPRETER: ", result.PreUnProg, Color.DarkBlue, Color.DarkOliveGreen);

                AppendColored(richTextBox9, "\n POST UNINSTALL SCRIPT: ", result.PostUn, Color.DarkBlue, Color.DarkOliveGreen);
                AppendColored(richTextBox9, " POST UNINSTALL INTERPRETER: ", result.PostUnProg, Color.DarkBlue, Color.DarkOliveGreen);

                //====================================================
                //  HASHES
                //====================================================
                AppendGreenBanner(richTextBox9, "MD5/SHA HASHES");

                for (int i = 0; i < (result.FileDigests?.Length ?? 0); i++)
                {
                    string digest = result.FileDigests[i];
                    string file = (i < result.FileList.Count) ? result.FileList[i] : "(unknown file)";

                    string algoName = result.FileDigestAlgorithm switch
                    {
                        0 => "MD5",
                        1 => "SHA1",
                        2 => "SHA256",
                        _ => "DIGEST"
                    };

                    AppendColored(
                        richTextBox9,
                        $"{algoName}: ",
                        $"{digest}   →   {file}",
                        Color.DarkBlue,
                        Color.Red
                    );
                }
            }
            catch (Exception ex)
            {
                richTextBox9.AppendText("ERROR: " + ex.Message);
            }
            finally
            {
                // RESET PROGRESS BAR
                toolStripProgressBar1.Style = ProgressBarStyle.Blocks;
                toolStripProgressBar1.MarqueeAnimationSpeed = 0;
                toolStripProgressBar1.Value = 0;
            }
        }

        //====================================================
        //  HEXBOX: Copy Text
        //====================================================

        private void copyTextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyHexBoxText();
        }

        //====================================================
        //  HEXBOX: Copy Bytes
        //====================================================

        private void copyBytesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyHexBoxBytes();
        }

        //====================================================
        //  HEXBOX: Dump as Binary
        //====================================================
        private void dumpAsBinaryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DumpHexBoxSelectionAsBinary();
        }

        //====================================================
        //  ContextMenu6: Select All
        //====================================================

        private void selectAllToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            var rtb = GetSenderRichTextBox(sender);
            if (rtb != null)
            {
                rtb.SelectAll();
                rtb.Focus();
            }
        }

        //====================================================
        //  ContextMenu6: Copy
        //====================================================
        private void copyToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            var rtb = GetSenderRichTextBox(sender);
            if (rtb != null && !string.IsNullOrEmpty(rtb.SelectedText))
                Clipboard.SetText(rtb.SelectedText);
        }

        //====================================================
        //  ContextMenu6: Clear
        //====================================================

        private void clearToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var rtb = GetSenderRichTextBox(sender);
            if (rtb != null)
                rtb.Clear();
        }

        //====================================================
        //  ContextMenu6: Export
        //====================================================

        private void exportToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            var rtb = GetSenderRichTextBox(sender);
            if (rtb == null)
                return;

            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Title = "Export RichTextBox content";
                dlg.Filter = "Text files|*.txt|All files|*.*";
                dlg.FileName = "export.txt";

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        File.WriteAllText(dlg.FileName, rtb.Text);
                        MessageBox.Show("Export completed!", "Success",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error exporting:\n" + ex.Message,
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        // ===================================================================
        //  Lookup user per proc
        // ===================================================================

        private async void button16_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select /etc/passwd file",
                Filter = "passwd files|passwd*;*passwd*|All files (*.*)|*.*"
            };

            if (ofd.ShowDialog() != DialogResult.OK)
                return;

            string path = ofd.FileName;

            // ============================
            // 1) Parsing /etc/passwd
            // ============================
            var uidToUser = new Dictionary<string, string>();

            try
            {
                var lines = await File.ReadAllLinesAsync(path);

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (!line.Contains(":")) continue;

                    // formato standard:
                    // username : x : uid : gid : comment : home : shell
                    var parts = line.Split(':');
                    if (parts.Length < 3) continue;

                    string userName = parts[0].Trim();
                    string uid = parts[2].Trim();

                    if (!string.IsNullOrWhiteSpace(userName) &&
                        !string.IsNullOrWhiteSpace(uid))
                    {
                        uidToUser[uid] = userName;
                    }
                }
            }
            catch
            {
                return;
            }

            // ============================
            // 2) Sostituzione nella listView9
            // ============================
            listView9.BeginUpdate();

            try
            {
                foreach (ListViewItem item in listView9.Items)
                {
                    if (item.SubItems.Count < 2)
                        continue;

                    string cellValue = item.SubItems[1].Text.Trim();

                    if (string.IsNullOrWhiteSpace(cellValue))
                        continue;

                    // se il valore è un UID presente nella mappa => sostituisci con username
                    if (uidToUser.TryGetValue(cellValue, out string userName))
                    {
                        item.SubItems[1].Text = userName;
                    }
                }
            }
            finally
            {
                listView9.EndUpdate();
            }
        }


        // ===================================================================

        private async void button17_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select a DEB package",
                Filter = "DEB files (*.deb)|*.deb|All files (*.*)|*.*"
            };

            if (ofd.ShowDialog() != DialogResult.OK)
                return;

            string path = ofd.FileName;
            textBox10.Text = path;
            richTextBox10.Clear();

            if (!DebParser.IsDebFile(path))
            {
                richTextBox10.AppendText("Not a valid DEB package.\n");
                return;
            }

            try
            {
                toolStripProgressBar1.Style = ProgressBarStyle.Marquee;
                toolStripProgressBar1.MarqueeAnimationSpeed = 40;

                var (debianBinary, controlFiles, dataTree) = await DebParser.GetAllAsync(path);

                // 1) Sezione "debian-binary"
                AppendDebianBinarySection(richTextBox10, debianBinary);

                // 2) Sezione CONTROL
                AppendControlSection(richTextBox10, controlFiles);

                // 3) Sezione DATA (tree view, no contenuto file)
                AppendDataSection(richTextBox10, dataTree);
            }
            catch (Exception ex)
            {
                richTextBox10.SelectionColor = Color.Red;
                richTextBox10.SelectionFont = richTextBox10.Font;
                richTextBox10.AppendText($"Error while parsing DEB file: {ex.Message}\n");
            }
            finally
            {
                toolStripProgressBar1.Style = ProgressBarStyle.Blocks;
                toolStripProgressBar1.MarqueeAnimationSpeed = 0;
                toolStripProgressBar1.Value = 0;
            }
        }

        // ===================================================================
    }   // Fine MainClass: Form
    // ===================================================================
} // fine del Namespace
