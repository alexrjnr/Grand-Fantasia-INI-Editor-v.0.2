using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using GrandFantasiaINIEditor.Core;

namespace GrandFantasiaINIEditor.Modules.Main
{
    public partial class MainView : UserControl
    {
        private readonly string clientPath;
        private readonly string schemasPath;

        public MainView(string clientPath)
        {
            InitializeComponent();
            this.clientPath = clientPath;
            this.schemasPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "ini_schemas.txt");
            
            // Set default view
            MainContent.Content = new GrandFantasiaINIEditor.Modules.Geral.GeralView();
        }

        private void Geral_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new GrandFantasiaINIEditor.Modules.Geral.GeralView();
        }

        private void Item_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new GrandFantasiaINIEditor.Modules.Item.ItemView(clientPath, schemasPath);
        }

        private void ItemMall_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new GrandFantasiaINIEditor.Modules.ItemMall.ItemMallView(clientPath, schemasPath);
        }

        private void Mission_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new GrandFantasiaINIEditor.Modules.Mission.MissionView(clientPath);
        }

        private void Store_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new GrandFantasiaINIEditor.Modules.Store.StoreView(clientPath, schemasPath);
        }

        private void ElfCombine_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new GrandFantasiaINIEditor.Modules.ElfCombine.ElfCombineView(clientPath, schemasPath);
        }

        private void DropItem_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new GrandFantasiaINIEditor.Modules.DropItem.DropItemView(clientPath, schemasPath);
        }

        private void Enchant_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new GrandFantasiaINIEditor.Modules.Enchant.EnchantView(clientPath, schemasPath);
        }

        private void Collection_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new GrandFantasiaINIEditor.Modules.Collection.CollectionView(clientPath, schemasPath);
        }

        private void Npc_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new GrandFantasiaINIEditor.Modules.Npc.NpcView(clientPath, schemasPath);
        }

        private void Monster_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new GrandFantasiaINIEditor.Modules.Monster.MonsterView(clientPath, schemasPath);
        }

        private void Scene_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new GrandFantasiaINIEditor.Modules.Scene.SceneView(clientPath);
        }
    }
}
