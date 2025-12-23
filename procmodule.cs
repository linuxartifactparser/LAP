using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LAP
{
    public partial class MainClass : Form
    {
        public class KernelModule
        {
            public string Name { get; set; }
            public string Size { get; set; }
            public string RefCount { get; set; }
            public string Dependencies { get; set; }
            public string State { get; set; }
            public string Address { get; set; }
            public string FlagsRaw { get; set; }
            public string Notes { get; set; }
        }


        //========================================================
        private async Task ParseProcModulesAsync(string path)
        {
            string[] lines;

            try
            {
                lines = await File.ReadAllLinesAsync(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to read file:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            listView6.BeginUpdate();

            try
            {
                listView6.Items.Clear();

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    // Se non ha almeno 6 campi, NON è in formato /proc/modules
                    if (parts.Length < 6)
                    {
                        MessageBox.Show(
                            "The selected file does not appear to be in /proc/modules format.\n" +
                            "Parsing aborted.",
                            "Invalid format",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning
                        );
                        break;
                    }

                    var module = ParseModuleLine(line);
                    AddModuleToListView(module);
                }
            }
            finally
            {
                // Importantissimo: evita la ListView "rotta"
                listView6.EndUpdate();
            }
        }
        //==========================================================

        private KernelModule ParseModuleLine(string line)
        {
            // Esempio di riga:
            // snd_hda_intel 53248 3 snd_hda_codec,snd_pcm Live 0xffffffffc0a2c000 (OE)
            // oppure senza flags finali

            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var module = new KernelModule
            {
                Name = parts[0],
                Size = parts[1],
                RefCount = parts[2],
                Dependencies = parts[3] == "-" ? "" : parts[3],
                State = parts[4],
                Address = parts[5],
                FlagsRaw = parts.Length >= 7 ? parts[6] : "",
                Notes = DecodeModuleFlags(parts.Length >= 7 ? parts[6] : "")
            };

            return module;
        }

        //==========================================================

        private string DecodeModuleFlags(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "";

            // Tipicamente raw = "(OE)" oppure "(T)" ecc.
            raw = raw.Trim();

            if (!raw.StartsWith("(") || !raw.EndsWith(")"))
                return "Unknown flags";

            string flags = raw.Trim('(', ')');
            List<string> notes = new List<string>();

            foreach (char c in flags)
            {
                switch (c)
                {
                    case 'O':
                        notes.Add("Out-of-tree module (not part of official kernel)");
                        break;
                    case 'P':
                        notes.Add("Proprietary module (non-GPL)");
                        break;
                    case 'E':
                        notes.Add("Exports kernel symbols");
                        break;
                    case 'C':
                        notes.Add("Cryptographically signed module");
                        break;
                    case 'T':
                        notes.Add("Kernel was tainted by this module");
                        break;
                    case 'F':
                        notes.Add("Force-loaded module (insmod -f)");
                        break;
                    default:
                        notes.Add($"Unknown flag '{c}'");
                        break;
                }
            }

            return string.Join("; ", notes);
        }

        //================================================

        private void AddModuleToListView(KernelModule m)
        {
            var item = new ListViewItem(m.Name);
            item.SubItems.Add(m.Size);
            item.SubItems.Add(m.RefCount);
            item.SubItems.Add(m.Dependencies);
            item.SubItems.Add(m.State);
            item.SubItems.Add(m.Address);
            item.SubItems.Add(m.FlagsRaw);
            item.SubItems.Add(m.Notes);

            // --- Evidenziazione dei flag sospetti ---
            if (!string.IsNullOrWhiteSpace(m.FlagsRaw))
            {
                string flags = m.FlagsRaw.Trim('(', ')');

                // Se contiene uno dei flag ad alto rischio
                if (flags.Contains('O') ||    // Out-of-tree (può indicare moduli rootkit)
                    flags.Contains('T') ||    // Kernel tainted
                    flags.Contains('P') ||    // Proprietary (non-GPL)
                    flags.Contains('F'))      // Force-loaded (insmod -f)
                {
                    item.BackColor = Color.LightSalmon;
                    item.ForeColor = Color.Black;
                    item.Font = new Font(item.Font, FontStyle.Bold);
                }
            }

            listView6.Items.Add(item);
        }

        //================================================
    }
}
