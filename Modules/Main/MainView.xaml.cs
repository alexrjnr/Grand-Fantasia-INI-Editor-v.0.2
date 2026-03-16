using System.Windows;
using System.Windows.Controls;
using GrandFantasiaINIEditor.Modules.Geral;
using GrandFantasiaINIEditor.Modules.Item;
using GrandFantasiaINIEditor.Modules.ItemMall;
using GrandFantasiaINIEditor.Modules.Mission;
using GrandFantasiaINIEditor.Modules.Store;

namespace GrandFantasiaINIEditor.Modules.Main
{
    public partial class MainView : UserControl
    {
        string clientPath;
        string schemasPath;

        public MainView(string clientPath)
        {
            InitializeComponent();

            this.clientPath = clientPath;
            this.schemasPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Data", "ini_schemas.txt");

            MainContent.Content = new GeralView();
        }

        private void Geral_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new GeralView();
        }

        private void Item_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new ItemView(clientPath, schemasPath);
        }

        private void ItemMall_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new ItemMallView(clientPath, schemasPath);
        }

        private void Mission_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new MissionView(clientPath);
        }

        private void Store_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new StoreView(clientPath, schemasPath);
        }
    }
}