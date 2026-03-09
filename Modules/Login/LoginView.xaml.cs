using System.Windows;
using System.Windows.Controls;
using GrandFantasiaINIEditor.Modules.Main;

namespace GrandFantasiaINIEditor.Modules.Login
{
    public partial class LoginView : UserControl
    {
        public LoginView()
        {
            InitializeComponent();
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ClientPathBox.Text = dialog.SelectedPath;
            }
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ClientPathBox.Text))
            {
                MessageBox.Show("Selecione a pasta do cliente.");
                return;
            }

            var main = new MainView(ClientPathBox.Text);

            Window.GetWindow(this).Content = main;
        }
    }
}