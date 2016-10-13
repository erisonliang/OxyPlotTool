using System;
using System.Windows.Forms;

namespace OxyPlotTool
{
    public partial class ColumnName : Form
    {
        public ColumnName()
        {
            InitializeComponent();
        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        public string NameValue
        {
            get { return textBoxName.Text; }
            set { textBoxName.Text = value; }
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
