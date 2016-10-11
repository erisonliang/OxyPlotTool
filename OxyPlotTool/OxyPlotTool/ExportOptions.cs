using System;
using System.Windows.Forms;

namespace OxyPlotTool
{
	public partial class ExportOptions : Form
	{
		public ExportOptions()
		{
			InitializeComponent();
		}

		public object SelectedObject
		{
			get { return propertyGrid.SelectedObject; }
			set { propertyGrid.SelectedObject = value; }
		}

		private void buttonOk_Click(object sender, EventArgs e)
		{
			DialogResult = DialogResult.OK;
			Close();
		}

		private void buttonCancel_Click(object sender, EventArgs e)
		{
			DialogResult = DialogResult.Cancel;
			Close();
		}
	}
}
