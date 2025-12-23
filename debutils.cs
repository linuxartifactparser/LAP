using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace LAP
{
    public partial class MainClass : Form
    {
        // ===================================================================
        // Banner e contenuto di "debian-binary"
        // ===================================================================
        private void AppendDebianBinarySection(RichTextBox rtb, string content)
        {
            rtb.SelectionColor = Color.Black;
            rtb.SelectionFont = rtb.Font;

            rtb.AppendText("===============================\n");
            rtb.AppendText("CONTENT OF \"debian-binary\" file\n");
            rtb.AppendText("===============================\n\n");

            if (string.IsNullOrWhiteSpace(content))
            {
                rtb.AppendText("(empty or missing)\n\n");
            }
            else
            {
                rtb.AppendText(content);
                if (!content.EndsWith("\n"))
                    rtb.AppendText("\n");
                rtb.AppendText("\n");
            }
        }

        // ===================================================================
        // CONTROL section (nomi + contenuto dei file)
        // ===================================================================
        private void AppendControlSection(
            RichTextBox rtb,
            List<DebParser.ControlFile> controlFiles)
        {
            rtb.SelectionColor = Color.Black;
            rtb.SelectionFont = rtb.Font;

            rtb.AppendText("================================\n");
            rtb.AppendText("\"CONTROL\" section\n");
            rtb.AppendText("================================\n\n");

            if (controlFiles == null || controlFiles.Count == 0)
            {
                rtb.AppendText("(no control files extracted or unsupported compression)\n\n");
                return;
            }

            foreach (var cf in controlFiles)
            {
                // Nome del file in rosso scuro bold
                rtb.SelectionColor = Color.DarkRed;
                rtb.SelectionFont = new Font(rtb.Font, FontStyle.Bold);

                rtb.AppendText(
                    $"        --------------> Content of file: \"{cf.FileName}\"\n\n");

                // Contenuto in verde scuro
                rtb.SelectionColor = Color.DarkGreen;
                rtb.SelectionFont = new Font(rtb.Font, FontStyle.Regular);

                if (!string.IsNullOrEmpty(cf.Content))
                {
                    rtb.AppendText(cf.Content);
                    if (!cf.Content.EndsWith("\n"))
                        rtb.AppendText("\n");
                }
                else
                {
                    rtb.AppendText("(empty file)\n");
                }

                rtb.AppendText("\n");
            }

            // Ripristino
            rtb.SelectionColor = Color.Black;
            rtb.SelectionFont = rtb.Font;
        }

        // ===================================================================
        // DATA section (tree view testuale)
        // ===================================================================
        private void AppendDataSection(
            RichTextBox rtb,
            List<string> dataTree)
        {
            rtb.SelectionColor = Color.Black;
            rtb.SelectionFont = rtb.Font;

            rtb.AppendText("==================================\n");
            rtb.AppendText("\"DATA\" section (no data exported)\n");
            rtb.AppendText("==================================\n\n");

            if (dataTree == null || dataTree.Count == 0)
            {
                rtb.AppendText("(no data entries extracted or unsupported compression)\n\n");
                return;
            }

            foreach (var line in dataTree)
                rtb.AppendText(line + Environment.NewLine);

            rtb.AppendText("\n");
        }
    }
}
