using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LAP
{
    public partial class MainClass : Form
    {

        // ================================================================
        // 🔹 FUNZIONI DI STAMPA SU UI
        // ================================================================
        public void DisplayProcEntries(List<ProcEntry> entries)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => DisplayProcEntries(entries)));
                return;
            }

            listView9.Items.Clear();

            foreach (var e in entries.OrderBy(x => x.PID))
            {
                var item = new ListViewItem(e.PID.ToString());

                item.SubItems.Add(e.User);     
                item.SubItems.Add(e.Session);   
                item.SubItems.Add(e.Comm);
                item.SubItems.Add(e.CmdLine);
                listView9.Items.Add(item);
            }
        }

        //======================================================
        //    STAMPA I FILE DESCRIPTORS 
        //    NELLA RICHTEXTBOX
        //=====================================================

        public void DisplayFileDescriptors(List<ProcFD> fds)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => DisplayFileDescriptors(fds)));
                return;
            }

            //  richTextBox4.Clear();

            if (fds.Count == 0)
            {
                richTextBox4.AppendText("(no file descriptors)\n");
                return;
            }

            foreach (var fd in fds.OrderBy(f => f.FDNumber))
            {
                richTextBox4.AppendText($"{fd.FDNumber} - {fd.Target}\n");
            }
        }

        //======================================================
        //    BANNER DA APPENDERE
        //    NELLA RICHTEXTBOX
        //=====================================================

        private void AppendBanner(string title, Color color)
        {
            richTextBox4.SelectionStart = richTextBox4.TextLength;
            richTextBox4.SelectionColor = color;

            var start = richTextBox4.TextLength;
            richTextBox4.AppendText("\n===================================\n" + title + "\n" + "===================================\n\n");
            var end = richTextBox4.TextLength;

            richTextBox4.Select(start + 65, title.Length); // solo il titolo centrale
            richTextBox4.SelectionFont = new Font(richTextBox4.Font, FontStyle.Bold);

            // reset
            richTextBox4.SelectionStart = richTextBox4.TextLength;
            richTextBox4.SelectionColor = Color.Black;
            richTextBox4.SelectionFont = new Font(richTextBox4.Font, FontStyle.Regular);
        }

        //======================================================
        //    STAMPA NETWORK
        //    NELLA RICHTEXTBOX
        //=====================================================

        public void DisplayNetworkActivity(List<ProcNetworkEntry> conns, int pid)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => DisplayNetworkActivity(conns, pid)));
                return;
            }

            richTextBox4.AppendText("\n===================================\n");
            richTextBox4.AppendText(" Network Activity (all processes !) \n");
            richTextBox4.AppendText("===================================\n\n");

            if (conns == null || conns.Count == 0)
            {
                richTextBox4.AppendText("(no network activity)\n");
                return;
            }

            foreach (var conn in conns)
            {
                // --- Colore protocollo ---
                Color protoColor = conn.Protocol switch
                {
                    "TCP" => Color.MediumBlue,
                    "UDP" => Color.Teal,
                    _ => Color.Black
                };

                // Protocol
                richTextBox4.SelectionColor = protoColor;
                richTextBox4.AppendText($"{conn.Protocol}: ");
                richTextBox4.SelectionColor = Color.Black;

                // Indirizzi
                richTextBox4.AppendText($"{conn.LocalAddress}:{conn.LocalPort} -> {conn.RemoteAddress}:{conn.RemotePort}");

                // --- Stato colorato (TCP + UDP) ---
                if (!string.IsNullOrEmpty(conn.State))
                {
                    richTextBox4.AppendText("  [ ");

                    Color stateColor = conn.State switch
                    {
                        // Stati comuni TCP/UDP
                        "LISTEN" => Color.Blue,
                        "ESTABLISHED" => Color.ForestGreen,

                        // TCP specifici
                        "SYN_SENT" => Color.DarkOrange,
                        "SYN_RECV" => Color.DarkOrange,
                        "TIME_WAIT" => Color.Gray,
                        "CLOSE_WAIT" => Color.IndianRed,
                        "LAST_ACK" => Color.MediumVioletRed,
                        "CLOSING" => Color.DarkRed,
                        "FIN_WAIT1" or "FIN_WAIT2" => Color.DarkGoldenrod,

                        // UDP specifici
                        "OPEN" => Color.ForestGreen,
                        "BOUND" => Color.DarkCyan,

                        // default / unknown
                        _ => Color.DimGray
                    };

                    richTextBox4.SelectionColor = stateColor;
                    richTextBox4.AppendText(conn.State);
                    richTextBox4.SelectionColor = Color.Black;
                    richTextBox4.AppendText(" ]");
                }

                richTextBox4.AppendText("\n");
            }

            // reset colore
            richTextBox4.SelectionColor = Color.Black;
        }


        //======================================================
        //    STAMPA OPEN PORTS
        //    NELLA RICHTEXTBOX
        //=====================================================
        public void DisplayListeningPorts(List<ProcListeningPort> ports, int pid)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => DisplayListeningPorts(ports, pid)));
                return;
            }

            AppendBanner("Open ports / listening ports", Color.SteelBlue);

            if (ports == null || ports.Count == 0)
            {
                richTextBox4.AppendText("(no listening ports)\n");
                return;
            }

            foreach (var p in ports)
                richTextBox4.AppendText($"{p.Protocol}: {p.LocalAddress}:{p.LocalPort}\n");
        }

        //======================================================
        //    STAMPA ARP TABLE
        //    NELLA RICHTEXTBOX
        //=====================================================
        public void DisplayArpTable(List<ProcArpEntry> arpEntries, int pid)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => DisplayArpTable(arpEntries, pid)));
                return;
            }

            AppendBanner("ARP Table", Color.DarkRed);

            if (arpEntries == null || arpEntries.Count == 0)
            {
                richTextBox4.AppendText("(no ARP entries found)\n");
                return;
            }

            foreach (var a in arpEntries)
            {
                richTextBox4.AppendText($"{a.IPAddress,-18} {a.MACAddress,-20} {a.Device}\n");
            }
        }

        //======================================================
        //    STAMPA ENVIRONMENT
        //    NELLA RICHTEXTBOX
        //=====================================================
        public void DisplayEnvironment(List<KeyValuePair<string, string>> envList, int pid)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => DisplayEnvironment(envList, pid)));
                return;
            }

            AppendBanner("Environment variables", Color.DarkOrange);

            if (envList == null || envList.Count == 0)
            {
                richTextBox4.AppendText("(no environment variables found)\n");
                return;
            }

            foreach (var kv in envList)
            {
                // Nome variabile → marrone scuro
                richTextBox4.SelectionColor = Color.DarkRed;
                richTextBox4.AppendText(kv.Key);

                // Separatore "=" → colore neutro
                richTextBox4.SelectionColor = Color.Black;
                richTextBox4.AppendText("=");

                // Valore variabile → rosso scuro
                richTextBox4.SelectionColor = Color.Blue;
                richTextBox4.AppendText(kv.Value + "\n");

                // Reset colore
                richTextBox4.SelectionColor = Color.Black;
            }
        }


        //======================================================
        //    POPOLA LISTVIEW
        //    FILE DESCRIPTOR CORRELATION
        //=====================================================

        public void DisplayFileDescriptorAnalysis(List<FileDescriptorInfo> fdAnalysis)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => DisplayFileDescriptorAnalysis(fdAnalysis)));
                return;
            }

            listView5.BeginUpdate();
            listView5.Items.Clear();

            if (fdAnalysis == null || fdAnalysis.Count == 0)
            {
                var emptyItem = new ListViewItem("(no file descriptors found)");
                listView5.Items.Add(emptyItem);
                listView5.EndUpdate();
                return;
            }

            foreach (var info in fdAnalysis)
            {
                var item = new ListViewItem(info.FD.ToString());

                // 2) Handler
                item.SubItems.Add(info.Handler);

                // 3) Correlation Inode
                item.SubItems.Add(string.IsNullOrEmpty(info.CorrelationInode) ? "" : info.CorrelationInode);

                // 4) Correlation Value
                item.SubItems.Add(string.IsNullOrEmpty(info.CorrelationValue) ? "" : info.CorrelationValue);

                // 5) Legitimate use
                item.SubItems.Add(string.IsNullOrEmpty(info.LegitimateUse) ? "" : info.LegitimateUse);

                // 6) Malicious use
                item.SubItems.Add(string.IsNullOrEmpty(info.MaliciousUse) ? "" : info.MaliciousUse);

                listView5.Items.Add(item);
            }

            listView5.EndUpdate();

            // 🔹 Ordina per FD numerico crescente
            listView5.ListViewItemSorter = new ListViewNumericComparer(0);
            listView5.Sort();
        }


        //======================================================
        //    STAMPA UNIX SOCKETS
        //    NELLA RICHTEXTBOX
        //=====================================================

        public void DisplayUnixSockets(List<ProcUnixSocket> sockets, int pid)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => DisplayUnixSockets(sockets, pid)));
                return;
            }

            AppendBanner("Unix Sockets", Color.DarkBlue);

            if (sockets == null || sockets.Count == 0)
            {
                richTextBox4.AppendText("(no unix sockets found)\n");
                return;
            }

            var withPath = sockets.Where(s => !string.IsNullOrEmpty(s.Path)).ToList();

            if (withPath.Count == 0)
            {
                richTextBox4.AppendText("(no unix sockets with path)\n");
                return;
            }

            // Intestazione
            richTextBox4.SelectionFont = new Font(richTextBox4.Font, FontStyle.Bold);
            richTextBox4.AppendText($"{"Inode",-10} {"Type",-10} {"State",-14} {"Path"}\n");
            richTextBox4.SelectionFont = new Font(richTextBox4.Font, FontStyle.Regular);
            richTextBox4.AppendText(new string('-', 85) + "\n");

            foreach (var s in withPath)
            {
                // 🔹 Decodifica tipo e stato
                string typeName = DecodeUnixType(s.Type);
                string stateName = DecodeUnixState(s.State);

                // Inode (neutro)
                richTextBox4.SelectionColor = Color.Black;
                richTextBox4.AppendText($"{s.Inode,-10} ");

                // Tipo (STREAM, DGRAM, ecc.)
                Color typeColor = typeName switch
                {
                    "STREAM" => Color.SaddleBrown,
                    "DGRAM" => Color.DarkGoldenrod,
                    "SEQPACKET" => Color.Teal,
                    "RAW" => Color.IndianRed,
                    _ => Color.Gray
                };
                richTextBox4.SelectionColor = typeColor;
                richTextBox4.AppendText($"{typeName,-10} ");

                // Stato (CONNECTED, ecc.)
                Color stateColor = stateName switch
                {
                    "CONNECTED" => Color.ForestGreen,
                    "CONNECTING" => Color.DarkOrange,
                    "UNCONNECTED" => Color.Gray,
                    "DISCONNECTING" => Color.IndianRed,
                    _ => Color.Black
                };
                richTextBox4.SelectionColor = stateColor;
                richTextBox4.AppendText($"{stateName,-14} ");

                // Path del socket
                richTextBox4.SelectionColor = Color.DarkRed;
                richTextBox4.AppendText($"{s.Path}\n");

                // reset
                richTextBox4.SelectionColor = Color.Black;
            }
        }


        //============================================
        private string DecodeUnixType(string typeHex)
        {
            return typeHex switch
            {
                "0001" => "STREAM",
                "0002" => "DGRAM",
                "0003" => "RAW",
                "0004" => "RDM",
                "0005" => "SEQPACKET",
                _ => typeHex
            };
        }

        //============================================
        private string DecodeUnixState(string stateHex)
        {
            return stateHex switch
            {
                "01" => "UNCONNECTED",
                "02" => "CONNECTING",
                "03" => "CONNECTED",
                "04" => "DISCONNECTING",
                _ => stateHex
            };
        }

        //======================================================
        //    STAMPA MEMORY MAP
        //    NELLA RICHTEXTBOX
        //=====================================================
        public void DisplayMemoryMap(List<string> maps, int pid)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => DisplayMemoryMap(maps, pid)));
                return;
            }

            richTextBox8.Clear();

            // --- Banner ---
            richTextBox8.SelectionColor = Color.DarkSlateGray;
            richTextBox8.AppendText("===================================\n");
            richTextBox8.AppendText($"Memory Map for PID {pid}\n");
            richTextBox8.AppendText("===================================\n\n");
            richTextBox8.SelectionColor = Color.Black;

            if (maps == null || maps.Count == 0)
            {
                richTextBox8.AppendText("(no memory maps found)\n");
                return;
            }

            // --- Intestazione tabellare ---
            richTextBox8.SelectionFont = new Font(richTextBox8.Font, FontStyle.Bold);
            richTextBox8.AppendText(
                $"{"Address Range",-25} {"Perm",-6} {"Offset",-8} {"Dev",-6} {"Inode",-8} {"Path"}\n");
            richTextBox8.SelectionFont = new Font(richTextBox8.Font, FontStyle.Regular);
            richTextBox8.AppendText(new string('-', 85) + "\n");

            // --- Stampa righe con colorazione tematica + evidenziazione ---
            foreach (string line in maps)
            {
                var cols = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (cols.Length < 5)
                    continue;

                string address = cols[0];
                string perm = cols[1];
                string offset = cols[2];
                string dev = DecodeDevice(cols[3]);
                string inode = cols[4];
                string path = cols.Length > 5 ? string.Join(' ', cols.Skip(5)) : "";

                // Determina il colore del testo in base al contenuto
                Color rowColor = Color.Black;

                if (path.Contains("[stack]"))
                    rowColor = Color.ForestGreen;
                else if (path.Contains("[heap]"))
                    rowColor = Color.MediumBlue;
                else if (path.Contains("[vdso]") || path.Contains("[vvar]") || path.Contains("[vsyscall]"))
                    rowColor = Color.DarkMagenta;
                else if (path.StartsWith("/lib") || path.Contains("/lib/") || path.Contains("/usr/lib"))
                    rowColor = Color.SteelBlue;
                else if (path.StartsWith("/usr/bin") || path.StartsWith("/bin"))
                    rowColor = Color.DarkOrange;
                else if (string.IsNullOrWhiteSpace(path))
                    rowColor = Color.DimGray; // mappature anonime

                // Verifica se l'area è eseguibile
                bool isExecutable = perm.Contains('x');

                // Registra inizio riga per eventuale highlight
                int start = richTextBox8.TextLength;

                // Stampa la riga con il colore tematico
                richTextBox8.SelectionColor = rowColor;
                richTextBox8.AppendText(
                    $"{address,-25} {perm,-6} {offset,-8} {dev,-6} {inode,-8} {path}\n");

                // Se è eseguibile, evidenzia la riga (background giallo)
                if (isExecutable)
                {
                    int end = richTextBox8.TextLength;
                    richTextBox8.Select(start, end - start);
                    richTextBox8.SelectionBackColor = Color.Yellow; // leggero highlight
                    richTextBox8.SelectionStart = end;
                    richTextBox8.SelectionBackColor = Color.White;       // reset per le righe successive
                }

                // Reset colore testo
                richTextBox8.SelectionColor = Color.Black;
            }

            // Reset finale
            richTextBox8.SelectionColor = Color.Black;
            richTextBox8.SelectionBackColor = Color.White;
        }



        //============================================

        private string DecodeDevice(string devField)
        {
            try
            {
                var parts = devField.Split(':');
                if (parts.Length != 2)
                    return devField;

                int major = Convert.ToInt32(parts[0], 16);
                int minor = Convert.ToInt32(parts[1], 16);

                if (major == 0 && minor == 0)
                    return "anon";          // mappatura anonima

                if (major == 8)
                    return $"/dev/sd{(char)('a' + minor / 16)}{minor % 16}";  // es: /dev/sda2

                if (major == 7)
                    return "/dev/loop";
                if (major == 1 && minor == 3)
                    return "/dev/null";
                if (major == 253)
                    return "/dev/dm";
                if (major == 254)
                    return "overlayfs";

                // fallback: mostra numeri decodificati
                return $"{major}:{minor}";
            }
            catch
            {
                return devField;
            }
        }

        //=================================================
        //  HELPER: scrittura CSV generica (riutilizzabile)
        //=================================================
        private void WriteCsvFile(string filePath, IEnumerable<string> headers, IEnumerable<string[]> rows)
        {
            try
            {
                using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    // intestazioni
                    writer.WriteLine(string.Join(",", headers));

                    // righe
                    foreach (var row in rows)
                    {
                        // Escape automatico dei campi
                        var safeRow = row.Select(CsvSafe);
                        writer.WriteLine(string.Join(",", safeRow));
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error writing CSV file '{Path.GetFileName(filePath)}':\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // helper per rendere sicuro un campo CSV
        private static string CsvSafe(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "\"\"";
            return $"\"{s.Replace("\"", "\"\"")}\"";
        }

        //========================================
    }  // chiude MainClass
}      // chiude Namespace
