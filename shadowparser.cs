using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LAP
{
    public partial class MainClass : Form
    {
        private async Task ParseShadowAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                MessageBox.Show("File not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string[] lines;
            try
            {
                lines = await File.ReadAllLinesAsync(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to read file:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            listView2.BeginUpdate();
            try
            {
                foreach (string raw in lines)
                {
                    if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("#"))
                        continue;

                    // Campi: 0=user, 1=passwd, 2=lastchg, 3=min, 4=max, 5=warn, 6=inactive, 7=expire
                    string[] f = raw.Split(':');
                    if (f.Length < 2)
                        continue;

                    string username = f[0]?.Trim() ?? "";
                    string passField = f[1]?.Trim() ?? "";

                    // Password column
                    string passwordDisplay = HasSaltedHash(passField) ? "[redacted]" : "";

                    // Algorithm column
                    string algorithm = DetectAlgorithm(passField);

                    // Date conversions
                    string lastChanged = ConvertDaysToDate(GetField(f, 2));
                    string minChange = GetField(f, 3);
                    string maxChange = GetField(f, 4);
                    string warnDays = GetField(f, 5);
                    string inactive = GetField(f, 6);
                    string expireDate = ConvertDaysToDate(GetField(f, 7));

                    var item = new ListViewItem(username);
                    item.SubItems.Add(algorithm);
                    item.SubItems.Add(passwordDisplay);
                    item.SubItems.Add(lastChanged);
                    item.SubItems.Add(minChange);
                    item.SubItems.Add(maxChange);
                    item.SubItems.Add(warnDays);
                    item.SubItems.Add(inactive);
                    item.SubItems.Add(expireDate);

                    listView2.Items.Add(item);
                }
            }
            finally
            {
                listView2.EndUpdate();
            }
        }

        private static string GetField(string[] fields, int index)
        {
            if (fields == null || index < 0 || index >= fields.Length) return "";
            string v = fields[index]?.Trim();
            return string.IsNullOrEmpty(v) ? "" : v;
        }

        private static bool HasSaltedHash(string passwordField)
        {
            if (string.IsNullOrWhiteSpace(passwordField))
                return false;

            if (passwordField == "!" || passwordField == "*")
                return false;

            string pf = passwordField.StartsWith("!$") ? passwordField.Substring(1) : passwordField;

            if (!pf.Contains("$")) return false;
            var parts = pf.Split('$');
            return parts.Length >= 4 &&
                   !string.IsNullOrEmpty(parts[1]) &&
                   !string.IsNullOrEmpty(parts[2]) &&
                   !string.IsNullOrEmpty(parts[3]);
        }

        private static string DetectAlgorithm(string passwordField)
        {
            if (string.IsNullOrWhiteSpace(passwordField))
                return "";

            string pf = passwordField.StartsWith("!$") ? passwordField.Substring(1) : passwordField;

            int first = pf.IndexOf('$');
            if (first < 0) return "";

            int second = pf.IndexOf('$', first + 1);
            if (second < 0) return "";

            string id = pf.Substring(first + 1, second - (first + 1));

            switch (id)
            {
                case "1": return "MD5";
                case "2a": return "Blowfish";
                case "2y": return "Blowfish";
                case "5": return "SHA-256";
                case "6": return "SHA-512";
                case "y": return "yescrypt";
                default: return $"Unknown ({id})";
            }
        }

        private static string ConvertDaysToDate(string daysField)
        {
            if (string.IsNullOrWhiteSpace(daysField))
                return "";

            if (long.TryParse(daysField, out long days))
            {
                var epoch = new DateTime(1970, 1, 1);
                try
                {
                    var dt = epoch.AddDays(days);
                    return dt.ToString("yyyy-MM-dd");
                }
                catch
                {
                    return "";
                }
            }
            return "";
        }

        //==============================================

        private async Task ParsePasswdAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                MessageBox.Show("File not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string groupName = "";
            string[] lines;
            try
            {
                lines = await File.ReadAllLinesAsync(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to read file:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            listView8.BeginUpdate();
            try
            {
                foreach (string raw in lines)
                {
                    if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("#"))
                        continue;

                    // Campi: user:passwd:uid:gid:gecos:home:shell
                    string[] f = raw.Split(':');
                    if (f.Length < 7)
                        continue;

                    string username = f[0]?.Trim() ?? "";
                    string uid = f[2]?.Trim() ?? "";
                    string gid = f[3]?.Trim() ?? "";
                    string fullName = f[4]?.Trim() ?? "";
                    string home = f[5]?.Trim() ?? "";
                    string shell = f[6]?.Trim() ?? "";

                    var item = new ListViewItem(username);
                    item.SubItems.Add(uid);
                    item.SubItems.Add(gid);
                    item.SubItems.Add(groupName);
                    item.SubItems.Add(fullName);
                    item.SubItems.Add(home);
                    item.SubItems.Add(shell);

                    listView8.Items.Add(item);
                }
            }
            finally
            {
                listView8.EndUpdate();
            }
        }

        //=============================================

    }
}
