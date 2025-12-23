using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LAP
{
    public partial class MainClass : Form
    {
        

        // ===================================================================

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                // Segna il link come visitato 
                linkLabel1.LinkVisited = true;

                // Apri l'URL nel browser predefinito
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = linkLabel1.Text, // oppure una stringa fissa
                    UseShellExecute = true      // necessario da .NET 5 in poi
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open link:\n{ex.Message}",
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ===================================================================

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                // Segna il link come visitato 
                linkLabel2.LinkVisited = true;

                // Apri l'URL nel browser predefinito
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = linkLabel2.Text, // oppure una stringa fissa
                    UseShellExecute = true      // necessario da .NET 5 in poi
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open link:\n{ex.Message}",
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        //========================================================

        private void listView4_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Nessuna selezione → esci
            if (listView4.SelectedItems.Count == 0)
                return;

            // Nome della sezione selezionata
            string sectionName = listView4.SelectedItems[0].SubItems[0].Text;

            // Pulisce la finestra descrittiva
            richTextBox7.Clear();

            // ============================================================
            // SWITCH COMPLETO DESCRIZIONI SEZIONI
            // (Qui mantieni il tuo switch aggiornato, lo lascio come placeholder)
            // ============================================================
            string description = sectionName switch
            {
                // -----------------------
                // Sezioni standard ELF
                // -----------------------
                ".text" => "Contains executable machine code — program instructions.",
                ".data" => "Initialized global and static variables.",
                ".rodata" => "Read-only constants and literals.",
                ".bss" => "Uninitialized global/static storage.",
                ".tbss" => "Thread-local uninitialized memory.",
                ".tdata" => "Thread-local initialized memory.",
                ".symtab" => "Static symbol table.",
                ".dynsym" => "Dynamic symbol table used by the dynamic loader.",
                ".strtab" => "String table for symbol and section names.",
                ".dynamic" => "Dynamic linking information including DT_NEEDED.",
                ".got" or ".got.plt" => "Global Offset Table (runtime relocation resolution).",
                ".plt" => "Procedure Linkage Table stubs.",
                ".plt.got" => "Optimized PLT/GOT hybrid section.",
                ".comment" => "Compiler metadata.",
                ".note" or ".note.ABI-tag" => "Metadata notes for the ELF loader.",
                ".init_array" => "Constructors executed before main().",
                ".fini_array" => "Destructors executed on exit.",
                ".eh_frame" => "Exception handling frame data.",
                ".eh_frame_hdr" => "Header for .eh_frame.",
                ".init" => "Initialization code.",
                ".fini" => "Finalization code.",
                ".ctors" => "Legacy constructors.",
                ".dtors" => "Legacy destructors.",
                ".data.rel.ro" => "Relocation-applied read-only data.",

                // -----------------------
                // GNU / Linker internals
                // -----------------------
                ".interp" => "Runtime path to the dynamic loader.",
                ".gnu.hash" => "GNU hash table for fast symbol lookup.",
                ".dynstr" => "Dynamic string table for .dynsym.",
                ".gnu.version" => "Symbol version table.",
                ".gnu.version_r" => "Required version dependencies.",
                ".rel.dyn" or ".rela.dyn" => "Dynamic relocation entries.",
                ".rel.plt" or ".rela.plt" => "PLT relocation entries.",
                ".gnu_debugaltlink" => "Alternate debug info reference.",
                ".gnu_debuglink" => "External debug symbols reference.",
                ".shstrtab" => "Section header string table.",
                ".note.gnu.build-id" => "Unique build ID generated by the linker.",
                ".note.gnu.property" => "CPU/ABI property notes.",
                ".note.package" => "Build/package metadata.",
                ".hash" => "Legacy SysV hash table.",
                ".gnu.lto_" => "GCC Link Time Optimization metadata.",

                // -----------------------
                // DWARF Debug (standard)
                // -----------------------
                ".debug_info" => "DWARF debug info — types, variables, metadata.",
                ".debug_abbrev" => "DWARF abbreviation table.",
                ".debug_line" => "Line number to instruction mapping.",
                ".debug_loc" => "Location lists for variables.",
                ".debug_str" => "DWARF string table.",
                ".debug_ranges" => "Non-contiguous code ranges.",
                ".debug_aranges" => "Address range table.",
                ".debug_macinfo" => "Macro information.",
                ".debug_frame" => "Unwind info for stack frames.",
                ".stab" => "Legacy debug symbol table.",
                ".stabstr" => "Legacy debug string table.",
                ".gdb_index" => "GDB accelerator index.",

                // -----------------------
                // DWARF Debug (Compressed)
                // -----------------------
                ".zdebug_info" => "Compressed DWARF .debug_info.",
                ".zdebug_abbrev" => "Compressed DWARF abbreviations.",
                ".zdebug_line" => "Compressed line table.",
                ".zdebug_loc" => "Compressed location lists.",
                ".zdebug_ranges" => "Compressed DWARF range data.",
                ".zdebug_pubnames" => "Compressed public names.",
                ".zdebug_pubtypes" => "Compressed public types.",
                ".zdebug_frame" => "Compressed unwind frame info.",

                // -----------------------
                // GO Language
                // -----------------------
                ".gosymtab" => "Go symbol table.",
                ".gopclntab" => "Go PC→line mapping table.",
                ".go.buildinfo" => "Go build metadata.",
                ".note.go.buildid" => "Go compiler build ID.",
                ".noptrdata" => "Go non-pointer data (GC optimized).",
                ".noptrbss" => "Go BSS without pointers.",
                ".itablink" => "Go interface method tables.",

                // -----------------------
                // Rust
                // -----------------------
                ".rustc" => "Rust compiler metadata.",
                ".comment.rustc" => "Rust toolchain info.",

                // -----------------------
                // Swift
                // -----------------------
                ".swift5_typeref" => "Swift type reference metadata.",
                ".swift5_capture" => "Swift closure data.",
                ".swift5_reflstr" => "Swift reflection strings.",
                ".swift5_builtin" => "Swift builtin metadata.",
                ".swift5_assocty" => "Swift associated type mappings.",
                ".swift5_fieldmd" => "Swift field descriptors.",

                // -----------------------
                // D Language
                // -----------------------
                ".dmoduleinfo" => "D language module metadata.",
                ".minfo" => "D runtime metadata.",

                // -----------------------
                // Glibc internals
                // -----------------------
                "__libc_freeres_fn" => "Glibc memory cleanup routines.",
                "__libc_subfreeres" => "Secondary cleanup routines.",
                "__libc_IO_vtables" => "Glibc I/O vtables.",
                "__libc_atexit" => "Exit handler tables.",
                "__libc_freeres_ptrs" => "Pointers freed at exit.",

                // -----------------------
                // SystemTap / DTrace
                // -----------------------
                ".stapsdt.base" => "SystemTap/DTrace probe base.",
                ".note.stapsdt" => "DTrace/SystemTap static probes.",

                // -----------------------
                // Malware / Suspicious sections
                // (i nomi veri sono gestiti da IsSuspiciousSectionName)
                // -----------------------
                ".upx" or ".upx0" or ".upx1" or ".upx2"
                    => "UPX packer section — likely obfuscated.",
                ".fsg" or ".fsg1" or ".fsg2"
                    => "FSG packed data — suspicious.",
                ".aspack" or ".aspack2"
                    => "Aspack packer — suspicious.",
                ".mpress" => "MPress packer section.",
                ".armadillo" => "Armadillo packer.",
                ".vmprotect" or ".vmp0" or ".vmp1"
                    => "VMProtect obfuscated code.",
                ".enc" or ".encrypted" or ".crypt" or ".crypted"
                    => "Encrypted payload — highly suspicious.",
                ".payload" => "Explicit malware payload container.",
                ".shell" or ".shellcode"
                    => "Embedded shellcode — dangerous.",
                ".stub" => "Packer or loader stub.",
                ".mal" => "Malware-specific section.",
                ".shc" or ".shcode" => "Shellcode container.",
                ".bin" or ".bincode" or ".bintext"
                    => "Raw binary blob section.",
                ".sec" or ".sec1" or ".sec2" or ".sec3" or ".sec_x"
                    => "Custom loader section — potentially malicious.",
                ".hook" => "Hook/injection code — suspicious.",

                // -----------------------
                // DEFAULT
                // -----------------------
                _ => "No specific description available for this section."
            };

            // ============================================================
            // STAMPA INTESTAZIONE
            // ============================================================
            richTextBox7.SelectionFont = new Font("Consolas", 11, FontStyle.Bold);
            richTextBox7.SelectionColor = Color.Black;
            richTextBox7.AppendText($"[Section]: {sectionName}\n\n");

            // ============================================================
            // STAMPA INFO CON COLORAZIONE SOSPETTA
            // ============================================================
            richTextBox7.SelectionFont = new Font("Consolas", 8, FontStyle.Regular);

            if (IsSuspiciousSectionName(sectionName))
            {
                richTextBox7.SelectionColor = Color.Red;
            }
            else
            {
                richTextBox7.SelectionColor = Color.Black;
            }

            richTextBox7.AppendText($"[ INFO ]: {description}");
        }


        //=================================================
        //  EVENT HANDLER: mostra i dettagli completi del processo selezionato
        //=================================================
        private async void listView9_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (!e.IsSelected || string.IsNullOrEmpty(procPath))
                return;

            string pid = e.Item.SubItems[0].Text;
            string fdPath = Path.Combine(procPath, pid, "fd");
            string netPath = Path.Combine(procPath, pid, "net");
            string arpPath = Path.Combine(netPath, "arp");
            string envPath = Path.Combine(procPath, pid, "environ");
            string mapsPath = Path.Combine(procPath, pid, "maps");
            string unixPath = Path.Combine(procPath, pid, "net", "unix");

            richTextBox4.Clear();
            richTextBox8.Clear();
            listView5.Items.Clear();

            // intestazione principale
            richTextBox4.AppendText("===================================\n");
            richTextBox4.AppendText($"File Descriptors for PID {pid}\n");
            richTextBox4.AppendText("===================================\n\n");

            toolStripStatusLabel1.Text = $"Reading data for PID {pid}...";

            try
            {
                // 🔹 1. File Descriptors
                var fdList = await ReadFileDescriptorsAsync(fdPath);
                DisplayFileDescriptors(fdList);

                // 🔹 2. Network Activity (TCP/UDP)
                var conns = await ReadNetworkInfoAsync(netPath);
                DisplayNetworkActivity(conns, int.Parse(pid));

                // 🔹 3. Listening Ports
             //   var ports = await ReadListeningPortsAsync(netPath);
              //  DisplayListeningPorts(ports, int.Parse(pid));

                // 🔹 4. ARP Table
                if (File.Exists(arpPath))
                {
                    var arpEntries = await ReadArpTableAsync(arpPath);
                    DisplayArpTable(arpEntries, int.Parse(pid));
                }
                else
                {
                    // richTextBox4.AppendText("\n===================================\n");
                    //  richTextBox4.AppendText("ARP Table\n");
                    //   richTextBox4.AppendText("===================================\n\n");
                    AppendBanner("ARP Table", Color.DarkRed);
                    richTextBox4.AppendText("(no ARP entries found)\n");
                }

                // 🔹 5. Environment Variables
                if (File.Exists(envPath))
                {
                    var envList = await ReadEnvironmentAsync(envPath);
                    DisplayEnvironment(envList, int.Parse(pid));
                }
                else
                {
                    //  richTextBox4.AppendText("\n===================================\n");
                    //   richTextBox4.AppendText("Environment variables\n");
                    //   richTextBox4.AppendText("===================================\n\n");
                    AppendBanner("Environment variables", Color.DarkOrange);
                    richTextBox4.AppendText("(no environment variables found)\n");
                }

                // 🔹 6. Memory Map
                if (File.Exists(mapsPath))
                {
                    var maps = await ReadMemoryMapAsync(mapsPath);
                    DisplayMemoryMap(maps, int.Parse(pid));
                }
                else
                {
                 //   richTextBox8.AppendText("\n===================================\n");
                 //   richTextBox8.AppendText("Memory Map\n");
                 //   richTextBox8.AppendText("===================================\n\n");
                    richTextBox8.AppendText("(no memory maps found)\n");
                }

                // 🔹 7. Unix Sockets
                List<ProcUnixSocket> unixSockets = new();
                if (File.Exists(unixPath))
                {
                    unixSockets = await ReadUnixSocketsAsync(unixPath);
                    DisplayUnixSockets(unixSockets, int.Parse(pid));
                }
                else
                {
                    //  richTextBox4.AppendText("\n===================================\n");
                    //   richTextBox4.AppendText("Unix Sockets\n");
                    //   richTextBox4.AppendText("===================================\n\n");
                    AppendBanner("Unix Sockets", Color.DarkBlue);
                    richTextBox4.AppendText("(no unix sockets found)\n");
                }

                // 🔹 8. Analisi dettagliata FD → listView5
                toolStripStatusLabel1.Text = $"Analyzing file descriptors for PID {pid}...";
                var fdAnalysis = await AnalyzeFileDescriptorsAsync(
                      Path.Combine(procPath, pid),
                      int.Parse(pid),
                      conns,
                      unixSockets
                );

                DisplayFileDescriptorAnalysis(fdAnalysis);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading process data for PID {pid}:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                toolStripStatusLabel1.Text = "Ready.";
            }
        }

        //=================================================
  

        //================================================

    }
}
