using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using GrandFantasiaINIEditor.Core;
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
                MessageBox.Show(
                    LocalizationManager.Instance.GetLocalizedString("Login.Messages.SelectFolder") ?? "Selecione a pasta do cliente.",
                    LocalizationManager.Instance.GetLocalizedString("Common.Warning") ?? "Aviso",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var main = new MainView(ClientPathBox.Text);

            Window.GetWindow(this).Content = main;
        }

        private void BtnLang_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string langCode)
            {
                LocalizationManager.Instance.CurrentLanguage = langCode;
                
                // Force UI refresh by recreating the initial login view
                Window.GetWindow(this).Content = new LoginView();
            }
        }
    }
}