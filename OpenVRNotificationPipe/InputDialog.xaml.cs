using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace OpenVRNotificationPipe
{
    public partial class InputDialog : Window
    {
        public int value;

        public InputDialog(int value, string labelText)
        {
            this.value = value;
            InitializeComponent();
            labelValue.Content = labelText;
            textBoxValue.Text = value.ToString();
            textBoxValue.Focus();
            textBoxValue.SelectAll();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = Int32.TryParse(textBoxValue.Text, out value);
        }
    }
}
