using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PokemonGoGUI.UI.PluginUI.ShuffleADS
{
    public partial class ShuffleADS_ImportForm : Form
    {
        public string api { get; set; }
        public int amount = 0;

        public ShuffleADS_ImportForm()
        {
            InitializeComponent();
        }

        private void ImportButton_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(AmountTextBox.Text, out amount))
            {
                MessageBox.Show("Please enter amount to import", "Warning");
            }
            api = APITextBox.Text.Trim();
            Close();
            DialogResult = DialogResult.OK;
        }

        private void AmountTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        }
    }
}
