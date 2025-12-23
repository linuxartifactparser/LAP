using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LAP
{
    public partial class MainClass : Form
    {
        //====================================================

        private void AppendColored(
    RichTextBox rtb,
    string label,
    string value,
    Color labelColor,
    Color valueColor,
    bool newLine = true)
        {
            // Label
            rtb.SelectionStart = rtb.TextLength;
            rtb.SelectionLength = 0;
            rtb.SelectionColor = labelColor;
            rtb.AppendText(label);

            // Value
            rtb.SelectionStart = rtb.TextLength;
            rtb.SelectionLength = 0;
            rtb.SelectionColor = valueColor;
            rtb.AppendText(value);

            if (newLine)
                rtb.AppendText(Environment.NewLine);

            // Reset
            rtb.SelectionColor = rtb.ForeColor;
        }

        //====================================================

        private void AppendGreenBanner(RichTextBox box, string title)
        {
            string line = new string('=', 38);

            box.SelectionColor = Color.Green;
            box.AppendText(Environment.NewLine + line + Environment.NewLine);
            box.AppendText("|" + title.PadLeft((title.Length + 34) / 2).PadRight(34) + Environment.NewLine);
            box.AppendText(line + Environment.NewLine + Environment.NewLine);

            box.SelectionColor = Color.Black;
        }

        // ==================================================
    }
}
