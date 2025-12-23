using Be.Windows.Forms;
using ELFSharp;
using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using ELFSharp.ELF.Segments;
using LibObjectFile.Elf;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;


namespace LAP
{
    public partial class MainClass : Form
    {
        // =======================================
        //  ELF Sections
        // =======================================

        private void LoadELFSections()
        {
            string filePath = textBox3.Text;

            if (!File.Exists(filePath))
            {
                MessageBox.Show("File not found:\n" + filePath,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                listView4.BeginUpdate();
                listView4.Items.Clear();

                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var elf = ElfFile.Read(fs);

                foreach (var section in elf.Sections)
                {
                    string name = section.Name.Value.Length > 0 ? section.Name.Value : "(unnamed)";
                    string type = section.Type.ToString();
                    string size = section.Size.ToString();
                    string flags = section.Flags.ToString();

                    var item = new ListViewItem(name);  // [0] Section Name
                    item.SubItems.Add(type);            // [1] Type
                    item.SubItems.Add(size);            // [2] Size
                    item.SubItems.Add(flags);           // [3] Flags
                    item.SubItems.Add(string.Empty);    // [4] NOTES

                    listView4.Items.Add(item);
                }

                toolStripStatusLabel1.Text = $"Loaded {elf.Sections.Count} ELF sections.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while reading ELF sections:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                listView4.EndUpdate();
            }
        }

        // =========================================================

        private void LoadELFSections2()
        {
            string filePath = textBox3.Text;

            if (!File.Exists(filePath))
            {
                MessageBox.Show("File not found:\n" + filePath,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var elf = ElfFile.Read(fs);

                using var sw = new StringWriter();
                elf.Print(sw);

                richTextBox5.Text = sw.ToString();

                toolStripStatusLabel1.Text = $"Loaded {elf.Sections.Count} ELF sections.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while reading ELF sections:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        // =========================================================
        public void AnalyzeElf()
        {
            string path = textBox3.Text;
            var elf = ELFReader.Load(path);

            richTextBox3.Clear();
            richTextBox3.Font = new Font("Consolas", 10);
            richTextBox3.SelectionColor = Color.Black;
            richTextBox3.AppendText("Defined functions:\n");

            // 1. Funzioni definite (.symtab)
            var symtab = elf.Sections
                            .OfType<ISymbolTable>()
                            .FirstOrDefault(s => s.Name == ".symtab");

            if (symtab != null)
            {
                foreach (var symbol in symtab.Entries)
                {
                    if (symbol.Type == SymbolType.Function && symbol.PointedSection != null)
                    {
                        richTextBox3.AppendText($"  - {symbol.Name}\n");
                    }
                }
            }
            else richTextBox3.AppendText("No .symtab section found.\n");

            // 2. Funzioni importate (.dynsym)
            var dynsym = elf.Sections
                            .OfType<ISymbolTable>()
                            .FirstOrDefault(s => s.Name == ".dynsym");

            if (dynsym != null)
            {
                richTextBox3.AppendText("\nImported functions:\n");
                richTextBox3.AppendText("------------------------------\n");

                foreach (var symbol in dynsym.Entries)
                {
                    if (symbol.Type == SymbolType.Function && symbol.PointedSection == null)
                    {
                        string name = symbol.Name.PadRight(40);
                        richTextBox3.SelectionColor = Color.Black;
                        richTextBox3.AppendText($"  {name}");

                        richTextBox3.SelectionColor = Color.Green;
                        richTextBox3.AppendText("( imported )\n");
                    }
                }
            }
            else richTextBox3.AppendText("\nNo .dynsym section found.\n");

            // 3. Dynamic Section
            var dynamicSection = elf.Sections
                                    .OfType<IDynamicSection>()
                                    .FirstOrDefault();

            if (dynamicSection != null)
            {
                richTextBox3.AppendText("\nDynamic Section:\n");
                richTextBox3.AppendText("------------------------------\n");

                var dynStr = elf.Sections
                                .OfType<IStringTable>()
                                .FirstOrDefault(s => s.Name == ".dynstr");

                if (dynStr == null)
                {
                    richTextBox3.AppendText("  (No .dynstr section found)\n");
                }
                else
                {
                    var rows = new List<(string Tag, string Desc, string Value)>();

                    foreach (var entry in dynamicSection.Entries)
                    {
                        string tagName = entry.Tag.ToString();
                        string desc;
                        string val;

                        if (entry.Tag == DynamicTag.Needed ||
                            entry.Tag == DynamicTag.SoName ||
                            entry.Tag == DynamicTag.RPath ||
                            entry.Tag == DynamicTag.RunPath)
                        {
                            desc = entry.Tag switch
                            {
                                DynamicTag.Needed => "Shared library",
                                DynamicTag.SoName => "Library soname",
                                DynamicTag.RPath => "Library rpath",
                                DynamicTag.RunPath => "Library runpath",
                                _ => "String"
                            };

                            if (TryGetDynamicEntryOffset(entry, out int offset))
                                val = dynStr[offset] ?? $"<invalid string @ 0x{offset:X}>";
                            else
                                val = entry.ToString();
                        }
                        else
                        {
                            desc = "Other";
                            val = entry.ToString();
                        }

                        rows.Add((tagName, desc, val));
                    }

                    int col1 = rows.Max(r => r.Tag.Length) + 2;
                    int col2 = rows.Max(r => r.Desc.Length) + 2;

                    var neededRows = rows.Where(r => r.Desc == "Shared library").ToList();
                    var runPathRows = rows.Where(r => r.Desc == "Library runpath").ToList();
                    var otherRows = rows.Where(r => r.Desc == "Other").ToList();

                    // SHARED LIBRARIES
                    if (neededRows.Count > 0)
                    {
                        richTextBox3.AppendText("\nShared Libraries:\n\n");
                        richTextBox3.AppendText("Tag".PadRight(col1) +
                                                "Purpose".PadRight(col2) +
                                                "Value\n");
                        richTextBox3.AppendText("".PadRight(col1 + col2 + 20, '-') + "\n");

                        foreach (var r in neededRows)
                        {
                            string line = r.Tag.PadRight(col1) +
                                          r.Desc.PadRight(col2) +
                                          r.Value;

                            richTextBox3.SelectionColor = Color.Green;
                            richTextBox3.AppendText("  " + line + "\n");
                        }
                    }

                    // RUNPATH
                    if (runPathRows.Count > 0)
                    {
                        richTextBox3.AppendText("\nRunPath:\n\n");
                        richTextBox3.SelectionColor = Color.Black;

                        richTextBox3.AppendText("Tag".PadRight(col1) +
                                                "Purpose".PadRight(col2) +
                                                "Value\n");
                        richTextBox3.AppendText("".PadRight(col1 + col2 + 20, '-') + "\n");

                        foreach (var r in runPathRows)
                        {
                            string line = r.Tag.PadRight(col1) +
                                          r.Desc.PadRight(col2) +
                                          r.Value;

                            richTextBox3.SelectionColor =
                                IsDangerousRunPath(r.Value) ? Color.Red : Color.Blue;

                            richTextBox3.AppendText("  " + line + "\n");
                        }
                    }

                    // OTHER
                    if (otherRows.Count > 0)
                    {
                        richTextBox3.AppendText("\nOther Dynamic Entries:\n\n");
                        richTextBox3.SelectionColor = Color.Black;

                        richTextBox3.AppendText("Tag".PadRight(col1) +
                                                "Purpose".PadRight(col2) +
                                                "Value\n");
                        richTextBox3.AppendText("".PadRight(col1 + col2 + 20, '-') + "\n");

                        foreach (var r in otherRows)
                        {
                            string line = r.Tag.PadRight(col1) +
                                          r.Desc.PadRight(col2) +
                                          r.Value;

                            richTextBox3.SelectionColor = Color.Blue;
                            richTextBox3.AppendText("  " + line + "\n");
                        }
                    }

                    richTextBox3.SelectionColor = Color.Black;
                }
            }
            else richTextBox3.AppendText("\nNo dynamic section was found.\n");

            // 4. PLT/GOT Analysis avanzata
            AnalyzePltGot(elf);
            AnalyzeElfHeuristics();
            richTextBox3.SelectionColor = Color.Black;
        }


        //=====================================================

        private static bool TryGetDynamicEntryOffset(object dynamicEntry, out int offset)
        {
            offset = 0;
            if (dynamicEntry == null)
                return false;

            string s = dynamicEntry.ToString();
            if (string.IsNullOrWhiteSpace(s))
                return false;

            var parts = s.Split(new[] { ' ', '\t' },
                                StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
                return false;

            string hex = parts[1];
            if (hex.StartsWith("0x"))
                hex = hex.Substring(2);

            return int.TryParse(hex, NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture,
                                out offset);
        }


        // =========================================================
        //  RPATH/RUNPATH Security
        // =========================================================
        private bool IsDangerousRunPath(string pathValue)
        {
            if (string.IsNullOrWhiteSpace(pathValue))
                return false;

            string p = pathValue.Trim().ToLowerInvariant();

            string[] unsafePaths =
            {
                "/tmp", "/var/tmp", "/dev/shm", "/run/shm",
                "/home/", "/mnt/", "/media/", "/run/user"
            };

            foreach (var u in unsafePaths)
                if (p.StartsWith(u))
                    return true;

            if (p == "." || p.StartsWith("./"))
                return true;

            if (p.Contains("$origin"))
                return true;

            return false;
        }

        // =========================================================
        // GOT pointer reader
        // =========================================================
        private ulong ReadPointer(byte[] buffer, int offset, int pointerSize)
        {
            if (pointerSize == 8)
            {
                ulong value = 0;
                for (int i = 0; i < 8; i++)
                    value |= (ulong)buffer[offset + i] << (8 * i);
                return value;
            }
            else
            {
                uint value = 0;
                for (int i = 0; i < 4; i++)
                    value |= (uint)buffer[offset + i] << (8 * i);
                return value;
            }
        }

        // =========================================================
        private static bool IsProbablyCodeSection(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            name = name.ToLowerInvariant();

            return name == ".text" ||
                   name == ".init" ||
                   name == ".fini" ||
                   name == ".plt" ||
                   name.StartsWith(".text.") ||
                   name.StartsWith(".init.") ||
                   name.StartsWith(".fini.");
        }


        // =========================================================
        private static string ClassifyTarget(
            ulong target,
            List<(string Name, ulong Start, ulong End)> codeSections)
        {
            if (target == 0)
                return "NULL";

            foreach (var c in codeSections)
                if (target >= c.Start && target < c.End)
                    return c.Name;

            return "external / unknown";
        }

        // =========================================================


        // =========================================================
        // PLT/GOT Analysis with Function Mapping
        // =========================================================
        private void AnalyzePltGot(IELF elf)
        {
            richTextBox11.Clear();
            // Per ora gestiamo solo ELF 64-bit (classe Bit64).
            if (elf.Class != Class.Bit64)
            {
                richTextBox11.AppendText(
                    "\n[PLT/GOT] Analysis currently implemented only for 64-bit ELF files.\n");
                return;
            }

            // Sezioni ProgBits specializzate (64 bit)
            var progBits = elf.GetSections<ProgBitsSection<ulong>>();

            // Cerchiamo prima .got.plt, altrimenti .got
            var gotPlt = progBits.FirstOrDefault(s => s.Name == ".got.plt")
                         ?? progBits.FirstOrDefault(s => s.Name == ".got");

            if (gotPlt == null)
            {
                richTextBox11.AppendText(
                    "\n[PLT/GOT] No .got or .got.plt section found.\n");
                return;
            }

            // Sezione .plt (per distinguere voci "normali" da possibili hook)
            var plt = progBits.FirstOrDefault(s => s.Name == ".plt");

            // Sezioni che consideriamo "codice" (per classificare i target)
            var codeSections = progBits
                .Where(s => s.LoadAddress != 0 && IsProbablyCodeSection(s.Name))
                .Select(s => (s.Name, Start: s.LoadAddress, End: s.LoadAddress + (ulong)s.GetContents().Length))
                .ToList();

            var gotBytes = gotPlt.GetContents();
            int pointerSize = 8; // per ELF 64-bit

            if (gotBytes.Length < pointerSize)
            {
                richTextBox11.AppendText(
                    "\n[PLT/GOT] GOT section too small to analyze.\n");
                return;
            }

            int entriesCount = gotBytes.Length / pointerSize;
            ulong gotBase = gotPlt.LoadAddress;

            richTextBox11.AppendText("\nPLT/GOT analysis (experimental):\n");
            richTextBox11.AppendText("--------------------------------\n\n");
            richTextBox11.AppendText(
                "  Idx   GOT address       Target address    Location             Note\n");
            richTextBox11.AppendText(
                "  ----  --------------    --------------    -------------------  -----------------------------\n");

            ulong pltStart = 0;
            ulong pltEnd = 0;
            if (plt != null)
            {
                pltStart = plt.LoadAddress;
                pltEnd = plt.LoadAddress + (ulong)plt.GetContents().Length;
            }

            for (int i = 0; i < entriesCount; i++)
            {
                int offset = i * pointerSize;
                ulong target = ReadPointer(gotBytes, offset, pointerSize);
                ulong gotEntryAddr = gotBase + (ulong)offset;

                string location = ClassifyTarget(target, codeSections);
                string note;

                bool inPlt = (plt != null) &&
                             target >= pltStart && target < pltEnd;

                bool inBinaryCode = codeSections.Any(c =>
                    target >= c.Start && target < c.End);

                if (target == 0)
                {
                    note = "unresolved / zero";
                }
                else if (inPlt)
                {
                    note = "likely normal PLT entry";
                }
                else if (inBinaryCode)
                {
                    note = "⚠ points into binary code (possible hook)";
                }
                else
                {
                    note = "";
                }

                string line = string.Format(CultureInfo.InvariantCulture,
                    "  {0,3}  0x{1:X12}    0x{2:X12}    {3,-19}  {4}",
                    i,
                    gotEntryAddr,
                    target,
                    location,
                    note
                );

                // Se punta in codice del binario ma non in .plt → sospetto
                if (inBinaryCode && !inPlt && target != 0)
                    richTextBox11.SelectionColor = Color.Red;
                else
                    richTextBox11.SelectionColor = Color.Black;

                richTextBox11.AppendText(line + "\n");
            }

            richTextBox11.SelectionColor = Color.Black;
        }

        // =========================================================

        private void AnalyzeElfHeuristics()
        {
            if (listView4.Items.Count == 0)
                return;

            var sectionNames = new HashSet<string>();
            var anomalies = new List<string>();

            // Per rilevare duplicati (size+flags)
            var duplicateGroups = listView4.Items.Cast<ListViewItem>()
                .GroupBy(i => $"{i.SubItems[2].Text}|{i.SubItems[3].Text}")
                .Where(g => g.Count() > 1)
                .ToList();

            // ============================================================
            // SCANSIONE PRINCIPALE DELLE SEZIONI
            // ============================================================
            foreach (ListViewItem item in listView4.Items)
            {
                string name = item.SubItems[0].Text;
                string type = item.SubItems[1].Text;
                string sizeStr = item.SubItems[2].Text;
                string flags = item.SubItems[3].Text;

                sectionNames.Add(name);

                // Cast sicuro della size
                long.TryParse(sizeStr, out long size);

                // ============================
                // 1) SEZIONI CON NOMI SOSPETTI
                // ============================
                if (IsSuspiciousSectionName(name))
                {
                    AppendNote(item, "Suspicious section name");
                    anomalies.Add($"Suspicious section name: {name}");
                    ColorListViewItem(item, Color.Yellow, Color.Black);
                }

                /*
                // ============================
                // 2) SEZIONE SENZA FLAG MA CON SIZE > 0
                // ============================
                if (flags == "None" && size > 0)
                {
                    AppendNote(item, "Section has size but no flags");
                    anomalies.Add($"{name} has size but no flags");
                    ColorListViewItem(item, Color.Yellow, Color.Black);
                }  */

                // ============================
                // 3) SEZIONE RWX → ALTAMENTE SOSPETTA
                // ============================
                if (flags.Contains("Write") && flags.Contains("Executable"))
                {
                    AppendNote(item, "RWX section (highly suspicious)");
                    anomalies.Add($"{name} has RWX permissions");
                    ColorListViewItem(item, Color.Red, Color.White);
                }

                // ============================
                // 4) .text TROPPO GRANDE
                // ============================
                if (name == ".text" && size > 700000)
                {
                    AppendNote(item, "Text section unusually large");
                    anomalies.Add($".text unusually large ({size} bytes)");
                    ColorListViewItem(item, Color.Yellow, Color.Black);
                }

                // ============================
                // 5) MISMATCH NOMI / PERMESSI
                // ============================
                if (name.Contains("text") && !flags.Contains("Executable"))
                {
                    AppendNote(item, "Text-like name but not executable");
                    anomalies.Add($"{name} text-like but not executable");
                    ColorListViewItem(item, Color.Yellow, Color.Black);
                }

                if (name == ".bss" && flags.Contains("Executable"))
                {
                    AppendNote(item, "BSS marked executable");
                    anomalies.Add(".bss marked executable");
                    ColorListViewItem(item, Color.Red, Color.White);
                }

                // ============================
                // 6) TLS ANOMALIE
                // ============================
                if ((name == ".tdata" || name == ".tbss") && size > 4096)
                {
                    AppendNote(item, "Large TLS region");
                    anomalies.Add($"{name} unusually large → TLS abuse suspected");
                    ColorListViewItem(item, Color.Yellow, Color.Black);
                }

                // ============================
                // 7) UNNAMED SUSPICIOUS SECTIONS
                // ============================
                if (name == "(unnamed)" && size > 0)
                {
                    AppendNote(item, "Unnamed section with content");
                    anomalies.Add("Unnamed non-empty section detected");
                    ColorListViewItem(item, Color.Red, Color.White);
                }
            }

            /*
            // ============================
            // 8) DUPLICATE SECTIONS
            // ============================
            foreach (var group in duplicateGroups)
            {
                foreach (var item in group)
                {
                    AppendNote(item, "Duplicate section layout");
                    ColorListViewItem(item, Color.Gold, Color.Black);
                }

                anomalies.Add($"Duplicate section layout detected: {group.Count()} sections share identical size+flags");
            } */

            // ============================
            // 9) TROPPE SEZIONI (PACKER)
            // ============================
            if (listView4.Items.Count > 40)
            {
                anomalies.Add($"Too many sections ({listView4.Items.Count}) → possible packer");
            }

            // ============================
            // 10) MANCA EH_FRAME + EH_FRAME_HDR
            // ============================
            bool missingUnwind =
                !sectionNames.Contains(".eh_frame") &&
                !sectionNames.Contains(".eh_frame_hdr");

            if (missingUnwind)
                anomalies.Add("Missing both .eh_frame and .eh_frame_hdr → stripped / packed binary");

            // ============================
            // 11) MANCA .note.gnu.build-id
            // ============================
            if (!sectionNames.Contains(".note.gnu.build-id"))
                anomalies.Add(".note.gnu.build-id missing → common in malware or packed binaries");

            // ============================
            // 12) MANCANO SIMBOLI
            // ============================
            if (!sectionNames.Contains(".dynsym") && !sectionNames.Contains(".symtab"))
                anomalies.Add("Both .dynsym and .symtab missing → symbol-stripped or packed");

            if (!sectionNames.Contains(".strtab") && !sectionNames.Contains(".dynstr"))
                anomalies.Add(".strtab and .dynstr BOTH missing → extremely unusual");

            // ============================================================
            // OUTPUT REPORT
            // ============================================================
            if (anomalies.Count > 0)
            {
                richTextBox3.AppendText("\n\n===========================================\n");
                richTextBox3.AppendText("ELF Section Heuristic Report\n");
                richTextBox3.AppendText("===========================================\n");

                foreach (string a in anomalies)
                {
                    richTextBox3.SelectionColor = Color.Red;
                    richTextBox3.AppendText("[!] " + a + "\n");
                }

                richTextBox3.SelectionColor = Color.Black;
                richTextBox3.AppendText("-------------------------------------------\n");
                richTextBox3.AppendText($"Total anomalies: {anomalies.Count}\n");
            }
            else
            {
                richTextBox3.AppendText("\nNo section anomalies detected.\n");
            }
        }



        //==========================================================

        private void AppendNote(ListViewItem item, string note)
        {
            string existing = item.SubItems[4].Text;
            if (string.IsNullOrEmpty(existing))
                item.SubItems[4].Text = note;
            else
                item.SubItems[4].Text = existing + "; " + note;
        }


        // =========================================================

        private bool IsSuspiciousSectionName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return true;

            string lower = name.ToLowerInvariant();

            // -------------------------------------------------------
            // 1. Caratteri non standard (binari, unicode strano, ecc.)
            // -------------------------------------------------------
            if (name.Any(ch => ch < 32 || ch > 126))
                return true;

            // -------------------------------------------------------
            // 2. Nomi troppo lunghi (tipico di polimorfismo malware)
            // -------------------------------------------------------
            if (name.Length > 40)
                return true;

            // -------------------------------------------------------
            // 3. Spazi nel nome → assolutamente anomalo
            // -------------------------------------------------------
            if (name.Contains(" "))
                return true;

            // -------------------------------------------------------
            // 4. Pattern HEX tipo ".ab12cd34" → malware polimorfi/metasploit
            // -------------------------------------------------------
            if (Regex.IsMatch(lower, @"^\.[0-9a-f]{8,}$"))
                return true;

            // -------------------------------------------------------
            // 5. Pattern numerici sospetti: .text1 .data2 .sec99 ecc.
            // -------------------------------------------------------
            if (Regex.IsMatch(lower, @"^\.(text|data|bss|sec|tmp|payload)[0-9]+$"))
                return true;

            // -------------------------------------------------------
            // 6. Lista di nomi MALEVOLI veri (packer, crypter, loader)
            // -------------------------------------------------------
            string[] badNames =
            {
        // Packer
        ".upx", ".upx0", ".upx1", ".upx2",
        ".fsg", ".fsg1", ".fsg2",
        ".aspack", ".aspack2",
        ".mpress",
        ".armadillo",
        ".vmprotect", ".vmp0", ".vmp1",

        // Encryption / payload
        ".enc", ".encrypted", ".crypt", ".crypted",
        ".cipher",
        ".payload",

        // Shellcode
        ".shell", ".shellcode",
        ".shc", ".shcode",

        // Data blob
        ".bin", ".bincode", ".bintext",
        ".data1", ".data2",
        ".stuff", ".junk",

        // Malicious misc
        ".stub",
        ".mal",
        ".mysec", ".mydata",
        ".sec", ".sec1", ".sec2", ".sec3", ".sec_x",

        // Hooking/injection
        ".hook", ".h0", ".h1", ".h2"
    };

            if (badNames.Contains(lower))
                return true;

            // -------------------------------------------------------
            // 7. Pattern generici usati da malware / packer
            // -------------------------------------------------------
            if (Regex.IsMatch(lower, @"^\.(tmp|sec|obj|blob|payload|crypt)[^/]*$"))
                return true;

            // -------------------------------------------------------
            // 8. Parole chiave molto sospette (anche parziali)
            // -------------------------------------------------------
            string[] badSubstrings =
            {
        "evil", "payload", "crypt", "cipher", "shell", "shcode",
        "x.", "junk", "stuff"
    };

            if (badSubstrings.Any(b => lower.Contains(b)))
                return true;

            // -------------------------------------------------------
            // Se nessun caso sospetto → considerata normale
            // -------------------------------------------------------
            return false;
        }


        // =========================================================
        private void ColorListViewItem(ListViewItem item, Color back, Color fore)
        {
            item.BackColor = back;
            item.ForeColor = fore;
            foreach (ListViewItem.ListViewSubItem sub in item.SubItems)
            {
                sub.BackColor = back;
                sub.ForeColor = fore;
            }
        }

        // =========================================================
    }
}
