using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LAP
{

    // ================================================================
    //  Strutture dati
    // ================================================================
    public class ProcEntry
    {
        public int PID { get; set; }
        public string User { get; set; } = string.Empty;       // /proc/PID/loginuid
        public string Session { get; set; } = string.Empty;
        public string Comm { get; set; } = string.Empty;
        public string CmdLine { get; set; } = string.Empty;
        public List<ProcFD> FileDescriptors { get; set; } = new();
        public List<ProcNetworkEntry> NetworkActivity { get; set; } = new();
        public List<ProcListeningPort> ListeningPorts { get; set; } = new();
        public List<ProcArpEntry> ArpTable { get; set; } = new();
        public List<KeyValuePair<string, string>> Environment { get; set; } = new();
        public List<string> MemoryMap { get; set; } = new();
        public List<ProcUnixSocket> UnixSockets { get; set; } = new();

    }

    public class ProcFD
    {
        public int FDNumber { get; set; }
        public string Target { get; set; } = string.Empty;
    }

    public class ProcNetworkEntry
    {
        public string Protocol { get; set; } = string.Empty;
        public string LocalAddress { get; set; } = string.Empty;
        public int LocalPort { get; set; }
        public string RemoteAddress { get; set; } = string.Empty;
        public int RemotePort { get; set; }
        public string State { get; set; } = string.Empty; // ✅ aggiunto
        public string Inode { get; set; } = string.Empty;
    }
    
    public class ProcListeningPort
    {
        public string Protocol { get; set; } = string.Empty;  // TCP o UDP
        public string LocalAddress { get; set; } = string.Empty;
        public int LocalPort { get; set; }
    } 

    public class ProcArpEntry
    {
        public string IPAddress { get; set; } = string.Empty;
        public string HWType { get; set; } = string.Empty;
        public string Flags { get; set; } = string.Empty;
        public string MACAddress { get; set; } = string.Empty;
        public string Mask { get; set; } = string.Empty;
        public string Device { get; set; } = string.Empty;
    }

    public class ProcUnixSocket
    {
        public string RefCount { get; set; } = string.Empty;
        public string Protocol { get; set; } = string.Empty;
        public string Flags { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Inode { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }

    public class FileDescriptorInfo
    {
        public int FD { get; set; }
        public string Handler { get; set; } = string.Empty;
        public string CorrelationInode { get; set; } = string.Empty;
        public string CorrelationValue { get; set; } = string.Empty;
        public string LegitimateUse { get; set; } = string.Empty;
        public string MaliciousUse { get; set; } = string.Empty;
    }

    public partial class MainClass : Form
    {
        // Path selezionato dell’albero /proc
        private string procPath = string.Empty;

        // Lista globale di processi analizzati
        private List<ProcEntry> procResults = new List<ProcEntry>();
        private List<FileDescriptorInfo> fdLookupResults = new();




        // ================================================================
        // PARSING PRINCIPALE
        // ================================================================
        public async Task<List<ProcEntry>> ParseProcDirectoryAsync(string procPath)
        {
            var results = new List<ProcEntry>();

            try
            {
                var pidDirs = Directory.GetDirectories(procPath)
                    .Where(dir => Path.GetFileName(dir).All(char.IsDigit))
                    .ToList();

                int total = pidDirs.Count;
                int processed = 0;

                foreach (var dir in pidDirs)
                {
                    string pidStr = Path.GetFileName(dir);
                    if (!int.TryParse(pidStr, out int pid))
                        continue;

                    var entry = new ProcEntry { PID = pid };

                    string commPath = Path.Combine(dir, "comm");
                    string cmdlinePath = Path.Combine(dir, "cmdline");
                    string fdPath = Path.Combine(dir, "fd");
                    string netPath = Path.Combine(dir, "net");
                    string arpPath = Path.Combine(netPath, "arp");
                    string envPath = Path.Combine(dir, "environ");
                    string mapsPath = Path.Combine(dir, "maps");
                    string unixPath = Path.Combine(netPath, "unix");

                    // 🆕 NEW
                    string loginUidPath = Path.Combine(dir, "loginuid");
                    string sessionIdPath = Path.Combine(dir, "sessionid");

                    try
                    {
                        // ----------------------------
                        //       User / Session
                        // ----------------------------

                        // /proc/PID/loginuid
                        if (File.Exists(loginUidPath))
                            entry.User = File.ReadAllText(loginUidPath).Trim();

                        // /proc/PID/sessionid
                        if (File.Exists(sessionIdPath))
                            entry.Session = File.ReadAllText(sessionIdPath).Trim();


                        // ----------------------------
                        //       Comm
                        // ----------------------------

                        if (File.Exists(commPath))
                            entry.Comm = File.ReadAllText(commPath).Trim();


                        // ----------------------------
                        //       CmdLine
                        // ----------------------------

                        if (File.Exists(cmdlinePath))
                        {
                            byte[] bytes = await File.ReadAllBytesAsync(cmdlinePath);
                            entry.CmdLine = Encoding.UTF8.GetString(bytes)
                                .Replace('\0', ' ')
                                .Trim();
                        }


                        // ----------------------------
                        //       File Descriptors
                        // ----------------------------

                        entry.FileDescriptors = await ReadFileDescriptorsAsync(fdPath);


                        // ----------------------------
                        //       Network Activity
                        // ----------------------------

                        if (Directory.Exists(netPath))
                        {
                            entry.NetworkActivity = await ReadNetworkInfoAsync(netPath);
                            // entry.ListeningPorts = await ReadListeningPortsAsync(netPath);

                            if (File.Exists(arpPath))
                                entry.ArpTable = await ReadArpTableAsync(arpPath);
                        }


                        // ----------------------------
                        //       Environment Variables
                        // ----------------------------

                        if (File.Exists(envPath))
                            entry.Environment = await ReadEnvironmentAsync(envPath);


                        // ----------------------------
                        //       Unix Sockets
                        // ----------------------------

                        if (File.Exists(unixPath))
                            entry.UnixSockets = await ReadUnixSocketsAsync(unixPath);


                        // ----------------------------
                        //       Memory Map
                        // ----------------------------

                        if (File.Exists(mapsPath))
                            entry.MemoryMap = await ReadMemoryMapAsync(mapsPath);


                        // ============================================================
                        //  ANALISI FILE DESCRIPTORS CON CORRELAZIONI
                        // ============================================================

                        if (Directory.Exists(fdPath))
                        {
                            var fdInfos = await AnalyzeFileDescriptorsAsync(
                                dir,
                                pid,
                                entry.NetworkActivity,
                                entry.UnixSockets);

                            // Aggiorna listView5 e lista globale
                            UpdateFileDescriptorView(fdInfos, pid);

                            // Sincronizza con FileDescriptors base
                            entry.FileDescriptors = fdInfos
                                .Select(fd => new ProcFD { FDNumber = fd.FD, Target = fd.Handler })
                                .ToList();
                        }

                        results.Add(entry);
                    }
                    catch
                    {
                        // Il processo può non essere accessibile → ignora
                        continue;
                    }


                    // ----------------------------
                    //      Progress bar update
                    // ----------------------------

                    processed++;
                    int progress = (int)((processed / (double)total) * 100);

                    if (InvokeRequired)
                    {
                        Invoke(new Action(() =>
                        {
                            toolStripProgressBar1.Value = Math.Min(progress, 100);
                        }));
                    }
                    else
                    {
                        toolStripProgressBar1.Value = Math.Min(progress, 100);
                    }
                }

                // ----------------------------
                //   Salva risultati globali
                // ----------------------------

                procResults = results;

                // ----------------------------
                //      Feedback finale UI
                // ----------------------------

                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        toolStripStatusLabel1.Text = $"Parsing completed: {results.Count} processes.";
                        toolStripProgressBar1.Value = 100;
                    }));
                }
                else
                {
                    toolStripStatusLabel1.Text = $"Parsing completed: {results.Count} processes.";
                    toolStripProgressBar1.Value = 100;
                }
            }
            catch (Exception ex)
            {
                Invoke(new Action(() =>
                {
                    MessageBox.Show($"Error parsing /proc: {ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }));
            }

            return results;
        }


        // ================================================================
        // 🔹 LETTURA MEMORY MAP
        // ================================================================

        public async Task<List<string>> ReadMemoryMapAsync(string mapsPath)
        {
            var list = new List<string>();

            if (!File.Exists(mapsPath))
                return list;

            try
            {
                var lines = await File.ReadAllLinesAsync(mapsPath);
                list.AddRange(lines.Take(1000)); // limite di sicurezza (facoltativo)
            }
            catch
            {
                // ignora errori per processi non accessibili
            }

            return list;
        }

        // ================================================================
        // 🔹 LETTURA FILE DESCRIPTORS
        // ================================================================
        public async Task<List<ProcFD>> ReadFileDescriptorsAsync(string fdPath)
        {
            var list = new List<ProcFD>();

            if (!Directory.Exists(fdPath))
                return list;

            var files = Directory.GetFiles(fdPath)
                .OrderBy(f =>
                {
                    string name = Path.GetFileName(f);
                    return int.TryParse(name, out int n) ? n : int.MaxValue;
                })
                .ToArray();

            foreach (var file in files)
            {
                var fd = new ProcFD();
                fd.FDNumber = int.TryParse(Path.GetFileName(file), out int num) ? num : -1;

                try
                {
                    fd.Target = await Task.Run(() => File.ReadAllText(file).Trim());
                    if (string.IsNullOrEmpty(fd.Target))
                        fd.Target = "(empty file)";
                }
                catch
                {
                    // prova a leggere link simbolico
                    try
                    {
                        fd.Target = Path.GetFullPath(file);
                    }
                    catch
                    {
                        fd.Target = "(unreadable)";
                    }
                }

                list.Add(fd);
            }

            return list;
        }

        // ================================================================
        // 🔹 LETTURA UNIX SOCKETS
        // ================================================================

        public async Task<List<ProcUnixSocket>> ReadUnixSocketsAsync(string unixPath)
        {
            var sockets = new List<ProcUnixSocket>();

            if (!File.Exists(unixPath))
                return sockets;

            try
            {
                var lines = await File.ReadAllLinesAsync(unixPath);
                if (lines.Length <= 1)
                    return sockets;

                // Skip intestazione
                foreach (var line in lines.Skip(1))
                {
                    var cols = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (cols.Length < 7)
                        continue;

                    // Il path (se presente) è dopo il settimo campo
                    string path = cols.Length > 7 ? string.Join(' ', cols.Skip(7)) : "";

                    sockets.Add(new ProcUnixSocket
                    {
                        RefCount = cols[0],
                        Protocol = cols[1],
                        Flags = cols[2],
                        Type = cols[4],
                        State = cols[5],
                        Inode = cols[6],
                        Path = path
                    });
                }
            }
            catch
            {
                // ignora processi non accessibili
            }

            return sockets;
        }

        // ================================================================
        // 🔹 LETTURA FILE DESCRIPTORS
        // ================================================================

        public async Task<List<FileDescriptorInfo>> AnalyzeFileDescriptorsAsync(
    string procDir,
    int pid,
    List<ProcNetworkEntry> netList,
    List<ProcUnixSocket> unixList)
        {
            var results = new List<FileDescriptorInfo>();

            try
            {
                string fdPath = Path.Combine(procDir, "fd");
                if (!Directory.Exists(fdPath))
                    return results;

                var deviceUseMap = new Dictionary<string, (string legit, string malicious)>(StringComparer.OrdinalIgnoreCase)
        {
            { "/dev/ptmx", ("Virtual console / interactive shell", "Possible C2 shell") },
            { "/dev/input/event0", ("USB or keyboard mapping", "Possible keylogger") },
            { "/dev/input/event1", ("USB or keyboard mapping", "Possible keylogger") },
            { "/dev/input/event2", ("USB or keyboard mapping", "Possible keylogger") },
            { "/dev/input/event3", ("USB or keyboard mapping", "Possible keylogger") },
            { "/dev/input/event4", ("USB or keyboard mapping", "Possible keylogger") },
            { "/dev/input/event5", ("USB or keyboard mapping", "Possible keylogger") },
            { "/dev/input/event6", ("USB or keyboard mapping", "Possible keylogger") },
            { "/dev/input/event7", ("USB or keyboard mapping", "Possible keylogger") },
            { "/dev/input/event8", ("USB or keyboard mapping", "Possible keylogger") },
            { "/dev/input/event9", ("USB or keyboard mapping", "Possible keylogger") },
            { "/dev/mem", ("Access to physical memory", "Suspicious - used by rootkits, memory dumpers etc") },
            { "/dev/kmem", ("Access to kernel memory", "Suspicious - used by rootkits, memory dumpers etc") },
            { "/dev/port", ("Direct access to I/O ports", "Very rare in normal processes") },
            { "/dev/tun", ("Tunnel or virtual network interface", "Indicator of Proxy, VPN or backdoor") },
            { "/dev/net/tun", ("Tunnel or virtual network interface", "Indicator of Proxy, VPN or backdoor") },
            { "/dev/tap0", ("Tunnel or virtual network interface", "Indicator of Proxy, VPN or backdoor") },
            { "/dev/tap1", ("Tunnel or virtual network interface", "Indicator of Proxy, VPN or backdoor") },
            { "/dev/tap2", ("Tunnel or virtual network interface", "Indicator of Proxy, VPN or backdoor") },
            { "/dev/tap3", ("Tunnel or virtual network interface", "Indicator of Proxy, VPN or backdoor") },
            { "/dev/tap4", ("Tunnel or virtual network interface", "Indicator of Proxy, VPN or backdoor") },
            { "/dev/tap5", ("Tunnel or virtual network interface", "Indicator of Proxy, VPN or backdoor") },
            { "/dev/tap6", ("Tunnel or virtual network interface", "Indicator of Proxy, VPN or backdoor") },
            { "/dev/tap7", ("Tunnel or virtual network interface", "Indicator of Proxy, VPN or backdoor") },
            { "/dev/tap8", ("Tunnel or virtual network interface", "Indicator of Proxy, VPN or backdoor") },
            { "/dev/tap9", ("Tunnel or virtual network interface", "Indicator of Proxy, VPN or backdoor") },
            { "/dev/random", ("Random Number Generator", "very common but useful to identify crypto malware") },
            { "/dev/urandom", ("Random Number Generator", "very common but useful to identify crypto malware") },
            { "/dev/pts/0", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/1", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/2", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/3", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/4", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/5", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/6", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/7", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/8", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/9", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/10", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/11", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/12", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/13", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/14", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/15", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/16", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/17", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/18", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/19", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/20", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/21", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/22", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/23", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/24", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/25", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/26", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/27", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/28", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/29", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/30", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/31", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/32", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/33", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/34", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/35", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/36", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/37", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/38", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/39", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/pts/40", ("Pseudo-TTY Terminal", "Possible interactive shell") },
            { "/dev/kmsg", ("Kernel logging", "Malware may intercept logging") },
            { "/dev/log", ("Syslog logging", "Malware may intercept logging") },
            { "/dev/fuse", ("Filesystem in user space", "Malware may mount virtual overlays or hide data") },
            { "/dev/tty0", ("Terminal", "Possible interactive shell") },
            { "/dev/tty1", ("Terminal", "Possible interactive shell") },
            { "/dev/tty2", ("Terminal", "Possible interactive shell") },
            { "/dev/tty3", ("Terminal", "Possible interactive shell") },
            { "/dev/tty4", ("Terminal", "Possible interactive shell") },
            { "/dev/tty5", ("Terminal", "Possible interactive shell") },
            { "/dev/tty6", ("Terminal", "Possible interactive shell") },
            { "/dev/tty7", ("Terminal", "Possible interactive shell") },
            { "/dev/tty8", ("Terminal", "Possible interactive shell") },
            { "/dev/tty9", ("Terminal", "Possible interactive shell") },
            { "/dev/tty10", ("Terminal", "Possible interactive shell") },
            { "/dev/tty11", ("Terminal", "Possible interactive shell") },
            { "/dev/tty12", ("Terminal", "Possible interactive shell") },
            { "/dev/tty13", ("Terminal", "Possible interactive shell") },
            { "/dev/tty14", ("Terminal", "Possible interactive shell") },
            { "/dev/tty15", ("Terminal", "Possible interactive shell") },
            { "/dev/tty16", ("Terminal", "Possible interactive shell") },
            { "/dev/tty17", ("Terminal", "Possible interactive shell") },
            { "/dev/tty18", ("Terminal", "Possible interactive shell") },
            { "/dev/tty19", ("Terminal", "Possible interactive shell") },
            { "/dev/tty20", ("Terminal", "Possible interactive shell") },
            { "/dev/tty21", ("Terminal", "Possible interactive shell") },
            { "/dev/tty22", ("Terminal", "Possible interactive shell") },
            { "/dev/tty23", ("Terminal", "Possible interactive shell") },
            { "/dev/tty24", ("Terminal", "Possible interactive shell") },
            { "/dev/tty25", ("Terminal", "Possible interactive shell") },
            { "/dev/tty26", ("Terminal", "Possible interactive shell") },
            { "/dev/tty27", ("Terminal", "Possible interactive shell") },
            { "/dev/tty28", ("Terminal", "Possible interactive shell") },
            { "/dev/tty29", ("Terminal", "Possible interactive shell") },
            { "/dev/tty30", ("Terminal", "Possible interactive shell") },
            { "/dev/tty31", ("Terminal", "Possible interactive shell") },
            { "/dev/tty32", ("Terminal", "Possible interactive shell") },
            { "/dev/tty33", ("Terminal", "Possible interactive shell") },
            { "/dev/tty34", ("Terminal", "Possible interactive shell") },
            { "/dev/tty35", ("Terminal", "Possible interactive shell") },
            { "/dev/tty36", ("Terminal", "Possible interactive shell") },
            { "/dev/tty37", ("Terminal", "Possible interactive shell") },
            { "/dev/tty38", ("Terminal", "Possible interactive shell") },
            { "/dev/tty39", ("Terminal", "Possible interactive shell") },
            { "/dev/tty40", ("Terminal", "Possible interactive shell") },
            { "/dev/ttyS0", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS1", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS2", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS3", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS4", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS5", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS6", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS7", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS8", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS9", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS10", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS11", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS12", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS13", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS14", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS15", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS16", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS17", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS18", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS19", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS20", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS21", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS22", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS23", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS24", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS25", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS26", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS27", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS28", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS29", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS30", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS31", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS32", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS33", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS34", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS35", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS36", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS37", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS38", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS39", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyS40", ("Terminal to serial port", "Possible interception") },
            { "/dev/ttyUSB0", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB1", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB2", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB3", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB4", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB5", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB6", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB7", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB8", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB9", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB10", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB11", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB12", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB13", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB14", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB15", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB16", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB17", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB18", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB19", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB20", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB21", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB22", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB23", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB24", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB25", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB26", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB27", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB28", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB29", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB30", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB31", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB32", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB33", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB34", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB35", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB36", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB37", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB38", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB39", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyUSB40", ("Terminal to USB port", "Possible interception") },
            { "/dev/ttyp0", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp1", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp2", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp3", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp4", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp5", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp6", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp7", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp8", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp9", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp10", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp11", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp12", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp13", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp14", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp15", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp16", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp17", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp18", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp19", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp20", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp21", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp22", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp23", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp24", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp25", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp26", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp27", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp28", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp29", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp30", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp31", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp32", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp33", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp34", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp35", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp36", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp37", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp38", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp39", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/ttyp40", ("Pseudo-Terminal", "Possible interactive shell") },
            { "/dev/hidraw0", ("Access to HID USBs, dongles etc", "Possible sniffer or keylogger") },
            { "/dev/hidraw1", ("Access to HID USBs, dongles etc", "Possible sniffer or keylogger") },
            { "/dev/hidraw2", ("Access to HID USBs, dongles etc", "Possible sniffer or keylogger") },
            { "/dev/hidraw3", ("Access to HID USBs, dongles etc", "Possible sniffer or keylogger") },
            { "/dev/hidraw4", ("Access to HID USBs, dongles etc", "Possible sniffer or keylogger") },
            { "/dev/hidraw5", ("Access to HID USBs, dongles etc", "Possible sniffer or keylogger") },
            { "/dev/hidraw6", ("Access to HID USBs, dongles etc", "Possible sniffer or keylogger") },
            { "/dev/hidraw7", ("Access to HID USBs, dongles etc", "Possible sniffer or keylogger") },
            { "/dev/hidraw8", ("Access to HID USBs, dongles etc", "Possible sniffer or keylogger") },
            { "/dev/hidraw9", ("Access to HID USBs, dongles etc", "Possible sniffer or keylogger") },
            { "/dev/hidraw10", ("Access to HID USBs, dongles etc", "Possible sniffer or keylogger") },
            { "/dev/vcs0", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs1", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs2", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs3", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs4", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs5", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs6", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs7", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs8", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs9", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs10", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs11", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs12", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs13", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs14", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs15", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs16", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs17", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs18", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs19", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs20", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs21", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs22", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs23", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs24", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs25", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs26", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs27", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs28", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs29", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs30", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs31", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs32", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs33", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs34", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs35", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs36", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs37", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs38", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs39", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcs40", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa0", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa1", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa2", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa3", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa4", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa5", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa6", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa7", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa8", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa9", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa10", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa11", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa12", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa13", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa14", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa15", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa16", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa17", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa18", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa19", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa20", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa21", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa22", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa23", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa24", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa25", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa26", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa27", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa28", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa29", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa30", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa31", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa32", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa33", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa34", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa35", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa36", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa37", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa38", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa39", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/dev/vcsa40", ("Video console screen buffers", "Rarely used but can capture screen") },
            { "/proc/kcore", ("Access to the Kernel virtual memory", "Depending on context, this can be very suspicious") },
            { "/proc/self/mem", ("Access to the memory space of a process", "Possible injection or dumping") },
            { "/proc/modules", ("Reading Kernel loaded modules", "Legitimate only for diagnostic tools") },
            { "/mnt", ("Common mount", "Can be suspicious, depending on context") },
            { "/media", ("Common mount", "Can be suspicious, depending on context") },
            { "/tmp/mnt", ("Common mount", "Can be suspicious, depending on context") },
            { "/proc/sysrq-trigger", ("Forces system events", "If overwritten it can cause crashes") },
            { "/sys/kernel/debug/tracing", ("Debugging - Tracing", "Could indicate kernel tracing or rootkit hooks") },
            { "/var/lib/sss/mc/passwd", ("Used by SSSD (System Security Services Daemon) for credential caching", "very suspicious on non-SSSD processes") },
            { "/dev/autofs", ("Device/interface for mounting SMB/NFS etc", "can create hidden mountpoints (for exfil, lateral movement etc.") },
            { "/dev/dri/card0", ("DRM (Direct Rendering Manager) for GPU", "can capture display buffers, screenshots or manipulate display output") }
        };

                var fdFiles = Directory.EnumerateFiles(fdPath)
                    .OrderBy(f => int.TryParse(Path.GetFileName(f), out var n) ? n : int.MaxValue)
                    .ToList();

                foreach (var file in fdFiles)
                {
                    var info = new FileDescriptorInfo();

                    string fdName = Path.GetFileName(file);
                    info.FD = int.TryParse(fdName, out int fdNum) ? fdNum : -1;

                    string handler = "(unreadable)";
                    try
                    {
                        var fi = new FileInfo(file);

                        // 1️ Se è un vero symlink (caso /proc reale)
                        if (fi.LinkTarget != null)
                        {
                            handler = fi.LinkTarget;
                        }
                        else
                        {
                            // 2️ Dataset offline: prova a leggere il contenuto del file
                            string content = await File.ReadAllTextAsync(file);
                            if (!string.IsNullOrWhiteSpace(content))
                                handler = content.Trim();
                        }
                    }
                    catch
                    {
                        handler = "(unreadable)";
                    }

                    info.Handler = handler;

                    string inode = ExtractInode(handler);
                    info.CorrelationInode = inode;

                     if (!string.IsNullOrEmpty(inode))
                    {
                        var netMatch = netList.FirstOrDefault(n => n.Inode == inode);
                        if (netMatch != null)
                        {
                            info.CorrelationValue =
                                $"{netMatch.Protocol}: {netMatch.LocalAddress}:{netMatch.LocalPort} -> " +
                                $"{netMatch.RemoteAddress}:{netMatch.RemotePort}" +
                                (string.IsNullOrEmpty(netMatch.State) ? "" : $" [ {netMatch.State} ]");
                        }
                        else
                        {
                            var unixMatch = unixList.FirstOrDefault(u => u.Inode == inode);
                            if (unixMatch != null)
                            {
                                info.CorrelationValue = string.IsNullOrEmpty(unixMatch.Path)
                                    ? "Unix Socket - [ no path ]"
                                    : $"Unix Socket - {unixMatch.Path}";
                            }
                            else
                            {
                                info.CorrelationValue = "Inode not found";
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(handler))
                    {
                        foreach (var kv in deviceUseMap)
                        {
                            if (handler.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase))
                            {
                                info.LegitimateUse = kv.Value.legit;
                                info.MaliciousUse = kv.Value.malicious;
                                break;
                            }
                        }
                    }

                    results.Add(info);
                }
            }
            catch (Exception ex)
            {
                Invoke(new Action(() =>
                {
                    MessageBox.Show($"Error analyzing file descriptors for PID {pid}: {ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }));
            }

            return await Task.FromResult(results);
        }


        //=========================================================

        private void UpdateFileDescriptorView(List<FileDescriptorInfo> fdList, int pid)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateFileDescriptorView(fdList, pid)));
                return;
            }

            if (fdList == null || fdList.Count == 0)
                return;

            //  Aggiunge i risultati alla lista globale (non si fa clear)
            fdLookupResults.AddRange(fdList);

            // Popola la ListView5
            foreach (var fd in fdList)
            {
                var item = new ListViewItem(new[]
                {
            fd.FD.ToString(),
            fd.Handler,
            fd.CorrelationInode,
            fd.CorrelationValue,
            fd.LegitimateUse,
            fd.MaliciousUse
        });
                listView5.Items.Add(item);
            }
        }

        //=========================================================

        private string ExtractInode(string handler)
        {
            if (string.IsNullOrEmpty(handler))
                return string.Empty;

            var match = System.Text.RegularExpressions.Regex.Match(handler, @"\[(\d+)\]");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }




        //============================================
        // Legge variabili ambiente
        //============================================

        public async Task<List<KeyValuePair<string, string>>> ReadEnvironmentAsync(string environPath)
        {
            var envList = new List<KeyValuePair<string, string>>();

            if (!File.Exists(environPath))
                return envList;

            try
            {
                byte[] data = await File.ReadAllBytesAsync(environPath);
                string content = Encoding.UTF8.GetString(data);
                string[] entries = content.Split('\0', StringSplitOptions.RemoveEmptyEntries);

                foreach (string item in entries)
                {
                    int sep = item.IndexOf('=');
                    if (sep > 0)
                    {
                        string key = item.Substring(0, sep);
                        string val = item.Substring(sep + 1);
                        envList.Add(new KeyValuePair<string, string>(key, val));
                    }
                }
            }
            catch
            {
                // ignora eccezioni (processi protetti)
            }

            return envList;
        }

        //============================================
        //     Legge Network
        //===========================================

        public async Task<List<ProcNetworkEntry>> ReadNetworkInfoAsync(string netPath)
        {
            var conns = new List<ProcNetworkEntry>();
            string[] files = { "tcp", "udp" };

            foreach (string protoFile in files)
            {
                string fpath = Path.Combine(netPath, protoFile);
                if (!File.Exists(fpath))
                    continue;

                string[] lines = await File.ReadAllLinesAsync(fpath);
                if (lines.Length <= 1)
                    continue;

                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var cols = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (cols.Length < 10)
                        continue;

                    try
                    {
                        string local = cols[1];
                        string remote = cols[2];
                        string stateHex = cols[3];
                        string inode = cols[9];

                        var (localIp, localPort) = DecodeAddress(local);
                        var (remoteIp, remotePort) = DecodeAddress(remote);

                        string state = DecodeSocketState(stateHex);

                        // 🔸 se è UDP, reinterpretare lo stato
                        if (protoFile == "udp")
                        {
                            // in UDP il kernel usa "07" per socket aperti
                            if (stateHex.ToUpper() == "07")
                                state = "OPEN";
                            else if (stateHex.ToUpper() == "0A")
                                state = "LISTEN";
                            else if (stateHex.ToUpper() == "01")
                                state = "ESTABLISHED";
                        }

                        conns.Add(new ProcNetworkEntry
                        {
                            Protocol = protoFile.ToUpper(),
                            LocalAddress = localIp,
                            LocalPort = localPort,
                            RemoteAddress = remoteIp,
                            RemotePort = remotePort,
                            State = state,
                            Inode = inode
                        });
                    }
                    catch
                    {
                        continue;
                    }
                }
            }

            return conns;
        }

        //==============================

        private string DecodeSocketState(string hex)
        {
            string code = hex.ToUpper();

            return code switch
            {
                "01" => "ESTABLISHED",
                "02" => "SYN_SENT",
                "03" => "SYN_RECV",
                "04" => "FIN_WAIT1",
                "05" => "FIN_WAIT2",
                "06" => "TIME_WAIT",
                "07" => "CLOSE",
                "08" => "CLOSE_WAIT",
                "09" => "LAST_ACK",
                "0A" => "LISTEN",
                "0B" => "CLOSING",
                "0C" => "NEW_SYN_RECV",
                _ => "UNKNOWN"
            };
        }

        // ======================helper per network

        private (string ip, int port) DecodeAddress(string hexPair)
        {
            try
            {
                var parts = hexPair.Split(':');
                string ipHex = parts[0];
                string portHex = parts[1];
                string ip = string.Join('.',
                    Enumerable.Range(0, 4).Select(j => Convert.ToInt32(ipHex.Substring(j * 2, 2), 16)));
                int port = Convert.ToInt32(portHex, 16);
                return (ip, port);
            }
            catch
            {
                return ("0.0.0.0", 0);
            }
        }

        //=============================================
        /*
        public async Task<List<ProcListeningPort>> ReadListeningPortsAsync(string netPath)
        {
            var ports = new List<ProcListeningPort>();
            string[] files = { "tcp", "udp" };

            foreach (string protoFile in files)
            {
                string fpath = Path.Combine(netPath, protoFile);
                if (!File.Exists(fpath))
                    continue;

                string[] lines = await File.ReadAllLinesAsync(fpath);
                if (lines.Length <= 1)
                    continue;

                foreach (string line in lines.Skip(1))
                {
                    var cols = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (cols.Length < 10)
                        continue;

                    string state = cols[3];
                    if (state != "0A") // solo le connessioni in stato LISTEN
                        continue;

                    try
                    {
                        var local = cols[1];
                        var (localIp, localPort) = DecodeAddress(local);

                        ports.Add(new ProcListeningPort
                        {
                            Protocol = protoFile.ToUpper(),
                            LocalAddress = localIp,
                            LocalPort = localPort
                        });
                    }
                    catch { continue; }
                }
            }

            return ports;
        } */

        //=========================================================
        //  LEGGE TABELLA ARP
        //=========================================================

        public async Task<List<ProcArpEntry>> ReadArpTableAsync(string arpPath)
        {
            var list = new List<ProcArpEntry>();

            if (!File.Exists(arpPath))
                return list;

            try
            {
                var lines = await File.ReadAllLinesAsync(arpPath);
                if (lines.Length <= 1)
                    return list;

                // salta l'header, ogni riga ha campi:
                // IP address, HW type, Flags, HW address, Mask, Device
                foreach (string line in lines.Skip(1))
                {
                    var cols = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (cols.Length < 6)
                        continue;

                    list.Add(new ProcArpEntry
                    {
                        IPAddress = cols[0],
                        HWType = cols[1],
                        Flags = cols[2],
                        MACAddress = cols[3],
                        Mask = cols[4],
                        Device = cols[5]
                    });
                }
            }
            catch
            {
                // ignora eccezioni dovute a permessi o processi kernel
            }

            return list;
        }


      

        //==============================================

    }  // Chiude MainClass



    // ================================================================
    // Comparatore numerico per ListView
    // ================================================================
    public class ListViewNumericComparer : IComparer
    {
        private readonly int columnIndex;

        public ListViewNumericComparer(int column)
        {
            columnIndex = column;
        }

        // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        //   da eliminare ?
        // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        public int Compare(object x, object y)
        {
            if (x is ListViewItem itemX && y is ListViewItem itemY)
            {
                if (int.TryParse(itemX.SubItems[columnIndex].Text, out int pidX) &&
                    int.TryParse(itemY.SubItems[columnIndex].Text, out int pidY))
                {
                    return pidX.CompareTo(pidY);
                }

                return string.Compare(itemX.SubItems[columnIndex].Text,
                    itemY.SubItems[columnIndex].Text,
                    StringComparison.Ordinal);
            }

            return 0;
        }
    }

    // =================================================================
}
