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
        public static MainView Instance { get; private set; }
        private readonly Dictionary<string, UserControl> _viewCache = new();
        private readonly string clientPath;
        private readonly string schemasPath;

        public MainView(string clientPath)
        {
            InitializeComponent();
            Instance = this;
            this.clientPath = clientPath;
            this.schemasPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "ini_schemas.txt");
            
            // Set default view
            Geral_Click(null, null);

            // Preload modules that are heavily cross-referenced
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                GetOrView("Item", () => new GrandFantasiaINIEditor.Modules.Item.ItemView(clientPath, schemasPath));
                GetOrView("ItemMall", () => new GrandFantasiaINIEditor.Modules.ItemMall.ItemMallView(clientPath, schemasPath));
            }));
        }

        private T GetOrView<T>(string key, Func<T> factory) where T : UserControl
        {
            if (!_viewCache.TryGetValue(key, out var view))
            {
                view = factory();
                _viewCache[key] = view;
            }
            return (T)view;
        }

        public void NavigateToEnchant(string enchantId)
        {
            Enchant_Click(null, null);
            var enchantView = MainContent.Content as GrandFantasiaINIEditor.Modules.Enchant.EnchantView;
            enchantView?.SelectEnchant(enchantId);
        }

        public void NavigateToItemWithSearch(string searchText)
        {
            Item_Click(null, null);
            if (MainContent.Content is GrandFantasiaINIEditor.Modules.Item.ItemView view)
            {
                view.SearchBox.Text = searchText;
            }
        }

        public void NavigateToItemMallWithSearch(string searchText)
        {
            ItemMall_Click(null, null);
            if (MainContent.Content is GrandFantasiaINIEditor.Modules.ItemMall.ItemMallView view)
            {
                view.SearchBox.Text = searchText;
            }
        }

        public void SearchEnchantInItems(string enchantId)
        {
            var itemView = GetOrView("Item", () => new GrandFantasiaINIEditor.Modules.Item.ItemView(clientPath, schemasPath));
            var itemMallView = GetOrView("ItemMall", () => new GrandFantasiaINIEditor.Modules.ItemMall.ItemMallView(clientPath, schemasPath));

            if (itemView.HasEnchant(enchantId))
            {
                NavigateToItemWithSearch("!enchant " + enchantId);
            }
            else if (itemMallView.HasEnchant(enchantId))
            {
                NavigateToItemMallWithSearch("!enchant " + enchantId);
            }
            else
            {
                MessageBox.Show("Este Enchant não está sendo referenciado por nenhum item em S_Item nem em S_ItemMall.", "Enchant não encontrado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Geral_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = GetOrView("Geral", () => new GrandFantasiaINIEditor.Modules.Geral.GeralView());
        }

        private void Item_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = GetOrView("Item", () => new GrandFantasiaINIEditor.Modules.Item.ItemView(clientPath, schemasPath));
        }

        private void ItemMall_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = GetOrView("ItemMall", () => new GrandFantasiaINIEditor.Modules.ItemMall.ItemMallView(clientPath, schemasPath));
        }

        private void Mission_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = GetOrView("Mission", () => new GrandFantasiaINIEditor.Modules.Mission.MissionView(clientPath));
        }

        private void Store_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = GetOrView("Store", () => new GrandFantasiaINIEditor.Modules.Store.StoreView(clientPath, schemasPath));
        }

        private void ElfCombine_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = GetOrView("ElfCombine", () => new GrandFantasiaINIEditor.Modules.ElfCombine.ElfCombineView(clientPath, schemasPath));
        }

        private void DropItem_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = GetOrView("DropItem", () => new GrandFantasiaINIEditor.Modules.DropItem.DropItemView(clientPath, schemasPath));
        }

        private void Enchant_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = GetOrView("Enchant", () => new GrandFantasiaINIEditor.Modules.Enchant.EnchantView(clientPath, schemasPath));
        }

        private void Collection_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = GetOrView("Collection", () => new GrandFantasiaINIEditor.Modules.Collection.CollectionView(clientPath, schemasPath));
        }

        private void Npc_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = GetOrView("Npc", () => new GrandFantasiaINIEditor.Modules.Npc.NpcView(clientPath, schemasPath));
        }

        private void Monster_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = GetOrView("Monster", () => new GrandFantasiaINIEditor.Modules.Monster.MonsterView(clientPath, schemasPath));
        }

        private void Scene_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = GetOrView("Scene", () => new GrandFantasiaINIEditor.Modules.Scene.SceneView(clientPath));
        }
    }
}
