using System.Windows;
using System.Windows.Controls;
using GrandFantasiaINIEditor.Modules.Geral;
using GrandFantasiaINIEditor.Modules.Item;
using GrandFantasiaINIEditor.Modules.ItemMall;
using GrandFantasiaINIEditor.Modules.Mission;

namespace GrandFantasiaINIEditor.Modules.Main
{
    public partial class MainView : UserControl
    {
        string clientPath;

        public MainView(string clientPath)
        {
            InitializeComponent();

            this.clientPath = clientPath;

            MainContent.Content = new GeralView();
        }

        private void Geral_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new GeralView();
        }

        private void Item_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new ItemView(clientPath);
        }

        private void ItemMall_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new ItemMallView(clientPath);
        }

        private void Mission_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new MissionView(clientPath);
        }
    }
}