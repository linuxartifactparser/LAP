using System;
using System.Collections.Generic;
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
        // MAIN FUNCTION - ASYNCHRONOUS AUDIT.LOG PARSER
        // ============================================================
        public async Task ParseAuditAsync(string inputFolder, string outputFolder, RichTextBox logBox, ToolStripProgressBar progressBar)
        {
            try
            {
                await LogAsync(logBox, $"[INFO] Starting Audit.log parsing in: {inputFolder}\n");

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
                if (!CanWriteToFolder(outputFolder))
                {
                    MessageBox.Show(
                        $"Cannot write to output folder:\n{outputFolder}\n\nPlease select a different folder or run as administrator.",
                        "Write Permission Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    return;
                }

                var logFiles = new DirectoryInfo(inputFolder)
                    .GetFiles("audit.log*")
                    .OrderBy(f => f.Name)
                    .ToArray();

                if (logFiles.Length == 0)
                {
                    MessageBox.Show("No audit.log files found in the selected folder.", "No Logs Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string outputFile = Path.Combine(outputFolder, "Audit_Logs_Parsed.csv");
                int totalFiles = logFiles.Length;
                int processed = 0;
                int totalEvents = 0;
                List<long> allSecTimestamps = new List<long>();
                HashSet<string> uniqueLines = new HashSet<string>();

                UpdateProgress(progressBar, 0);

                using (var writer = new StreamWriter(outputFile, false, Encoding.Unicode))
                {
                    // PARAMETERS è l’ultima colonna
                    string header =
                        "TYPE\tUnix Timestamp\tDATE\tTIME\tRECORD ID\tcomm\texe\tpid\tterminal\thostname\tdirection\taddr\trport\tladdr\tlport\tsyscall\tacct\tterminal2\tuid\tauid\tgid\tses\tKnown_Good\tPARAMETERS";
                    await writer.WriteLineAsync(header);

                    foreach (var file in logFiles)
                    {
                        processed++;
                        int percent = (int)((processed / (double)totalFiles) * 100);
                        UpdateProgress(progressBar, percent);
                        await LogAsync(logBox, $"[INFO] Processing file {processed}/{totalFiles}: {file.Name}\n");

                        string[] lines = await File.ReadAllLinesAsync(file.FullName, Encoding.UTF8);

                        foreach (string originalLine in lines)
                        {
                            if (string.IsNullOrWhiteSpace(originalLine)) continue;
                            if (!originalLine.Contains("msg=audit(")) continue;
                            if (!uniqueLines.Add(originalLine)) continue;

                            string type = GetTypeFromLine(originalLine);
                            if (!TryParseAuditPrefix(originalLine, out long unixSeconds, out string recordId))
                                continue;

                            allSecTimestamps.Add(unixSeconds);
                            totalEvents++;

                            string parameters = ExtractParametersTail(originalLine);

                            // Decodifica/normalizzazione PROCTITLE e USER_CMD con copia in exe
                            parameters = MaybeNormalizeProctitle(type, parameters, out string proctitleText);
                            parameters = MaybeNormalizeUserCmd(type, parameters, out string usercmdText);

                            var fields = ExtractAuditFieldsFromParameters(parameters);
                            fields["terminal2"] = fields.TryGetValue("terminal", out var t) ? t : "";

                            // Known Good
                            fields["Known_Good"] =
                                Regex.IsMatch(parameters, "(tanium|falcon-sensor)", RegexOptions.IgnoreCase) ||
                                Regex.IsMatch(originalLine, "(tanium|falcon-sensor)", RegexOptions.IgnoreCase)
                                ? "X" : "";

                            // syscall lookup
                            if (fields.TryGetValue("syscall", out string sysVal) && !string.IsNullOrEmpty(sysVal))
                                fields["syscall"] = ResolveSyscallName(sysVal);

                            // exe: aggiungo testo da proctitle/usercmd (anche quando non è hex)
                            string exeValue = fields.GetValueOrDefault("exe", "");
                            string decodedJoin = JoinNonEmpty(" | ",
                                NormalizeDecoded(proctitleText),
                                NormalizeDecoded(usercmdText));

                            if (!string.IsNullOrEmpty(decodedJoin))
                            {
                                if (IsQuotedOrEmpty(exeValue))
                                    exeValue = decodedJoin;
                                else
                                    exeValue = exeValue + " | " + decodedJoin;
                            }

                            // Riga CSV
                            string csvRow = string.Join("\t", new[]
                            {
                                type,
                                unixSeconds.ToString(),
                                ConvertUnixTimestampToDate(unixSeconds),
                                ConvertUnixTimestampToTime(unixSeconds),
                                recordId,
                                fields.GetValueOrDefault("comm",""),
                                exeValue,
                                fields.GetValueOrDefault("pid",""),
                                fields.GetValueOrDefault("terminal",""),
                                fields.GetValueOrDefault("hostname",""),
                                fields.GetValueOrDefault("direction",""),
                                fields.GetValueOrDefault("addr",""),
                                fields.GetValueOrDefault("rport",""),
                                fields.GetValueOrDefault("laddr",""),
                                fields.GetValueOrDefault("lport",""),
                                fields.GetValueOrDefault("syscall",""),
                                fields.GetValueOrDefault("acct",""),
                                fields.GetValueOrDefault("terminal2",""),
                                fields.GetValueOrDefault("uid",""),
                                fields.GetValueOrDefault("auid",""),
                                fields.GetValueOrDefault("gid",""),
                                fields.GetValueOrDefault("ses",""),
                                fields.GetValueOrDefault("Known_Good",""),
                                parameters
                            });

                            await writer.WriteLineAsync(csvRow);
                        }
                    }
                }

                UpdateProgress(progressBar, 100);
                await LogAsync(logBox, $"[OK] Parsing completed successfully.\n[INFO] CSV saved to:\n{outputFile}\n");

                if (allSecTimestamps.Count > 0)
                {
                    long oldest = allSecTimestamps.Min();
                    long newest = allSecTimestamps.Max();
                    DateTime dtOld = DateTimeOffset.FromUnixTimeSeconds(oldest).UtcDateTime;
                    DateTime dtNew = DateTimeOffset.FromUnixTimeSeconds(newest).UtcDateTime;
                    TimeSpan diff = dtNew - dtOld;

                    await LogAsync(logBox,
                        $"\nOldest timestamp: {dtOld:MM/dd/yyyy HH:mm:ss UTC}\n" +
                        $"Newest timestamp: {dtNew:MM/dd/yyyy HH:mm:ss UTC}\n" +
                        $"Time Span: {diff.TotalDays:F1} days\n");

                    if (CheckOverlapBetweenFiles(inputFolder))
                        await LogAsync(logBox, "\nOverlapping time range detected between rotated logs.\n");

                    await LogAsync(logBox, $"\n[INFO] Total unique events parsed: {totalEvents}\n\n");
                }

                await Task.Delay(500);
                UpdateProgress(progressBar, 0);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An exception occurred:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateProgress(progressBar, 0);
            }
        }

        // ============================================================
        // PARSING HELPERS
        // ============================================================
        private string GetTypeFromLine(string line)
        {
            var m = Regex.Match(line, @"\btype=([A-Z_\-]+)");
            return m.Success ? m.Groups[1].Value : "";
        }

        private bool TryParseAuditPrefix(string line, out long unixSeconds, out string recordId)
        {
            unixSeconds = 0;
            recordId = "";
            var m = Regex.Match(line, @"msg=audit\((\d+)\.(\d+:\d+)\)");
            if (!m.Success) return false;
            long.TryParse(m.Groups[1].Value, out unixSeconds);
            recordId = m.Groups[2].Value;
            return true;
        }

        private string ExtractParametersTail(string line)
        {
            var m = Regex.Match(line, @"\)\:\s*(.*)$");
            return m.Success ? m.Groups[1].Value : "";
        }

        // ============================================================
        // PROCTITLE / USER_CMD NORMALIZATION (hex o ascii)
        // ============================================================
        private string MaybeNormalizeProctitle(string type, string parameters, out string proctitleText)
        {
            proctitleText = "";
            if (!string.Equals(type, "PROCTITLE", StringComparison.OrdinalIgnoreCase))
                return parameters;

            // Cattura proctitle con o senza virgolette
            var m = Regex.Match(parameters, @"\bproctitle=(?:""([^""]*)""|([^\s]+))");
            if (!m.Success) return parameters;

            string val = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;

            string decoded = val;
            if (IsLikelyHex(val))
                decoded = HexToAscii(val).Replace('\0', ' ').Trim();

            // Normalizza per exe (copiamo sempre, anche se non era hex)
            proctitleText = decoded;

            // Sostituzione in PARAMETERS sempre come proctitle="..."
            string replacement = $"proctitle=\"{decoded}\"";
            string newParams = parameters.Substring(0, m.Index) + replacement + parameters.Substring(m.Index + m.Length);
            return newParams;
        }

        private string MaybeNormalizeUserCmd(string type, string parameters, out string usercmdText)
        {
            usercmdText = "";
            if (!string.Equals(type, "USER_CMD", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(type, "USER-CMD", StringComparison.OrdinalIgnoreCase))
                return parameters;

            // Cattura cmd= con o senza virgolette (e senza assumere hex)
            var m = Regex.Match(parameters, @"\bcmd=(?:""([^""]*)""|([^\s']+))");
            if (!m.Success) return parameters;

            string val = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;

            string decoded = val;
            if (IsLikelyHex(val))
                decoded = HexToAscii(val).Replace('\0', ' ').Trim();

            usercmdText = decoded;

            // Manteniamo cmd=decoded senza virgolette (come da precedente comportamento)
            string replacement = $"cmd={decoded}";
            string newParams = parameters.Substring(0, m.Index) + replacement + parameters.Substring(m.Index + m.Length);
            return newParams;
        }

        private static bool IsLikelyHex(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if ((s.Length % 2) != 0 || s.Length < 4) return false;
            return Regex.IsMatch(s, @"\A[0-9A-Fa-f]+\z");
        }

        // ============================================================
        // FIELD EXTRACTION (solo dai PARAMETERS)
        // ============================================================
        private Dictionary<string, string> ExtractAuditFieldsFromParameters(string parameters)
        {
            string[] keys = { "comm", "exe", "pid", "terminal", "hostname", "direction", "addr", "rport", "laddr", "lport", "syscall", "acct", "uid", "auid", "gid", "ses" };
            var dict = new Dictionary<string, string>();
            foreach (string key in keys)
            {
                var m = Regex.Match(parameters, $@"\b{key}=([^\s']+)");
                if (m.Success) dict[key] = m.Groups[1].Value.Trim();
            }
            return dict;
        }

        // ============================================================
        // TIME + HEX + MISC HELPERS
        // ============================================================
        private string ConvertUnixTimestampToDate(long unixSeconds)
        {
            DateTime dt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
            return dt.ToString("MM/dd/yyyy");
        }

        private string ConvertUnixTimestampToTime(long unixSeconds)
        {
            DateTime dt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
            return dt.ToString("HH:mm:ss 'UTC'");
        }

        private string HexToAscii(string hex)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < hex.Length; i += 2)
            {
                string pair = (i + 2 <= hex.Length) ? hex.Substring(i, 2) : hex.Substring(i);
                if (byte.TryParse(pair, System.Globalization.NumberStyles.HexNumber, null, out byte b))
                    sb.Append((char)b);
            }
            return sb.ToString();
        }

        private static string NormalizeDecoded(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            s = Regex.Replace(s, @"[\t\r\n]+", " ");
            s = Regex.Replace(s, @"\s{2,}", " ").Trim();
            return s;
        }

        private static bool IsQuotedOrEmpty(string s)
        {
            if (string.IsNullOrEmpty(s)) return true;
            string trimmed = s.Trim();
            return trimmed == "\"\"" || trimmed == "''" || trimmed == "\"";
        }

        private static string JoinNonEmpty(string sep, params string[] parts)
        {
            var cleaned = parts.Where(p => !string.IsNullOrWhiteSpace(p));
            return string.Join(sep, cleaned);
        }

        // ============================================================
        // OVERLAP + UI HELPERS
        // ============================================================
        private bool CheckOverlapBetweenFiles(string inputFolder)
        {
            try
            {
                var files = new DirectoryInfo(inputFolder)
                    .GetFiles("audit.log*")
                    .OrderBy(f => f.Name)
                    .ToArray();
                long? prevMax = null;
                foreach (var f in files)
                {
                    var sec = new List<long>();
                    foreach (var l in File.ReadLines(f.FullName))
                    {
                        var m = Regex.Match(l, @"msg=audit\((\d{10})");
                        if (m.Success && long.TryParse(m.Groups[1].Value, out long ts))
                            sec.Add(ts);
                    }
                    if (sec.Count == 0) continue;
                    long min = sec.Min(), max = sec.Max();
                    if (prevMax.HasValue && min < prevMax.Value)
                        return true;
                    prevMax = max;
                }
                return false;
            }
            catch { return false; }
        }

        private bool CanWriteToFolder(string folderPath)
        {
            try
            {
                string testFile = Path.Combine(folderPath, $"_write_test_{Guid.NewGuid():N}.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return true;
            }
            catch { return false; }
        }

        private async Task LogAsync(RichTextBox box, string message)
        {
            if (box.InvokeRequired)
                box.Invoke((MethodInvoker)(() => box.AppendText(message)));
            else
                box.AppendText(message);
            await Task.Yield();
        }

        private void UpdateProgress(ToolStripProgressBar bar, int value)
        {
            if (bar == null) return;
            int safe = Math.Max(0, Math.Min(value, 100));
            if (bar.GetCurrentParent()?.InvokeRequired == true)
                bar.GetCurrentParent()?.Invoke((MethodInvoker)(() => bar.Value = safe));
            else
                bar.Value = safe;
        }

        // ============================================================
        // SYSCALL LOOKUP
        // ============================================================
        private string ResolveSyscallName(string val)
        {
            var m = Regex.Match(val ?? "", @"\d+");
            if (!m.Success) return val;
            if (!int.TryParse(m.Value, out int id)) return val;
            return _syscallMap.TryGetValue(id, out string name) ? name : val;
        }

        private static readonly Dictionary<int, string> _syscallMap = new Dictionary<int, string>
        {
            {0,"sys_read"},
            {1,"sys_write"},
            {2,"sys_open"},
            {3,"sys_close"},
            {4,"sys_stat"},
            {5,"sys_fstat"},
            {6,"sys_lstat"},
            {7,"sys_poll"},
            {8,"sys_lseek"},
            {9,"sys_mmap"},
            {10,"sys_mprotect"},
            {11,"sys_munmap"},
            {12,"sys_brk"},
            {13,"sys_rt_sigaction"},
            {14,"sys_rt_sigprocmask"},
            {15,"sys_rt_sigreturn"},
            {16,"sys_ioctl"},
            {17,"sys_pread64"},
            {18,"sys_pwrite64"},
            {19,"sys_readv"},
            {20,"sys_writev"},
            {21,"sys_access"},
            {22,"sys_pipe"},
            {23,"sys_select"},
            {24,"sys_sched_yield"},
            {25,"sys_mremap"},
            {26,"sys_msync"},
            {27,"sys_mincore"},
            {28,"sys_madvise"},
            {29,"sys_shmget"},
            {30,"sys_shmat"},
            {31,"sys_shmctl"},
            {32,"sys_dup"},
            {33,"sys_dup2"},
            {34,"sys_pause"},
            {35,"sys_nanosleep"},
            {36,"sys_getitimer"},
            {37,"sys_alarm"},
            {38,"sys_setitimer"},
            {39,"sys_getpid"},
            {40,"sys_sendfile"},
            {41,"sys_socket"},
            {42,"sys_connect"},
            {43,"sys_accept"},
            {44,"sys_sendto"},
            {45,"sys_recvfrom"},
            {46,"sys_sendmsg"},
            {47,"sys_recvmsg"},
            {48,"sys_shutdown"},
            {49,"sys_bind"},
            {50,"sys_listen"},
            {51,"sys_getsockname"},
            {52,"sys_getpeername"},
            {53,"sys_socketpair"},
            {54,"sys_setsockopt"},
            {55,"sys_getsockopt"},
            {56,"sys_clone"},
            {57,"sys_fork"},
            {58,"sys_vfork"},
            {59,"sys_execve"},
            {60,"sys_exit"},
            {61,"sys_wait4"},
            {62,"sys_kill"},
            {63,"sys_uname"},
            {64,"sys_semget"},
            {65,"sys_semop"},
            {66,"sys_semctl"},
            {67,"sys_shmdt"},
            {68,"sys_msgget"},
            {69,"sys_msgsnd"},
            {70,"sys_msgrcv"},
            {71,"sys_msgctl"},
            {72,"sys_fcntl"},
            {73,"sys_flock"},
            {74,"sys_fsync"},
            {75,"sys_fdatasync"},
            {76,"sys_truncate"},
            {77,"sys_ftruncate"},
            {78,"sys_getdents"},
            {79,"sys_getcwd"},
            {80,"sys_chdir"},
            {81,"sys_fchdir"},
            {82,"sys_rename"},
            {83,"sys_mkdir"},
            {84,"sys_rmdir"},
            {85,"sys_creat"},
            {86,"sys_link"},
            {87,"sys_unlink"},
            {88,"sys_symlink"},
            {89,"sys_readlink"},
            {90,"sys_chmod"},
            {91,"sys_fchmod"},
            {92,"sys_chown"},
            {93,"sys_fchown"},
            {94,"sys_lchown"},
            {95,"sys_umask"},
            {96,"sys_gettimeofday"},
            {97,"sys_getrlimit"},
            {98,"sys_getrusage"},
            {99,"sys_sysinfo"},
            {100,"sys_times"},
            {101,"sys_ptrace"},
            {102,"sys_getuid"},
            {103,"sys_syslog"},
            {104,"sys_getgid"},
            {105,"sys_setuid"},
            {106,"sys_setgid"},
            {107,"sys_geteuid"},
            {108,"sys_getegid"},
            {109,"sys_setpgid"},
            {110,"sys_getppid"},
            {111,"sys_getpgrp"},
            {112,"sys_setsid"},
            {113,"sys_setreuid"},
            {114,"sys_setregid"},
            {115,"sys_getgroups"},
            {116,"sys_setgroups"},
            {117,"sys_setresuid"},
            {118,"sys_getresuid"},
            {119,"sys_setresgid"},
            {120,"sys_getresgid"},
            {121,"sys_getpgid"},
            {122,"sys_setfsuid"},
            {123,"sys_setfsgid"},
            {124,"sys_getsid"},
            {125,"sys_capget"},
            {126,"sys_capset"},
            {127,"sys_rt_sigpending"},
            {128,"sys_rt_sigtimedwait"},
            {129,"sys_rt_sigqueueinfo"},
            {130,"sys_rt_sigsuspend"},
            {131,"sys_sigaltstack"},
            {132,"sys_utime"},
            {133,"sys_mknod"},
            {134,"uselib"},
            {135,"sys_personality"},
            {136,"sys_ustat"},
            {137,"sys_statfs"},
            {138,"sys_fstatfs"},
            {139,"sys_sysfs"},
            {140,"sys_getpriority"},
            {141,"sys_setpriority"},
            {142,"sys_sched_setparam"},
            {143,"sys_sched_getparam"},
            {144,"sys_sched_setscheduler"},
            {145,"sys_sched_getscheduler"},
            {146,"sys_sched_get_priority_max"},
            {147,"sys_sched_get_priority_min"},
            {148,"sys_sched_rr_get_interval"},
            {149,"sys_mlock"},
            {150,"sys_munlock"},
            {151,"sys_mlockall"},
            {152,"sys_munlockall"},
            {153,"sys_vhangup"},
            {154,"sys_modify_ldt"},
            {155,"sys_pivot_root"},
            {156,"_sysctl"},
            {157,"sys_prctl"},
            {158,"sys_arch_prctl"},
            {159,"sys_adjtimex"},
            {160,"sys_setrlimit"},
            {161,"sys_chroot"},
            {162,"sys_sync"},
            {163,"sys_acct"},
            {164,"sys_settimeofday"},
            {165,"sys_mount"},
            {166,"sys_umount2"},
            {167,"sys_swapon"},
            {168,"sys_swapoff"},
            {169,"sys_reboot"},
            {170,"sys_sethostname"},
            {171,"sys_setdomainname"},
            {172,"sys_iopl"},
            {173,"sys_ioperm"},
            {174,"create_module"},
            {175,"sys_init_module"},
            {176,"sys_delete_module"},
            {177,"get_kernel_syms"},
            {178,"query_module"},
            {179,"sys_iquotactl"},
            {180,"nftservctl"},
            {181,"getpmsg"},
            {182,"putpmsg"},
            {183,"afs_syscall"},
            {184,"tuxcall"},
            {185,"security"},
            {186,"sys_gettid"},
            {187,"sys_readahead"},
            {188,"sys_setxattr"},
            {189,"sys_lsetxattr"},
            {190,"sys_fsetxattr"},
            {191,"sys_getxattr"},
            {192,"sys_lgetxattr"},
            {193,"sys_fgetxattr"},
            {194,"sys_listxattr"},
            {195,"sys_llistxattr"},
            {196,"sys_flistxattr"},
            {197,"sys_removexattr"},
            {198,"sys_lremovexattr"},
            {199,"sys_fremovexattr"},
            {200,"sys_tkill"},
            {201,"sys_time"},
            {202,"sys_futex"},
            {203,"sys_sched_setaffinity"},
            {204,"sys_sched_getaffinity"},
            {206,"sys_io_setup"},
            {207,"sys_io_destroy"},
            {208,"sys_io_getevents"},
            {209,"sys_io_submit"},
            {210,"sys_io_cancel"},
            {211,"get_thread_area"},
            {212,"sys_lookup_dcookie"},
            {213,"sys_epoll_create"},

            {217,"sys_getdents64"},
            {218,"sys_set_tid_address"},
            {219,"sys_restart_syscall"},
            {220,"sys_semtimedop"},
            {221,"sys_fadvise64"},
            {222,"sys_timer_create"},
            {223,"sys_timer_settime"},
            {224,"sys_timer_gettime"},
            {225,"sys_timer_getoverrun"},
            {226,"sys_timer_delete"},
            {227,"sys_clock_settime"},
            {228,"sys_clock_gettime"},
            {229,"sys_clock_getres"},
            {230,"sys_clock_nanosleep"},
            {231,"sys_exit_group"},
            {232,"sys_epoll_wait"},
            {233,"sys_epoll_ctl"},
            {234,"sys_tgkill"},
            {235,"sys_utimes"},

            {237,"sys_mbind"},
            {238,"sys_set_mempolicy"},
            {239,"sys_get_mempolicy"},
            {240,"sys_mq_open"},
            {241,"sys_mq_unlink"},
            {242,"sys_mq_timedsend"},
            {243,"sys_mq_timedreceive"},
            {244,"sys_mq_notify"},
            {245,"sys_mq_getsetattr"},
            {246,"sys_kexec_load"},
            {247,"sys_waitid"},
            {248,"sys_add_key"},
            {249,"sys_request_key"},
            {250,"sys_keyctl"},
            {251,"sys_ioprio_set"},
            {252,"sys_ioprio_get"},
            {253,"sys_inotify_init"},
            {254,"sys_inotify_add_watch"},
            {255,"sys_inotify_rm_watch"},
            {256,"sys_migrate_pages"},
            {257,"sys_openat"},
            {258,"sys_mkdirat"},
            {259,"sys_mknodat"},
            {260,"sys_fchownat"},
            {261,"sys_futimesat"},
            {262,"sys_newfstatat"},
            {263,"sys_unlinkat"},
            {264,"sys_renameat"},
            {265,"sys_linkat"},
            {266,"sys_symlinkat"},
            {267,"sys_readlinkat"},
            {268,"sys_fchmodat"},
            {269,"sys_faccessat"},
            {270,"sys_pselect6"},
            {271,"sys_ppoll"},
            {272,"sys_unshare"},
            {273,"sys_set_robust_list"},
            {274,"sys_get_robust_list"},
            {275,"sys_splice"},
            {276,"sys_tee"},
            {277,"sys_sync_file_range"},
            {278,"sys_vmsplice"},
            {279,"sys_move_pages"},
            {280,"sys_utimensat"},
            {281,"sys_epoll_pwait"},
            {282,"sys_signalfd"},
            {283,"sys_timerfd_create"},
            {284,"sys_eventfd"},
            {285,"sys_fallocate"},
            {286,"sys_timerfd_settime"},
            {287,"sys_timerfd_gettime"},
            {288,"sys_accept4"},
            {289,"sys_signalfd4"},
            {290,"sys_eventfd2"},
            {291,"sys_epoll_create1"},
            {292,"sys_dup3"},
            {293,"sys_pipe2"},
            {294,"sys_inotify_init1"},
            {295,"sys_preadv"},
            {296,"sys_pwritev"},
            {297,"sys_rt_tgsigqueueinfo"},
            {298,"sys_perf_event_open"},
            {299,"sys_recvmmsg"},
            {300,"sys_fanotify_init"},
            {301,"sys_fanotify_mark"},
            {302,"sys_prlimit64"},
            {303,"sys_name_to_handle_at"},
            {304,"sys_open_by_handle_at"},
            {305,"sys_clock_adjtime"},
            {306,"sys_syncfs"},
            {307,"sys_sendmmsg"},
            {308,"sys_setns"},
            {309,"sys_getcpu"},
            {310,"sys_process_vm_readv"},
            {311,"sys_process_vm_writev"},
            {312,"sys_kcmp"},
            {313,"sys_finit_module"},
            {314,"sys_sched_setattr"},
            {315,"sys_sched_getattr"},
            {316,"sys_renameat2"},
            {317,"sys_seccomp"},
            {318,"sys_getrandom"},
            {319,"sys_memfd_create"},
            {320,"sys_kexec_file_load"},
            {321,"sys_bpf"},
            {322,"sys_execveat"},
            {323,"sys_userfaultfd"},
            {324,"sys_membarrier"},
            {325,"sys_mlock2"},
            {326,"sys_copy_file_range"},
            {327,"sys_preadv2"},
            {328,"sys_pwritev2"},
            {329,"sys_pkey_mprotect"},
            {330,"sys_pkey_alloc"},
            {331,"sys_pkey_free"},
            {332,"sys_statx"},
            {333,"sys_io_pgetevents"},
            {334,"sys_rseq"},

            {424,"sys_pidfd_send_signal"},
            {425,"sys_io_uring_setup"},
            {426,"sys_io_uring_enter"},
            {427,"sys_io_uring_register"},
            {428,"sys_open_tree"},
            {429,"sys_move_mount"},
            {430,"sys_fsopen"},
            {431,"sys_fsconfig"},
            {432,"sys_fsmount"},
            {433,"sys_fspick"},
            {434,"sys_pidfd_open"},
            {435,"sys_clone3"},
            {436,"sys_close_range"},
            {437,"sys_openat2"},
            {438,"sys_pidfd_getfd"},
            {439,"sys_faccessat2"},
            {440,"sys_process_madvise"},
            {441,"sys_epoll_pwait2"},
            {442,"sys_mount_setattr"},
            {443,"sys_quotactl_fd"},
            {444,"sys_landlock_create_ruleset"},
            {445,"sys_landlock_add_rule"},
            {446,"sys_landlock_restrict_self"},
            {447,"sys_memfd_secret"},
            {448,"sys_process_mrelease"},
            {449,"sys_futex_waitv"},
            {450,"sys_set_mempolicy_home_node"},
            {451,"sys_cachestat"},
            {452,"sys_fchmodat2"},
            {453,"sys_map_shadow_stack"},
            {454,"sys_futex_wake"},
            {455,"sys_futex_wait"},
            {456,"sys_futex_requeue"},
            {457,"sys_statmount"},
            {458,"sys_listmount"},
            {459,"sys_lsm_get_self_attr"},
            {460,"sys_lsm_set_self_attr"},
            {461,"sys_lsm_list_modules"},
            {462,"sys_mseal"},
            {463,"sys_sys_riscv_flush_icache"},    // placeholder (reserved ID in x86_64 table)
            {464,"sys_getrandom2"},                // alias/placeholder, reserved
            {465,"sys_sysfs_get_mountpoint"},      // future reserved
        };
    }
}
