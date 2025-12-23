using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LAP
{
    public partial class MainClass : Form
    {

        //============================================================ 
        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Svuota eventuali contenuti precedenti
            foreach (ListViewItem item in listView10.Items)
            {
                if (item.SubItems.Count > 1)
                    item.SubItems[1].Text = string.Empty;
            }

            string selected = comboBox2.SelectedItem?.ToString() ?? string.Empty;

            switch (selected)
            {

                case "RPM DNF Package Database":
                    UpdateListViewDetails(
                        "DNF Package DB",
                        "Database used by DNF to handle installed packages",
                        "/var/lib/dnf",
                        "history.sqlite"
                    );
                    break;

                case "RPM YUM Package Database":
                    UpdateListViewDetails(
                        "YUM Package DB",
                        "Database of packages and repositories handled via YUM",
                        "/var/lib/yum/history",
                        "history-YYYY-MM-DD.sqlite"
                    );
                    break;

                case "RPM DB History":
                    UpdateListViewDetails(
                        "RPM History",
                        "Database that stores the cronology of RPM operations",
                        "/var/lib/rpm",
                        "rpmdb.sqlite"
                    );
                    break;

                default:
                    // Se non riconosciuto, lascia vuoto
                    break;
            }
        }

        //============================================================ 

        private void UpdateListViewDetails(string artifact, string description, string path, string filename)
        {
            try
            {
                if (listView10.Items.Count < 4) return; // sicurezza

                string[] details = { artifact, description, path, filename };

                for (int i = 0; i < 4; i++)
                {
                    // Se la seconda colonna esiste già → aggiorna
                    if (listView10.Items[i].SubItems.Count > 1)
                    {
                        listView10.Items[i].SubItems[1].Text = details[i];
                    }
                    else // Altrimenti crea
                    {
                        listView10.Items[i].SubItems.Add(details[i]);
                    }
                    listView10.Items[i].UseItemStyleForSubItems = false; // necessario!
                    listView10.Items[i].SubItems[1].ForeColor = Color.DarkGreen;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error while updating the ListView: " + ex.Message);
            }
        }


        //============================================================ 
        // Escape CSV per virgole, doppi apici e newline
        private static string CsvEscape(string val)
        {
            if (string.IsNullOrEmpty(val)) return "";
            bool mustQuote = val.Contains(",") || val.Contains("\"") || val.Contains("\n") || val.Contains("\r");
            if (val.Contains("\"")) val = val.Replace("\"", "\"\"");
            return mustQuote ? $"\"{val}\"" : val;
        }

        //============================================================ 

        private async Task ExtractDnfHistoryAsync(string dbPath, string outputBasePath)
        {
            string outputFolder = Path.Combine(outputBasePath, "RPM DNF Package Database");
            Directory.CreateDirectory(outputFolder);

            var jobs = new List<(string Csv, string Sql)>
    {
        ("DNF_Transactions.csv", @"
SELECT
  t.id AS transaction_id,
  datetime(t.dt_begin, 'unixepoch') AS begin_time,
  datetime(t.dt_end, 'unixepoch') AS end_time,
  t.rpmdb_version_begin,
  t.rpmdb_version_end,
  t.releasever,
  t.user_id,
  t.cmdline,
  t.state
FROM trans t;"),

        ("DNF_RPMs.csv", @"
SELECT
  r.item_id,
  r.name,
  r.epoch,
  r.version,
  r.release,
  r.arch
FROM rpm r;"),

        ("DNF_Repos.csv", @"SELECT id, repoid FROM repo;"),

        ("DNF_ConsoleOutput.csv", @"
SELECT
  co.trans_id,
  datetime(t.dt_begin, 'unixepoch') AS trans_begin,
  co.line
FROM console_output co
JOIN trans t ON co.trans_id = t.id;")
    };

            int total = jobs.Count;
            Invoke(new Action(() =>
            {
                toolStripProgressBar1.Value = 0;
                toolStripStatusLabel1.Text = $"Extracting RPM DNF Package Database (0/{total})...";
                richTextBox6.AppendText($"[+] RPM DNF Package Database: {total} queries to run...\n");
            }));

            await Task.Run(() =>
            {
                using (var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly"))
                {
                    connection.Open();

                    for (int i = 0; i < total; i++)
                    {
                        var (csvName, sql) = jobs[i];
                        string csvPath = Path.Combine(outputFolder, csvName);
                        int step = i + 1;

                        Invoke(new Action(() =>
                        {
                            richTextBox6.AppendText($"[{step}/{total}] Writing {csvName} ...\n");
                            toolStripStatusLabel1.Text = $"Extracting RPM DNF Package Database ({step}/{total})...";
                        }));

                        try
                        {
                            using (var cmd = new SqliteCommand(sql, connection))
                            using (var reader = cmd.ExecuteReader())
                            using (var writer = new StreamWriter(csvPath, false, Encoding.UTF8))
                            {
                                // Intestazioni CSV
                                var columnNames = Enumerable.Range(0, reader.FieldCount)
                                                            .Select(idx => reader.GetName(idx));
                                writer.WriteLine(string.Join(",", columnNames));

                                // Riga per riga
                                while (reader.Read())
                                {
                                    var values = new string[reader.FieldCount];
                                    for (int c = 0; c < reader.FieldCount; c++)
                                    {
                                        if (reader.IsDBNull(c))
                                            values[c] = "";
                                        else
                                        {
                                            string val = reader.GetValue(c)?.ToString() ?? "";
                                            values[c] = CsvEscape(val);
                                        }
                                    }
                                    writer.WriteLine(string.Join(",", values));
                                }
                            }

                            int percent = (int)((step / (double)total) * 100.0);
                            Invoke(new Action(() =>
                            {
                                toolStripProgressBar1.Value = percent;
                                richTextBox6.AppendText($"    → Saved: {csvPath}\n");
                            }));
                        }
                        catch (Exception exJob)
                        {
                            Invoke(new Action(() =>
                            {
                                richTextBox6.AppendText($"[!] {csvName} failed: {exJob.Message}\n");
                            }));
                        }
                    }
                }
            });

            Invoke(new Action(() =>
            {
                toolStripProgressBar1.Value = 0;
                toolStripStatusLabel1.Text = "Ready";
                richTextBox6.AppendText("[✓] RPM DNF Package Database extraction completed.\n\n");
            }));
        }



        //============================================================ 

        private async Task ExtractYumHistoryAsync(string dbPath, string outputBasePath)
        {
            string outputFolder = Path.Combine(outputBasePath, "RPM YUM Package Database");
            Directory.CreateDirectory(outputFolder);

            var jobs = new List<(string Csv, string Sql)>
    {
        // 1️⃣ pacchetti RPM (pivoted)
        ("YUM_pkg_rpmdb.csv", @"
SELECT 
  p.pkgtupid AS id,
  MAX(CASE WHEN p.rpmdb_key = 'committer' THEN p.rpmdb_val END) AS committer,
  MAX(CASE WHEN p.rpmdb_key = 'vendor' THEN p.rpmdb_val END) AS vendor,
  MAX(CASE WHEN p.rpmdb_key = 'license' THEN p.rpmdb_val END) AS license,
  MAX(CASE WHEN p.rpmdb_key = 'url' THEN p.rpmdb_val END) AS url,
  datetime(MAX(CASE WHEN p.rpmdb_key = 'buildtime' THEN p.rpmdb_val END), 'unixepoch') AS buildtime,
  MAX(CASE WHEN p.rpmdb_key = 'buildhost' THEN p.rpmdb_val END) AS buildhost,
  MAX(CASE WHEN p.rpmdb_key = 'packager' THEN p.rpmdb_val END) AS packager,
  MAX(CASE WHEN p.rpmdb_key = 'sourcerpm' THEN p.rpmdb_val END) AS sourcerpm,
  datetime(MAX(CASE WHEN p.rpmdb_key = 'committime' THEN p.rpmdb_val END), 'unixepoch') AS committime,
  MAX(CASE WHEN p.rpmdb_key = 'size' THEN p.rpmdb_val END) AS size
FROM pkg_rpmdb p
GROUP BY p.pkgtupid
ORDER BY p.pkgtupid;
"),

        // 2️⃣ pacchetti YUM (pivoted)
        ("YUM_pkg_yumdb.csv", @"
SELECT
  y.pkgtupid AS id,
  datetime(MAX(CASE WHEN y.yumdb_key = 'from_repo_revision' THEN y.yumdb_val END), 'unixepoch') AS from_repo_revision,
  datetime(MAX(CASE WHEN y.yumdb_key = 'from_repo_timestamp' THEN y.yumdb_val END), 'unixepoch') AS from_repo_timestamp,
  MAX(CASE WHEN y.yumdb_key = 'reason' THEN y.yumdb_val END) AS reason,
  MAX(CASE WHEN y.yumdb_key = 'releasever' THEN y.yumdb_val END) AS releasever,
  MAX(CASE WHEN y.yumdb_key = 'installed_by' THEN y.yumdb_val END) AS installed_by,
  MAX(CASE WHEN y.yumdb_key = 'from_repo' THEN y.yumdb_val END) AS from_repo
FROM pkg_yumdb y
GROUP BY y.pkgtupid
ORDER BY y.pkgtupid;
"),

        // 3️⃣ transazioni con pacchetti
        ("YUM_trans_with_pkgs.csv", @"
SELECT
  twp.tid AS transaction_id,
  twp.pkgtupid AS package_id
FROM trans_with_pkgs twp;
"),

        // 4️⃣ problemi RPM
        ("YUM_trans_rpmdb_problems.csv", @"
SELECT
  p.rpid AS problem_id,
  p.tid AS transaction_id,
  p.problem,
  p.msg
FROM trans_rpmdb_problems p;
"),

        // 5️⃣ comandi YUM
        ("YUM_trans_cmdline.csv", @"
SELECT
  t.tid AS transaction_id,
  t.cmdline
FROM trans_cmdline t;
")
    };

            int total = jobs.Count;
            Invoke(new Action(() =>
            {
                toolStripProgressBar1.Value = 0;
                toolStripStatusLabel1.Text = $"Extracting RPM YUM Package Database (0/{total})...";
                richTextBox6.AppendText($"[+] RPM YUM Package Database: {total} queries to run...\n");
            }));

            await Task.Run(() =>
            {
                using (var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly"))
                {
                    connection.Open();

                    for (int i = 0; i < total; i++)
                    {
                        var (csvName, sql) = jobs[i];
                        string csvPath = Path.Combine(outputFolder, csvName);
                        int step = i + 1;

                        Invoke(new Action(() =>
                        {
                            richTextBox6.AppendText($"[{step}/{total}] Writing {csvName} ...\n");
                            toolStripStatusLabel1.Text = $"Extracting RPM YUM Package Database ({step}/{total})...";
                        }));

                        try
                        {
                            using (var cmd = new SqliteCommand(sql, connection))
                            using (var reader = cmd.ExecuteReader())
                            using (var writer = new StreamWriter(csvPath, false, Encoding.UTF8))
                            {
                                // Header CSV
                                var columnNames = Enumerable.Range(0, reader.FieldCount)
                                                            .Select(idx => reader.GetName(idx));
                                writer.WriteLine(string.Join(",", columnNames));

                                int rowCount = 0;

                                // Scrittura righe
                                while (reader.Read())
                                {
                                    var values = new string[reader.FieldCount];
                                    for (int c = 0; c < reader.FieldCount; c++)
                                    {
                                        if (reader.IsDBNull(c))
                                            values[c] = "";
                                        else
                                        {
                                            string val = reader.GetValue(c)?.ToString() ?? "";
                                            values[c] = CsvEscape(val);
                                        }
                                    }
                                    writer.WriteLine(string.Join(",", values));
                                    rowCount++;
                                }

                                Invoke(new Action(() =>
                                {
                                    richTextBox6.AppendText($"    → Saved: {csvPath} ({rowCount} rows)\n");
                                }));
                            }

                            // Progress cumulativo
                            int percent = (int)((step / (double)total) * 100.0);
                            Invoke(new Action(() => toolStripProgressBar1.Value = percent));
                        }
                        catch (Exception exJob)
                        {
                            Invoke(new Action(() =>
                            {
                                richTextBox6.AppendText($"[!] {csvName} failed: {exJob.Message}\n");
                            }));
                        }
                    }
                }
            });

            Invoke(new Action(() =>
            {
                toolStripProgressBar1.Value = 0;
                toolStripStatusLabel1.Text = "Ready";
                richTextBox6.AppendText("[✓] RPM YUM Package Database extraction completed.\n\n");
            }));
        }

        //============================================================

        private async Task ExtractRpmDbHistoryAsync(string dbPath, string outputBasePath)
        {
            string outputFolder = Path.Combine(outputBasePath, "RPM DB History");
            Directory.CreateDirectory(outputFolder);

            var jobs = new List<(string Csv, string Sql)>
    {
        ("RPMDB_Packages.csv", @"
SELECT
  n.hnum AS package_id,
  n.key AS name,
  g.key AS pkg_group
FROM Name n
LEFT JOIN ""Group"" g ON n.hnum = g.hnum
ORDER BY n.hnum;
"),

("RPMDB_Files.csv", @"
SELECT
  b.hnum AS package_id,
  d.key AS dirname,
  b.key AS basename,
  CASE
    WHEN d.key LIKE '%/' THEN d.key || b.key
    ELSE d.key || '/' || b.key
  END AS full_path
FROM Basenames b
JOIN Dirnames d
  ON d.hnum = b.hnum
 AND d.idx  = b.idx
ORDER BY b.hnum, b.idx;
"),

        ("RPMDB_Provides.csv", @"
SELECT
  p.hnum AS package_id,
  p.key AS provides
FROM Providename p
ORDER BY p.hnum;
"),

        ("RPMDB_Requires.csv", @"
SELECT
  r.hnum AS package_id,
  r.key AS requires
FROM Requirename r
ORDER BY r.hnum;
"),

        ("RPMDB_Signatures.csv", @"
SELECT
  s.hnum AS package_id,
  hex(s.key) AS md5_signature,
  sh.key AS sha1_header
FROM Sigmd5 s
LEFT JOIN Sha1header sh ON s.hnum = sh.hnum
ORDER BY s.hnum;
"),

        ("RPMDB_InstallTID.csv", @"
SELECT
  hnum AS package_id,
  hex(key) AS install_tid
FROM Installtid
ORDER BY hnum;
")
    };

            int total = jobs.Count;
            Invoke(new Action(() =>
            {
                toolStripProgressBar1.Value = 0;
                toolStripStatusLabel1.Text = $"Extracting RPM DB History (0/{total})...";
                richTextBox6.AppendText($"[+] RPM DB History: {total} queries to run...\n");
            }));

            await Task.Run(() =>
            {
                using (var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly"))
                {
                    connection.Open();

                    for (int i = 0; i < total; i++)
                    {
                        var (csvName, sql) = jobs[i];
                        string csvPath = Path.Combine(outputFolder, csvName);
                        int step = i + 1;

                        Invoke(new Action(() =>
                        {
                            richTextBox6.AppendText($"[{step}/{total}] Writing {csvName} ...\n");
                            toolStripStatusLabel1.Text = $"Extracting RPM DB History ({step}/{total})...";
                        }));

                        try
                        {
                            using (var cmd = new SqliteCommand(sql, connection))
                            using (var reader = cmd.ExecuteReader())
                            using (var writer = new StreamWriter(csvPath, false, Encoding.UTF8))
                            {
                                var columns = Enumerable.Range(0, reader.FieldCount)
                                                        .Select(idx => reader.GetName(idx));
                                writer.WriteLine(string.Join(",", columns));

                                int rowCount = 0;
                                while (reader.Read())
                                {
                                    var vals = new string[reader.FieldCount];
                                    for (int c = 0; c < reader.FieldCount; c++)
                                    {
                                        vals[c] = reader.IsDBNull(c) ? "" : CsvEscape(reader.GetValue(c)?.ToString() ?? "");
                                    }
                                    writer.WriteLine(string.Join(",", vals));
                                    rowCount++;
                                }

                                Invoke(new Action(() =>
                                {
                                    richTextBox6.AppendText($"    → Saved: {csvPath} ({rowCount} rows)\n");
                                }));
                            }

                            int percent = (int)((step / (double)total) * 100.0);
                            Invoke(new Action(() => toolStripProgressBar1.Value = percent));
                        }
                        catch (Exception ex)
                        {
                            Invoke(new Action(() =>
                            {
                                richTextBox6.AppendText($"[!] {csvName} failed: {ex.Message}\n");
                            }));
                        }
                    }
                }
            });

            Invoke(new Action(() =>
            {
                toolStripProgressBar1.Value = 0;
                toolStripStatusLabel1.Text = "Ready";
                richTextBox6.AppendText("[✓] RPM DB History extraction completed.\n\n");
            }));
        }

        //============================================================ 
    }
}
