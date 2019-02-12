using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace OpenVRNotificationPipe
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainController ctrl = new MainController();
        private Properties.Settings p = Properties.Settings.Default;
        private SolidColorBrush red = new SolidColorBrush(Colors.Tomato);
        private SolidColorBrush green = new SolidColorBrush(Colors.OliveDrab);

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();
            ctrl.statusEventVR += StatusEventVR;
            ctrl.statusEventHTTP += StatusEventHTTP;
            ctrl.SetPort(p.Port);
        }

        private void LoadSettings()
        {
            textBox_Port.Text = p.Port.ToString();
        }

        #region events
        private void StatusEventVR(bool ok, string message, string toolTip)
        {
            StatusEvent(label_StatusVR, ok, message, toolTip);
        }
        private void StatusEventHTTP(bool ok, string message, string toolTip)
        {
            StatusEvent(label_StatusHTTP, ok, message, toolTip);
        }
        private void StatusEvent(Label label, bool ok, string message, string toolTip)
        {
            label.Background = ok ? green : red;
            label.Content = message;
            label.ToolTip = toolTip;
        }
        #endregion

        #region interface
        private void Button_Edit_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new InputDialog(p.Port, "Port:");
            dlg.Owner = this;
            dlg.ShowDialog();
            var result = dlg.DialogResult;
            if(result == true)
            {
                p.Port = dlg.value;
                p.Save();
                textBox_Port.Text = p.Port.ToString();
                ctrl.SetPort(p.Port);
            }
        }
        #endregion

        private void Button_Test_Click(object sender, RoutedEventArgs e)
        {
            System.IO.File.WriteAllText("port.js", $"var port = {p.Port};");
            Process.Start("Example.html");
        }
    }
}
