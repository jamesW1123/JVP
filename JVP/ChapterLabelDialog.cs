using System.Drawing;
using System.Windows.Forms;

namespace JVP
{
    // Small prompt shown when marking a chapter; built in code so it needs no designer file
    internal class ChapterLabelDialog : Form
    {
        private TextBox txtLabel;

        public string label
        {
            get { return txtLabel.Text.Trim(); }
        }

        public ChapterLabelDialog(string defaultLabel)
        {
            Text = "Mark Chapter";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(300, 95);

            Label lblPrompt = new Label() { Text = "Chapter label:", Location = new Point(12, 12), AutoSize = true };
            txtLabel = new TextBox() { Text = defaultLabel, Location = new Point(12, 32), Width = 276 };
            Button btnOK = new Button() { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(132, 62) };
            Button btnCancel = new Button() { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(213, 62) };

            Controls.AddRange(new Control[] { lblPrompt, txtLabel, btnOK, btnCancel });
            AcceptButton = btnOK;
            CancelButton = btnCancel;
            Shown += (s, e) => txtLabel.SelectAll();
        }
    }
}
