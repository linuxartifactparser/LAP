using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LAP
{
    public partial class MainClass : Form
    {
        //=================================
        // Richtextbox crawler from Unix DateTime
        //=================================
        private void fromUnixDateTimeToolStripMenuItem_Click(object sender, EventArgs e)
        {

            if (string.IsNullOrWhiteSpace(richTextBox2.SelectedText))
            {
                MessageBox.Show("No text selected.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string converted = ConvertUnixTimestamp(richTextBox2.SelectedText);

            if (string.IsNullOrWhiteSpace(converted))
            {
                MessageBox.Show("Not a Unix DateTime format.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            richTextBox2.SelectedText = converted;

            scintilla1.ReplaceSelection(converted);
        }

        //=======================================================

        private void fromBase64ToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            string selectedText = richTextBox2.SelectedText.Trim();

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
                int start = richTextBox2.SelectionStart;
                richTextBox2.SelectedText = decodedString;
                richTextBox2.SelectionStart = start;
                richTextBox2.SelectionLength = decodedString.Length;
            }
            catch
            {
                MessageBox.Show("This is not a valid Base64 string.", "Invalid Base64", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        //=======================================================

        private void hexToSignedIntToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string selectedText = richTextBox2.SelectedText.Trim();

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
                int start = richTextBox2.SelectionStart;
                richTextBox2.SelectedText = result;
                richTextBox2.SelectionStart = start;
                richTextBox2.SelectionLength = result.Length;
            }
            catch
            {
                MessageBox.Show("Not a valid Hex sequence.", "Invalid Hex", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        //=======================================================

        private void hexToUintToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            string selectedText = richTextBox2.SelectedText.Trim();

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
                int start = richTextBox2.SelectionStart;
                richTextBox2.SelectedText = result;
                richTextBox2.SelectionStart = start;
                richTextBox2.SelectionLength = result.Length;
            }
            catch
            {
                MessageBox.Show("Not a valid Hex sequence.", "Invalid Hex", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }


        }

        //=======================================================

        private void hexToCharToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            string selectedText = richTextBox2.SelectedText.Trim();

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
                int start = richTextBox2.SelectionStart;
                richTextBox2.SelectedText = decoded;
                richTextBox2.SelectionStart = start;
                richTextBox2.SelectionLength = decoded.Length;
            }
            catch
            {
                MessageBox.Show("Not a valid Hex sequence.", "Invalid Hex", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        //=======================================================

        private void decToCharToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(richTextBox2.SelectedText))
                return;

            try
            {
                string input = richTextBox2.SelectedText;

                // Normalizza spazi
                input = Regex.Replace(input, @"\s+", " ").Trim();

                string[] groups = input.Split(' ');
                StringBuilder sb = new StringBuilder(groups.Length);

                foreach (string g in groups)
                {
                    if (!int.TryParse(g, out int value))
                        throw new FormatException();

                    if (value < 0 || value > 65535)
                        throw new OverflowException();

                    sb.Append((char)value);
                }

                int selStart = richTextBox2.SelectionStart;
                richTextBox2.SelectedText = sb.ToString();
                richTextBox2.SelectionStart = selStart;
                richTextBox2.SelectionLength = sb.Length;
            }
            catch (FormatException)
            {
                MessageBox.Show(
                    "Decoding error.\nInput must contain decimal values separated by spaces.",
                    "Dec to Char",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            catch (OverflowException)
            {
                MessageBox.Show(
                    "Decoding error.\nOne or more decimal values are outside the valid character range (0–65535).",
                    "Dec to Char",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unexpected error during decoding:\n{ex.Message}",
                    "Dec to Char",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        //=======================================================

        private void escapedUnicodeToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(richTextBox2.SelectedText))
                return;

            try
            {
                string input = richTextBox2.SelectedText;

                // Converte %uXXXX / %UXXXX in \uXXXX per Regex.Unescape
                string normalized = input.Replace("%", "\\");

                string decoded = System.Text.RegularExpressions.Regex.Unescape(normalized);

                int selStart = richTextBox2.SelectionStart;
                richTextBox2.SelectedText = decoded;
                richTextBox2.SelectionStart = selStart;
                richTextBox2.SelectionLength = decoded.Length;
            }
            catch (ArgumentException)
            {
                MessageBox.Show(
                    "Decoding error.\nThe selected text contains malformed or incomplete escaped Unicode sequences.",
                    "Escaped Unicode",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unexpected error during decoding:\n{ex.Message}",
                    "Escaped Unicode",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        //=======================================================

        private void hTMLDecodeToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(richTextBox2.SelectedText))
                return;

            try
            {
                string input = richTextBox2.SelectedText;

                string decoded = System.Net.WebUtility.HtmlDecode(input);

                // Se non cambia nulla, evita una replace inutile
                if (decoded == input)
                    return;

                int selStart = richTextBox2.SelectionStart;
                richTextBox2.SelectedText = decoded;
                richTextBox2.SelectionStart = selStart;
                richTextBox2.SelectionLength = decoded.Length;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"HTML decoding error:\n{ex.Message}",
                    "HTML Decode",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        //=======================================================

        private void uRLDecodeToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(richTextBox2.SelectedText))
                return;

            try
            {
                string input = richTextBox2.SelectedText;

                string decoded = System.Net.WebUtility.UrlDecode(input);

                // Evita replace inutile
                if (decoded == input)
                    return;

                int selStart = richTextBox2.SelectionStart;
                richTextBox2.SelectedText = decoded;
                richTextBox2.SelectionStart = selStart;
                richTextBox2.SelectionLength = decoded.Length;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"URL decoding error:\n{ex.Message}",
                    "URL Decode",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        //=======================================================

        private void uUDecodeToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(richTextBox2.SelectedText))
                return;

            try
            {
                string input = richTextBox2.SelectedText;

                using MemoryStream output = new MemoryStream();
                using Stream inputStream = DaStringaAStream(input);

                UUDecode(inputStream, output);

                output.Position = 0;
                string decoded = Encoding.UTF8.GetString(output.ToArray());

                int selStart = richTextBox2.SelectionStart;
                richTextBox2.SelectedText = decoded;
                richTextBox2.SelectionStart = selStart;
                richTextBox2.SelectionLength = decoded.Length;
            }
            catch (ArgumentNullException ex)
            {
                MessageBox.Show(
                    $"UUDecode error:\n{ex.Message}",
                    "UUDecode",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unexpected UUDecode error:\n{ex.Message}",
                    "UUDecode",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        //=======================================================

        private void linuxPermissions3DigitsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(richTextBox2.SelectedText))
            {
                MessageBox.Show(
                    "No text selected.",
                    "Linux Permissions",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string sel = richTextBox2.SelectedText.Trim();

                string decoded = ConvertLinuxPermission(sel);

                if (string.IsNullOrWhiteSpace(decoded))
                {
                    MessageBox.Show(
                        "The selected text is not a valid 3-digit Linux permission.",
                        "Linux Permissions",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                // Esempio: 700 (User: rwx Group: --- World: ---)
                string final = $"{sel} ({decoded})";

                int selStart = richTextBox2.SelectionStart;
                richTextBox2.SelectedText = final;
                richTextBox2.SelectionStart = selStart;
                richTextBox2.SelectionLength = final.Length;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Linux permissions decoding error:\n{ex.Message}",
                    "Linux Permissions",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        //=======================================================
        private void linuxPermissions4DigitsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(richTextBox2.SelectedText))
            {
                MessageBox.Show(
                    "No text selected.",
                    "Linux Permissions",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string sel = richTextBox2.SelectedText.Trim();

                string decoded = ConvertLinuxPermissionFourDigits(sel);

                if (string.IsNullOrWhiteSpace(decoded))
                {
                    MessageBox.Show(
                        "The selected text is not a valid 4-digit Linux permission.",
                        "Linux Permissions",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                // Esempio: 4755 (SetUID + User/Group/World permissions)
                string final = $"{sel} ({decoded})";

                int selStart = richTextBox2.SelectionStart;
                richTextBox2.SelectedText = final;
                richTextBox2.SelectionStart = selStart;
                richTextBox2.SelectionLength = final.Length;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Linux permissions decoding error:\n{ex.Message}",
                    "Linux Permissions",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        //=======================================================

        private void uptimeIdleSecondsToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(richTextBox2.SelectedText))
            {
                MessageBox.Show(
                    "No text selected.",
                    "Uptime / Idle Time",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string sel = richTextBox2.SelectedText.Trim();

                string decoded = ConvertUptimeToReadable(sel);

                if (string.IsNullOrWhiteSpace(decoded))
                {
                    MessageBox.Show(
                        "Not a valid uptime/idle time.",
                        "Uptime / Idle Time",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                // Sostituisce il valore selezionato con la forma leggibile
                int selStart = richTextBox2.SelectionStart;
                richTextBox2.SelectedText = decoded;
                richTextBox2.SelectionStart = selStart;
                richTextBox2.SelectionLength = decoded.Length;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Uptime decoding error:\n{ex.Message}",
                    "Uptime / Idle Time",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        //=======================================================

        private void copyCtrlCToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            try
            {
                Clipboard.SetText(richTextBox2.SelectedText);
            }
            catch (Exception) {
                MessageBox.Show("Cannot copy.");
            }
        }

        //==================================================
        private void selectAllToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            try
            {
                richTextBox2.SelectAll();
                richTextBox2.Focus();
            }
            catch (Exception) {
                MessageBox.Show("Selection cannot be made.");
            }
        }

        //==================================================

        private void undoCtrlUToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            try
            {
                richTextBox2.Undo();
            }
            catch (Exception)
            {
                MessageBox.Show("An exception occurred.");
            }

        }

        //==================================================

        private void iPAddressLookupToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            string selectedText = richTextBox2.SelectedText.Trim();

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
                    int start = richTextBox2.SelectionStart;
                    int length = richTextBox2.SelectionLength;
                    richTextBox2.SelectedText = hostname;

                    // Mantiene la selezione sul nuovo testo
                    richTextBox2.SelectionStart = start;
                    richTextBox2.SelectionLength = hostname.Length;

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

        //==================================================
        private void exportToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Title = "Export RichTextBox content";
                dlg.Filter = "Text files|*.txt|All files|*.*";
                dlg.FileName = "export.txt";

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        File.WriteAllText(dlg.FileName, richTextBox2.Text);
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



        //=======================================================

    }
}
