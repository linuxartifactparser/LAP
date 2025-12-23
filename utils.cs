
using Be.Windows.Forms;
using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace LAP
{
    public partial class MainClass : Form
    {
        // ======================================
        //  IP ADDRESS LOOKUP
        // ======================================
        private void iPLookupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string selectedText = richTextBox4.SelectedText.Trim();

            if (string.IsNullOrEmpty(selectedText))
            {
                MessageBox.Show("Please select an IP Address.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Verifica formato IP
            if (!IPAddress.TryParse(selectedText, out IPAddress ip))
            {
                MessageBox.Show("The selected text is not a valid IP Address.", "Invalid IP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Controlli per localhost / broadcast / speciali
            if (IPAddress.IsLoopback(ip))
            {
                MessageBox.Show("This is a localhost IP Address.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Controllo IPv4 broadcast e riservati
            byte[] bytes = ip.GetAddressBytes();
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                // 255.255.255.255
                if (bytes[0] == 255 && bytes[1] == 255 && bytes[2] == 255 && bytes[3] == 255)
                {
                    MessageBox.Show("This is a broadcast IP Address.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 0.0.0.0
                if (bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0 && bytes[3] == 0)
                {
                    MessageBox.Show("This is a null IP Address.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 169.254.x.x → APIPA
                if (bytes[0] == 169 && bytes[1] == 254)
                {
                    MessageBox.Show("This is an APIPA (link-local) IP Address.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 10.x.x.x / 192.168.x.x / 172.16-31.x.x → Private
                if (bytes[0] == 10 ||
                    (bytes[0] == 192 && bytes[1] == 168) ||
                    (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31))
                {
                    MessageBox.Show("This is a private IP Address.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                   // return;
                }
            }

            try
            {
                // Lookup DNS
                string hostname = Dns.GetHostEntry(ip).HostName;

                if (!string.IsNullOrEmpty(hostname))
                {
                    int start = richTextBox4.SelectionStart;
                    int length = richTextBox4.SelectionLength;
                    richTextBox4.SelectedText = hostname;

                    // Mantiene la selezione sul nuovo testo
                    richTextBox4.SelectionStart = start;
                    richTextBox4.SelectionLength = hostname.Length;

                }
                else
                {
                    MessageBox.Show("Unable to resolve this IP Address.", "Lookup failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to resolve this IP Address.\n\n{ex.Message}", "Lookup failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //=================================
        // Base64 converter
        //=================================


        private void base64DecodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string selectedText = richTextBox4.SelectedText.Trim();

            if (string.IsNullOrEmpty(selectedText))
            {
                MessageBox.Show("No string is selected.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                // Decodifica Base64 → byte[]
                byte[] data = Convert.FromBase64String(selectedText);

                // Converte in stringa ASCII / UTF8
                string decodedString = Encoding.UTF8.GetString(data);

                // Sostituisce testo selezionato
                int start = richTextBox4.SelectionStart;
                richTextBox4.SelectedText = decodedString;
                richTextBox4.SelectionStart = start;
                richTextBox4.SelectionLength = decodedString.Length;
            }
            catch
            {
                MessageBox.Show("This is not a valid Base64 string.", "Invalid Base64", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        //==================================
        // HEx to signed int
        //==================================
        private void hexToDecToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string selectedText = richTextBox4.SelectedText.Trim();

            if (string.IsNullOrEmpty(selectedText))
            {
                MessageBox.Show("No string is selected.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Rimuove eventuali spazi
            string cleanHex = Regex.Replace(selectedText, @"\s+", "");

            // Controllo validità caratteri
            if (!Regex.IsMatch(cleanHex, @"\A[0-9A-Fa-f]+\z") || cleanHex.Length % 2 != 0)
            {
                MessageBox.Show("Not a valid Hex sequence.", "Invalid Hex", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Conversione a signed int (sbyte)
                string[] bytePairs = Enumerable.Range(0, cleanHex.Length / 2)
                                               .Select(i => cleanHex.Substring(i * 2, 2))
                                               .ToArray();

                var signedInts = bytePairs
                    .Select(b =>
                    {
                        byte value = byte.Parse(b, NumberStyles.HexNumber);
                        return unchecked((sbyte)value); // signed conversion
                    })
                    .ToArray();

                // Converte l'array in stringa (es: "-12 45 127 ...")
                string result = string.Join(" ", signedInts);

                // Sostituisce testo selezionato
                int start = richTextBox4.SelectionStart;
                richTextBox4.SelectedText = result;
                richTextBox4.SelectionStart = start;
                richTextBox4.SelectionLength = result.Length;
            }
            catch
            {
                MessageBox.Show("Not a valid Hex sequence.", "Invalid Hex", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        //==================================
        // HEX to UINT
        //==================================

        private void hexToUintToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string selectedText = richTextBox4.SelectedText.Trim();

            if (string.IsNullOrEmpty(selectedText))
            {
                MessageBox.Show("No string is selected.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Rimuove eventuali spazi e caratteri non validi
            string cleanHex = Regex.Replace(selectedText, @"\s+", "");

            // Verifica validità e lunghezza pari
            if (!Regex.IsMatch(cleanHex, @"\A[0-9A-Fa-f]+\z") || cleanHex.Length % 2 != 0)
            {
                MessageBox.Show("Not a valid Hex sequence.", "Invalid Hex", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Divide la stringa in coppie di byte
                string[] bytePairs = Enumerable.Range(0, cleanHex.Length / 2)
                                               .Select(i => cleanHex.Substring(i * 2, 2))
                                               .ToArray();

                // Converte in unsigned integer (0–255)
                var unsignedInts = bytePairs
                    .Select(b => byte.Parse(b, NumberStyles.HexNumber))
                    .Select(b => b.ToString())
                    .ToArray();

                string result = string.Join(" ", unsignedInts);

                // Sostituisce il testo selezionato
                int start = richTextBox4.SelectionStart;
                richTextBox4.SelectedText = result;
                richTextBox4.SelectionStart = start;
                richTextBox4.SelectionLength = result.Length;
            }
            catch
            {
                MessageBox.Show("Not a valid Hex sequence.", "Invalid Hex", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        //==================================
        // HEX to CHAR
        //==================================
        private void hexToCharToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string selectedText = richTextBox4.SelectedText.Trim();

            if (string.IsNullOrEmpty(selectedText))
            {
                MessageBox.Show("No string is selected.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Rimuove spazi e caratteri non validi
            string cleanHex = Regex.Replace(selectedText, @"\s+", "");

            // Verifica che sia esadecimale e di lunghezza pari
            if (!Regex.IsMatch(cleanHex, @"\A[0-9A-Fa-f]+\z") || cleanHex.Length % 2 != 0)
            {
                MessageBox.Show("Not a valid Hex sequence.", "Invalid Hex", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Converte da hex → bytes
                byte[] bytes = Enumerable.Range(0, cleanHex.Length / 2)
                                         .Select(i => Convert.ToByte(cleanHex.Substring(i * 2, 2), 16))
                                         .ToArray();

                // Converte bytes → stringa (ASCII/UTF-8)
                string decoded = Encoding.UTF8.GetString(bytes);

                // Sostituisce testo selezionato
                int start = richTextBox4.SelectionStart;
                richTextBox4.SelectedText = decoded;
                richTextBox4.SelectionStart = start;
                richTextBox4.SelectionLength = decoded.Length;
            }
            catch
            {
                MessageBox.Show("Not a valid Hex sequence.", "Invalid Hex", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        //=================================
        // COPIA
        //=================================

        private void copyCtrlCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (contextMenuStrip2.SourceControl is ListView lv && lv.SelectedItems.Count > 0)
            {
                var sb = new StringBuilder();

                foreach (ListViewItem item in lv.SelectedItems)
                {
                    string row = string.Join("\t", item.SubItems
                        .Cast<ListViewItem.ListViewSubItem>()
                        .Select(s => s.Text));
                    sb.AppendLine(row);
                }

                Clipboard.SetText(sb.ToString());
            }
        }


        //=================================
        // ESPORTA RIGHE SELEZIONATE
        //=================================
        private void exportSelectedRowsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (contextMenuStrip2.SourceControl is not ListView lv || lv.SelectedItems.Count == 0)
                return;

            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select destination folder for CSV export";
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    string filePath = Path.Combine(fbd.SelectedPath, lv.Tag + "Exported_selected.csv");
                    ExportListViewToCsv(lv, filePath, selectedOnly: true);
                    MessageBox.Show($"Selected rows exported:\n{filePath}", "Export Completed",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

 
        //=================================
        // HELPER PER ESPORTARE
        //=================================

        private void ExportListViewToCsv(ListView lv, string filePath, bool selectedOnly)
        {
            using (var sw = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                // Scrive header
                var headers = lv.Columns.Cast<ColumnHeader>().Select(c => EscapeCsv(c.Text));
                sw.WriteLine(string.Join(",", headers));

                // Scrive righe
                var items = selectedOnly ? lv.SelectedItems.Cast<ListViewItem>() : lv.Items.Cast<ListViewItem>();
                foreach (var item in items)
                {
                    var values = item.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(s => EscapeCsv(s.Text));
                    sw.WriteLine(string.Join(",", values));
                }
            }
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"'))
                value = "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }


        //===============================================================
        //    DA QUI INIZIANO I CONTROLLI PER IL CONTEXTMENU DI SCINTILLA
        //   clear all contents
        //===============================================================

        private void clearCommentsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string text = scintilla1.Text;
            text = Regex.Replace(text, @"#.*$", "", RegexOptions.Multiline);
            scintilla1.Text = text;
        }


        //===================================
        // Select all
        //===================================

        private void selectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            scintilla1.SelectAll();
        }

        //===================================
        // Base64 to ASCII
        //===================================

        private void fromBase64ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                string alfa = scintilla1.SelectedText;
                byte[] data = System.Convert.FromBase64String(alfa);
                string beta = Encoding.ASCII.GetString(data);
                scintilla1.ReplaceSelection(beta);
            }
            catch (FormatException)
            {
                MessageBox.Show("Parsing error - The selected text is not Base64.");
            }
        }

        //===================================
        // Hex to Dec
        //===================================
        private void hexToSignedIntToolStripMenuItem1_Click(object sender, EventArgs e)
        {

            try
            {
                BigInteger decValue;
                string alfaint = normalizehex(scintilla1.SelectedText);

                string[] gruppi = alfaint.Split(' ');
                string contenitore;
                StringBuilder builder = new StringBuilder();
                foreach (string elemento in gruppi)
                {
                    decValue = BigInteger.Parse(elemento, NumberStyles.AllowHexSpecifier);
                    contenitore = decValue.ToString();
                    builder.Append(contenitore);
                    builder.Append(" ");
                }

                string risultato = builder.ToString();
                scintilla1.ReplaceSelection(risultato);
            }
            catch (FormatException)
            {
                MessageBox.Show("Decoding error - Format unknown to me.");
            }
        }

        //===========================
        // Hex normalizer
        //===========================

        public string normalizehex(string grezza)
        {
            grezza = grezza.Replace("0x", " ");
            grezza = grezza.Replace("%", " ");
            grezza = grezza.Replace("\\x", " ");
            grezza = grezza.Replace("\\", "");
            grezza = grezza.Replace("x", "");
            grezza = Regex.Replace(grezza, " +", " ");
            grezza = grezza.TrimStart(' ');
            return grezza;
        }

        //======================================
        // Hex to Uint
        //======================================
        private void hexToUintToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            try
            {
                uint decValue;
                string alfaint = normalizehex(scintilla1.SelectedText);
                string[] gruppi = alfaint.Split(' ');
                string contenitore;
                StringBuilder builder = new StringBuilder();
                foreach (string elemento in gruppi)
                {
                    decValue = Convert.ToUInt32(elemento, 16);
                    contenitore = decValue.ToString();
                    builder.Append(contenitore);
                    builder.Append(" ");
                }

                string risultato = builder.ToString();
                scintilla1.ReplaceSelection(risultato);
            }
            catch (FormatException)
            {
                MessageBox.Show("Decoding error - Format unknown to me.");
            }
        }

        //===================================
        // Hex to Char
        //===================================

        private void hexToCharToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            try
            {
                string alfa = normalizehex(scintilla1.SelectedText);

                string[] gruppi = alfa.Split(' ');
                StringBuilder builder = new StringBuilder();
                foreach (string elemento in gruppi)
                {
                    builder.Append((char)Int16.Parse(elemento, NumberStyles.AllowHexSpecifier));
                    builder.Append(" ");
                }

                string risultato = builder.ToString();
                risultato = risultato.Replace(" ", "");
                scintilla1.ReplaceSelection(risultato);
            }
            catch (FormatException)
            {
                MessageBox.Show("Decoding error - Cannot handle this kind of hex values");
            }

            catch (OverflowException)
            {
                MessageBox.Show("Decoding error - Hex value too big to handle. I need groups of Hex values separated by a SPACE. Please edit manually, if you wish.");
            }
        }

        //===================================
        // Dec to Char
        //===================================

        private void decToCharToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            try
            {
                string alfa = scintilla1.SelectedText;
                alfa = Regex.Replace(alfa, " +", " ");
                alfa = alfa.TrimStart(' ');
                alfa = alfa.TrimEnd(' ');
                string[] gruppi = alfa.Split(' ');
                StringBuilder builder = new StringBuilder();
                foreach (string elemento in gruppi)
                {
                    builder.Append((char)Int16.Parse(elemento));
                    builder.Append(" ");
                }

                string risultato = builder.ToString();
                risultato = risultato.Replace(" ", "");
                scintilla1.ReplaceSelection(risultato);
            }
            catch (FormatException)
            {
                MessageBox.Show("Decoding error - Cannot handle this kind of hex values");
            }

            catch (OverflowException)
            {
                MessageBox.Show("Decoding error - Hex value too big to handle. I need groups of decimal values separated by a SPACE. Please edit manually, if you wish.");
            }
        }

        //===================================
        // Unicode decode (escaped ?)
        //===================================

        private void escapedUnicodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                string alfa = scintilla1.SelectedText;
                string beta = alfa.Replace("%", "\\");
                string gamma = System.Text.RegularExpressions.Regex.Unescape(beta);
                scintilla1.ReplaceSelection(gamma);
            }
            catch (Exception)
            {
                MessageBox.Show("Decoding did not work.");
            }
        }

        //=============================
        // HTML decode
        //=============================
        private void hTMLDecodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                string alfa = scintilla1.SelectedText;
                string beta = System.Net.WebUtility.HtmlDecode(alfa);
                scintilla1.ReplaceSelection(beta);
            }
            catch (Exception)
            {
                MessageBox.Show("Decoding did not work.");
            }
        }

        //=============================
        // URL decode
        //=============================

        private void uRLDecodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                string alfa = scintilla1.SelectedText;
                string beta = System.Net.WebUtility.UrlDecode(alfa);
                scintilla1.ReplaceSelection(beta);
            }
            catch (Exception) {
                MessageBox.Show("Decoding did not work.");
            }
        }

        // ==================================================================
        // UUDEcode
        // ==================================================================

        private void uUDecodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string alfa = scintilla1.SelectedText;
            MemoryStream stocazzo = new MemoryStream();

            using (Stream s = DaStringaAStream(alfa))
            {
                UUDecode(s, stocazzo);
                string result = System.Text.Encoding.UTF8.GetString(stocazzo.ToArray());
                scintilla1.ReplaceSelection(result);
            }
        }


        //===================================
        // Remove empty lines
        //===================================

        private void removeEmptyLinesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            scintilla1.Text = Regex.Replace(scintilla1.Text, @"^\s*$(\n|\r|\r\n)", "", RegexOptions.Multiline);
        }

        //===================================
        // Delete extra spaces
        //===================================

        private void shortenLongSpacesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            scintilla1.Text = Regex.Replace(scintilla1.Text, " +", " ");
        }

        private void copyCtrlCToolStripMenuItem1_Click(object sender, EventArgs e)
        {

        }

        private void iPAddressLookupToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        // ==========================================================================
        public static string ConvertUnixTimestamp(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "";

            if (!long.TryParse(input, out long ts))
                return "";

            try
            {
                DateTime dt = DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime;
                return dt.ToString("yyyy-MM-dd HH:mm:ss UTC");
            }
            catch
            {
                return "";
            }
        }

        //==================================================================

        public static string ConvertLinuxPermission(string input)
        {
            // Deve essere esattamente 3 cifre numeriche
            if (string.IsNullOrWhiteSpace(input) || input.Length != 3)
                return "";

            if (!int.TryParse(input, out int perm))
                return "";

            char[] chars = input.ToCharArray();

            // Ogni cifra deve essere tra 0 e 7
            if (chars.Any(c => c < '0' || c > '7'))
                return "";

            string user = OctalToRwx(chars[0]);
            string group = OctalToRwx(chars[1]);
            string world = OctalToRwx(chars[2]);

            return $"User: {user} Group: {group} World: {world}";
        }

        // =========================================================
        private static string OctalToRwx(char c)
        {
            int val = c - '0';
            return $"{((val & 4) != 0 ? 'r' : '-')}" +
                   $"{((val & 2) != 0 ? 'w' : '-')}" +
                   $"{((val & 1) != 0 ? 'x' : '-')}";
        }

        //===========================================================

        public static string ConvertLinuxPermissionFourDigits(string input)
        {
            // Deve essere esattamente 4 cifre numeriche
            if (string.IsNullOrWhiteSpace(input) || input.Length != 4)
                return "";

            if (!int.TryParse(input, out int perm))
                return "";

            char[] chars = input.ToCharArray();

            // Ogni cifra deve essere tra 0 e 7
            if (chars.Any(c => c < '0' || c > '7'))
                return "";

            // Prima cifra → special bits
            int special = chars[0] - '0';
            bool setuid = (special & 4) != 0;
            bool setgid = (special & 2) != 0;
            bool sticky = (special & 1) != 0;

            string user = OctalToRwxWithSpecialBit(chars[1], setuid, 's');
            string group = OctalToRwxWithSpecialBit(chars[2], setgid, 's');
            string world = OctalToRwxWithSpecialBit(chars[3], sticky, 't');

            // Output completo
            string specialDesc = "";
            if (setuid) specialDesc += "setuid ";
            if (setgid) specialDesc += "setgid ";
            if (sticky) specialDesc += "sticky ";

            specialDesc = specialDesc.Trim();

            if (specialDesc == "")
                specialDesc = "none";

            return $"Special: {specialDesc} User: {user} Group: {group} World: {world}";
        }

        //=======================================================================

        private static string OctalToRwxWithSpecialBit(char c, bool special, char specialChar)
        {
            int val = c - '0';

            char r = (val & 4) != 0 ? 'r' : '-';
            char w = (val & 2) != 0 ? 'w' : '-';
            char x;

            if ((val & 1) != 0)
            {
                x = special ? char.ToLower(specialChar) : 'x';
            }
            else
            {
                x = special ? specialChar : '-';
            }

            return $"{r}{w}{x}";
        }

        // =========================================================================

        public static string ConvertUptimeToReadable(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "";

            // Supporta numeri con o senza decimali
            if (!double.TryParse(input, System.Globalization.NumberStyles.Float,
                                 System.Globalization.CultureInfo.InvariantCulture,
                                 out double seconds))
                return "";

            if (seconds < 0)
                return "";

            // Conversione
            TimeSpan ts = TimeSpan.FromSeconds(seconds);

            int days = ts.Days;
            int hours = ts.Hours;
            int minutes = ts.Minutes;
            int secs = ts.Seconds;

            // Costruzione stringa leggibile
            string result = $"{days} days, {hours} hours, {minutes} minutes, {secs} seconds";
            return result;
        }

        //====================================================
        //  HEXBOX: Copy Text
        //====================================================

        private void CopyHexBoxText()
        {
            try
            {
                // Nessuna selezione?
                if (hexBox2.SelectionLength <= 0)
                {
                    MessageBox.Show("No text selected.", "Info",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // ByteProvider necessario per leggere i bytes
                var provider = hexBox2.ByteProvider;
                if (provider == null)
                {
                    MessageBox.Show("ByteProvider is null.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                int start = (int)hexBox2.SelectionStart;
                int length = (int)hexBox2.SelectionLength;

                byte[] buffer = new byte[length];

                for (int i = 0; i < length; i++)
                {
                    buffer[i] = provider.ReadByte(start + i);
                }

                // Converti byte → ASCII
                string ascii = System.Text.Encoding.ASCII.GetString(buffer);

                // Copia negli appunti
                Clipboard.SetText(ascii);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error copying text:\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //====================================================
        //  HEXBOX: Copy Bytes
        //====================================================

        private void CopyHexBoxBytes()
        {
            try
            {
                if (hexBox2.SelectionLength <= 0)
                {
                    MessageBox.Show("No bytes selected.", "Info",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var provider = hexBox2.ByteProvider;
                if (provider == null)
                {
                    MessageBox.Show("ByteProvider is null.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                int start = (int)hexBox2.SelectionStart;
                int length = (int)hexBox2.SelectionLength;

                StringBuilder sb = new StringBuilder(length * 3);

                for (int i = 0; i < length; i++)
                {
                    byte b = provider.ReadByte(start + i);
                    sb.Append(b.ToString("X2")).Append(' ');
                }

                // Rimuovi spazio finale
                string result = sb.ToString().TrimEnd();

                Clipboard.SetText(result);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error copying bytes:\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //====================================================
        //  HEXBOX: Dump as Binary
        //====================================================

        private void DumpHexBoxSelectionAsBinary()
        {
            try
            {
                if (hexBox2.SelectionLength <= 0)
                {
                    MessageBox.Show("No selection to dump.", "Info",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var provider = hexBox2.ByteProvider;
                if (provider == null)
                {
                    MessageBox.Show("ByteProvider is null.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                int start = (int)hexBox2.SelectionStart;
                int length = (int)hexBox2.SelectionLength;

                // Scegli file di destinazione
                using (SaveFileDialog dlg = new SaveFileDialog())
                {
                    dlg.Title = "Save binary dump";
                    dlg.Filter = "Binary files|*.bin;*.dat|All files|*.*";
                    dlg.FileName = "dump.bin";

                    if (dlg.ShowDialog() != DialogResult.OK)
                        return;

                    // Scrivi i bytes selezionati
                    using (FileStream fs = new FileStream(dlg.FileName, FileMode.Create, FileAccess.Write))
                    {
                        for (int i = 0; i < length; i++)
                        {
                            byte b = provider.ReadByte(start + i);
                            fs.WriteByte(b);
                        }
                    }

                    MessageBox.Show("Binary dump saved!", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error dumping binary:\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //========================================================

        private RichTextBox GetSenderRichTextBox(object sender)
        {
            if (contextMenuStrip6.SourceControl is RichTextBox rtb)
                return rtb;

            return null;
        }

        //========================================================

        //========================================================
    }
}
