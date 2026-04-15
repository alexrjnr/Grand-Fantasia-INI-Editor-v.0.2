using GrandFantasiaINIEditor.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Data;
using System.Threading.Tasks;

namespace GrandFantasiaINIEditor.Modules.Collection
{
    public partial class CollectionView : UserControl
    {
        private readonly string clientPath;
        private readonly string schemasPath;
        private GenericIniDb db;

        private ObservableCollection<CollectionEntry> AllEntries = new();
        private ObservableCollection<CollectionEntry> TopViewEntries = new();
        private ObservableCollection<CollectionEntry> BottomViewEntries = new();
        
        private readonly Dictionary<string, string> itemNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> itemIcons = new(StringComparer.OrdinalIgnoreCase);
        private List<CollectionCategory> categories;
        private readonly object _cacheLock = new();

        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register("IsLoading", typeof(bool), typeof(CollectionView), new PropertyMetadata(false));

        public bool IsLoading
        {
            get => (bool)GetValue(IsLoadingProperty);
            set => SetValue(IsLoadingProperty, value);
        }

        // Column indexes for S_Collection.ini
        private const int IDX_ID = 0;
        private const int IDX_NAME_S = 1;
        private const int IDX_IS_BOTTOM = 2;
        private const int IDX_CATEGORY = 3;
        private const int IDX_SUBCATEGORY = 4;
        private const int IDX_INDEX = 5;
        private const int IDX_POINTS = 6;
        private const int IDX_DESC = 7;
        private const int IDX_LOCATE_LIMIT = 8;

        public CollectionView(string clientPath, string schemasPath)
        {
            InitializeComponent();
            this.clientPath = clientPath;
            this.schemasPath = schemasPath;

            TopItemsList.ItemsSource = TopViewEntries;
            BottomItemsList.ItemsSource = BottomViewEntries;

            InitializeDataAsync();
        }

        private void CategoryTree_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject source = e.OriginalSource as DependencyObject;
            while (source != null && !(source is TreeViewItem))
                source = VisualTreeHelper.GetParent(source);

            if (source is TreeViewItem item)
            {
                item.IsSelected = true;
                item.Focus();
                // We do NOT set e.Handled = true because we want the ContextMenu to still open
            }
        }

        private async void InitializeDataAsync()
        {
            IsLoading = true;
            try
            {
                await Task.Run(() => {
                    LoadItemLookups();
                    LoadDatabases();
                });

                InitCategories();
                
                // Load icons in background without blocking
                _ = Task.Run(LoadIconsInBackground);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void LoadIconsInBackground()
        {
            var entriesWithIcons = AllEntries.ToList();
            
            foreach (var entry in entriesWithIcons)
            {
                if (itemIcons.TryGetValue(entry.Id, out var iconName) && !string.IsNullOrEmpty(iconName))
                {
                    entry.Icon = await GetSingleIconAsync(entry.Id);
                }
            }
        }

        private void LoadItemLookups()
        {
            itemNames.Clear();

            string dataDb = Path.Combine(clientPath, "data", "db");
            if (!Directory.Exists(dataDb)) dataDb = Path.Combine(clientPath, "Data", "db");
            if (!Directory.Exists(dataDb)) dataDb = Path.Combine(clientPath, "Data", "DB");
            if (!Directory.Exists(dataDb)) dataDb = clientPath;

            string dataTrans = Path.Combine(clientPath, "data", "translate");
            if (!Directory.Exists(dataTrans)) dataTrans = Path.Combine(clientPath, "Data", "translate");
            if (!Directory.Exists(dataTrans)) dataTrans = clientPath;

            // T_Item.ini
            LoadNames(Path.Combine(dataTrans, "T_Item.ini"));
            LoadNames(Path.Combine(dataTrans, "T_ItemMall.ini"));

            // S_Item.ini for Icons
            LoadIcons("S_Item.ini");
            LoadIcons("S_ItemMall.ini");
        }

        private void LoadIcons(string fileName)
        {
            try
            {
                var itemDb = GenericIniLoader.Load(clientPath, schemasPath, fileName);
                foreach (var row in itemDb.Rows)
                {
                    if (row.Value.Count > 1 && !string.IsNullOrWhiteSpace(row.Key) && !string.IsNullOrWhiteSpace(row.Value[1]))
                    {
                        itemIcons[row.Key] = row.Value[1].Trim();
                    }
                }
            }
            catch { }
        }

        private void LoadNames(string path)
        {
            if (!File.Exists(path)) return;
            var lines = File.ReadAllLines(path, Encoding.GetEncoding(1252));
            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split('|');
                if (parts.Length < 2) continue;
                string id = parts[0].Trim();
                string name = parts[1].Trim();
                if (!string.IsNullOrEmpty(id)) itemNames[id] = name;
            }
        }

        private void LoadDatabases()
        {
            try
            {
                db = GenericIniLoader.Load(clientPath, schemasPath, "S_Collection.ini", "T_Collection.ini");
                
                // If GenericIniLoader is missing items (due to PipeIniReader SC logic), 
                // we'll try to use a more robust fallback for S_Collection
                AllEntries.Clear();

                // Get robust rows if GenericIniLoader didn't get them all
                var rows = LoadRobustSCollection();

                foreach (var row in rows)
                {
                    if (row.Count < 3) continue;

                    string id = row[IDX_ID];

                    var entry = new CollectionEntry
                    {
                        Id = id,
                        Name = row[IDX_NAME_S],
                        IsBottomWindow = row[IDX_IS_BOTTOM] == "1",
                        CategoryId = int.TryParse(row[IDX_CATEGORY], out var cat) ? cat : 0,
                        SubCategoryId = int.TryParse(row[IDX_SUBCATEGORY], out var sub) ? sub : (int?)null,
                        Index = int.TryParse(row[IDX_INDEX], out var idx) ? idx : 0,
                        Points = int.TryParse(row[IDX_POINTS], out var pts) ? pts : 0,
                        Description = row.Count > 7 ? row[IDX_DESC] : "",
                        LocateLimit = row.Count > 8 ? row[IDX_LOCATE_LIMIT] : ""
                    };

                    if (itemNames.TryGetValue(entry.Id, out var itemName))
                        entry.ItemNameFromTItem = itemName;
                    else
                        entry.ItemNameFromTItem = "Unknown Item " + entry.Id;

                    // Icon will be loaded in background
                    
                    AllEntries.Add(entry);
                }

                Dispatcher.Invoke(() => UpdateStats());
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show("Erro ao carregar bancos de dados: " + ex.Message));
            }
        }

        private List<List<string>> LoadRobustSCollection()
        {
            string dataDb = Path.Combine(clientPath, "data", "db");
            if (!Directory.Exists(dataDb)) dataDb = Path.Combine(clientPath, "Data", "db");
            if (!Directory.Exists(dataDb)) dataDb = Path.Combine(clientPath, "Data", "DB");
            if (!Directory.Exists(dataDb)) dataDb = clientPath;

            string path = Path.Combine(dataDb, "S_Collection.ini");
            var results = new List<List<string>>();
            if (!File.Exists(path)) return results;

            var lines = File.ReadAllLines(path, Encoding.GetEncoding(950));
            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split('|').Select(p => p.Trim()).ToList();
                if (parts.Count > 0 && !string.IsNullOrEmpty(parts[0]))
                    results.Add(parts);
            }
            return results;
        }

        private void InitCategories()
        {
            categories = CollectionDefinitions.GetCategories();
            CategoryTree.ItemsSource = categories;
            
            // Expand first category and select it to trigger initial load
            if (categories.Count > 0)
            {
                categories[0].IsExpanded = true;
                Dispatcher.BeginInvoke(new Action(() => {
                    var item = CategoryTree.ItemContainerGenerator.ContainerFromItem(categories[0]) as TreeViewItem;
                    if (item != null) item.IsSelected = true;
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }


        private bool _suppressSelection = false;
        private void ItemsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelection) return;
            var list = sender as ListBox;
            if (list == null || list.SelectedItem == null) return;

            var entry = list.SelectedItem as CollectionEntry;
            if (entry == null) return;

            // Clear other list selection to avoid confusion
            _suppressSelection = true;
            if (list == TopItemsList) BottomItemsList.SelectedIndex = -1;
            else TopItemsList.SelectedIndex = -1;
            _suppressSelection = false;

            // Populate Edit Panel
            SelectedItemName.Text = $"{entry.Id} - {entry.ItemNameFromTItem}";
            SelectedItemDesc.Text = entry.Description;
            SelectedIcon.Source = entry.Icon;
        }

        private void ItemsList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var list = sender as ListBox;
            if (list == null || list.SelectedItem == null) return;

            var entry = list.SelectedItem as CollectionEntry;
            if (entry != null)
            {
                EditItem(entry);
            }
        }

        private void EditItem(CollectionEntry entry)
        {
            var editWindow = new CollectionAddWindow(entry.CategoryId, entry.SubCategoryId, entry.Index, entry);
            if (editWindow.ShowDialog() == true)
            {
                var updated = editWindow.NewEntry;
                entry.Id = updated.Id;
                entry.Name = updated.Name;
                entry.IsBottomWindow = updated.IsBottomWindow;
                entry.Points = updated.Points;
                entry.Description = updated.Description;

                // Update Lookups if ID changed (rare)
                if (itemNames.TryGetValue(entry.Id, out var itemName))
                    entry.ItemNameFromTItem = itemName;
                
                // Load icon directly (sync for single edit is fine)
                entry.Icon = GetSingleIcon(entry.Id);

                FilterItems();
            }
        }

        private void CategoryTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            FilterItems();
        }

        private void FilterItems()
        {
            TopViewEntries.Clear();
            BottomViewEntries.Clear();

            var selected = CategoryTree.SelectedItem;
            if (selected == null) return;

            IEnumerable<CollectionEntry> filtered;

            if (selected is CollectionCategory cat)
            {
                // Rule: Items only show in category if it has NO subcategories
                if (cat.SubCategories != null && cat.SubCategories.Count > 0)
                {
                    UpdateStats();
                    return;
                }
                filtered = AllEntries.Where(x => x.CategoryId == cat.Id && (x.SubCategoryId == null || x.SubCategoryId == 0));
            }
            else if (selected is CollectionSubCategory sub)
            {
                filtered = AllEntries.Where(x => x.CategoryId == sub.ParentCategoryId && x.SubCategoryId == sub.Id);
            }
            else
            {
                UpdateStats();
                return;
            }

            foreach (var item in filtered.OrderBy(x => x.Index))
            {
                if (item.IsBottomWindow)
                    BottomViewEntries.Add(item);
                else
                    TopViewEntries.Add(item);
            }

            UpdateStats();
        }

        private void UpdateStats()
        {
            int topPoints = TopViewEntries.Sum(x => x.Points);
            int bottomPoints = BottomViewEntries.Sum(x => x.Points);
            
            TopScoreLabel.Text = $"Collection Score: {topPoints} point(s)";
            BottomScoreLabel.Text = $"Collection Score: {bottomPoints} point(s)";
            
            TotalItemsLabel.Text = $"Total Items: {AllEntries.Count}";
            TotalScoreLabel.Text = $"Total Score: {AllEntries.Sum(x => x.Points)}";
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            // Calculate next index for current category
            var selected = CategoryTree.SelectedItem;
            int catId = 0;
            int? subId = null;

            if (selected is CollectionCategory c) catId = c.Id;
            else if (selected is CollectionSubCategory s) { catId = s.ParentCategoryId; subId = s.Id; }

            if (catId == 0)
            {
                MessageBox.Show("Selecione uma categoria primeiro.");
                return;
            }

            int nextIdx = 1;
            var sameCatItems = AllEntries.Where(x => x.CategoryId == catId && x.SubCategoryId == subId);
            if (sameCatItems.Any())
            {
                nextIdx = sameCatItems.Max(x => x.Index) + 1;
            }

            // Simple input dialog implementation
            var addWindow = new CollectionAddWindow(catId, subId, nextIdx);
            if (addWindow.ShowDialog() == true)
            {
                var newEntry = addWindow.NewEntry;
                
                // Fetch name and icon from lookups
                if (itemNames.TryGetValue(newEntry.Id, out var itemName))
                    newEntry.ItemNameFromTItem = itemName;
                else
                    newEntry.ItemNameFromTItem = "Unknown Item " + newEntry.Id;

                newEntry.Icon = GetSingleIcon(newEntry.Id);

                AllEntries.Add(newEntry);
                FilterItems();
            }
        }

        private void BatchAddButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = CategoryTree.SelectedItem;
            int catId = 0;
            int? subId = null;

            if (selected is CollectionCategory c) catId = c.Id;
            else if (selected is CollectionSubCategory s) { catId = s.ParentCategoryId; subId = s.Id; }

            // Open window with categories and current selection
            var batchWin = new CollectionBatchAddWindow(categories.ToList(), catId, subId);
            batchWin.Owner = Window.GetWindow(this);
            if (batchWin.ShowDialog() == true)
            {
                BatchProcessIds(batchWin);
            }
        }

        private void BatchProcessIds(CollectionBatchAddWindow win)
        {
            var ids = win.ResultIds;
            int catId = win.SelectedCategoryId;
            int? subId = win.SelectedSubCategoryId;

            int nextIndex = 1;
            var sameCatItems = AllEntries.Where(x => x.CategoryId == catId && x.SubCategoryId == subId).ToList();
            if (sameCatItems.Any())
            {
                nextIndex = sameCatItems.Max(x => x.Index) + 1;
            }

            int addedCount = 0;
            var newlyAdded = new List<CollectionEntry>();

                foreach (var id in ids)
                {

                    var entry = new CollectionEntry
                    {
                        Id = id,
                        Name = win.BatchName,
                        IsBottomWindow = win.IsBottom,
                        CategoryId = catId,
                        SubCategoryId = subId,
                        Index = nextIndex++,
                        Points = win.BatchPoints,
                        Description = win.BatchDescription,
                        LocateLimit = ""
                    };

                    // Auto-fill ItemNameFromTItem for UI display (tooltip/name lookup)
                    if (itemNames.TryGetValue(id, out var itemName))
                        entry.ItemNameFromTItem = itemName;
                    else
                        entry.ItemNameFromTItem = "Unknown Item " + id;

                    newlyAdded.Add(entry);
                    AllEntries.Add(entry);
                    addedCount++;
                }

                if (addedCount > 0)
                {
                    UpdateStats();
                    FilterItems();
                    
                    // Load icons in background for newly added items
                    _ = Task.Run(() => {
                        foreach (var entry in newlyAdded)
                        {
                            var icon = GetSingleIcon(entry.Id);
                            if (icon != null)
                            {
                                Dispatcher.Invoke(() => entry.Icon = icon);
                            }
                        }
                    });

                    MessageBox.Show($"{addedCount} itens adicionados com sucesso!");
                }
        }

        private void RemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = AllEntries.Where(x => x.IsSelected).ToList();

            if (selected.Count == 0)
            {
                MessageBox.Show("Selecione pelo menos um item via checkbox para remover.");
                return;
            }

            if (MessageBox.Show($"Deseja remover {selected.Count} item(ns)?", "Confirmar Remoção", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                foreach (var item in selected) AllEntries.Remove(item);
                
                FilterItems();
            }
        }

        private void SelectCategory_Click(object sender, RoutedEventArgs e) => PerformSelectCategory(sender, null, true);
        private void UnselectCategory_Click(object sender, RoutedEventArgs e) => PerformSelectCategory(sender, null, false);
        private void SelectCategoryTop_Click(object sender, RoutedEventArgs e) => PerformSelectCategory(sender, false, true);
        private void SelectCategoryBottom_Click(object sender, RoutedEventArgs e) => PerformSelectCategory(sender, true, true);

        private ContextMenu GetContextMenuFromMenuItem(MenuItem mi)
        {
            DependencyObject obj = mi;
            while (obj != null && !(obj is ContextMenu))
            {
                obj = LogicalTreeHelper.GetParent(obj) ?? VisualTreeHelper.GetParent(obj);
            }
            return obj as ContextMenu;
        }

        private void PerformSelectCategory(object sender, bool? isBottom, bool select)
        {
            var mi = sender as MenuItem;
            var cat = mi?.CommandParameter as CollectionCategory;
            
            if (cat == null)
            {
                // Fallback for extreme cases, but CommandParameter should be primary
                cat = mi?.DataContext as CollectionCategory;
                if (cat == null)
                {
                    var cm = GetContextMenuFromMenuItem(mi);
                    if (cm != null && cm.PlacementTarget is FrameworkElement fe)
                        cat = (fe.Tag as CollectionCategory) ?? (fe.DataContext as CollectionCategory);
                }
            }

            if (cat == null)
            {
                cat = CategoryTree.SelectedItem as CollectionCategory;
            }

            if (cat == null)
            {
                MessageBox.Show("Erro: Não foi possível identificar a categoria. Por favor, clique (com o botão esquerdo) antes de tentar usar o menu.");
                return;
            }

            // Seleciona TUDO da categoria, inclusive o que estiver nas subcategorias!
            var query = AllEntries.Where(x => x.CategoryId == cat.Id);
            
            // If selecting for a specific window
            if (isBottom != null)
                query = query.Where(x => x.IsBottomWindow == isBottom.Value);

            var targets = query.ToList();
            foreach (var item in targets) item.IsSelected = select;
            
            string win = isBottom == null ? "em ambas as janelas" : (isBottom.Value ? "na janela Bottom" : "na janela Top");
            string action = select ? "selecionados" : "desmarcados";
            MessageBox.Show($"{targets.Count} itens {action} {win} para a categoria '{cat.Name}' (ID {cat.Id}).");
        }

        private void SelectSubCategory_Click(object sender, RoutedEventArgs e) => PerformSelectSubCategory(sender, null, true);
        private void UnselectSubCategory_Click(object sender, RoutedEventArgs e) => PerformSelectSubCategory(sender, null, false);
        private void SelectSubCategoryTop_Click(object sender, RoutedEventArgs e) => PerformSelectSubCategory(sender, false, true);
        private void SelectSubCategoryBottom_Click(object sender, RoutedEventArgs e) => PerformSelectSubCategory(sender, true, true);

        private void PerformSelectSubCategory(object sender, bool? isBottom, bool select)
        {
            var mi = sender as MenuItem;
            var sub = mi?.CommandParameter as CollectionSubCategory;
            
            if (sub == null)
            {
                // Fallback
                sub = mi?.DataContext as CollectionSubCategory;
                if (sub == null)
                {
                    var cm = GetContextMenuFromMenuItem(mi);
                    if (cm != null && cm.PlacementTarget is FrameworkElement fe)
                        sub = (fe.Tag as CollectionSubCategory) ?? (fe.DataContext as CollectionSubCategory);
                }
            }

            if (sub == null)
            {
                sub = CategoryTree.SelectedItem as CollectionSubCategory;
            }

            if (sub == null)
            {
                MessageBox.Show("Erro: Não foi possível identificar a subcategoria. Por favor, clique (com o botão esquerdo) na subcategoria antes de tentar usar o menu direito.");
                return;
            }

            // Robust search for subcategory
            var query = AllEntries.Where(x => x.CategoryId == sub.ParentCategoryId && 
                                             (x.SubCategoryId == sub.Id || (sub.Id == 0 && (x.SubCategoryId == null || x.SubCategoryId == 0))));
            
            if (isBottom != null)
                query = query.Where(x => x.IsBottomWindow == isBottom.Value);

            var targets = query.ToList();
            foreach (var item in targets) item.IsSelected = select;
            
            string win = isBottom == null ? "em ambas as janelas" : (isBottom.Value ? "na janela Bottom" : "na janela Top");
            string action = select ? "selecionados" : "desmarcados";
            MessageBox.Show($"{targets.Count} itens {action} {win} para a subcategoria '{sub.Name}' (ID {sub.Id}).");
        }

        private void SaveAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Save S_Collection.ini (BIG5)
                string sPath = db.FilePath;
                SaveSCollection(sPath);
                
                // Save C_Collection.ini (Same folder as S_Collection.ini per user feedback)
                string cPath = Path.Combine(Path.GetDirectoryName(sPath), "C_Collection.ini");

                SaveSCollection(cPath);
                
                // Save T_Collection.ini (ANSI/UTF-8 with accents)

                MessageBox.Show("Alterações salvas com sucesso no Cliente (C_) e Servidor (S_)!");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao salvar: " + ex.Message);
            }
        }

        private void SaveSCollection(string path)
        {
            // Ensure directory exists
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var big5 = Encoding.GetEncoding(950);
            var sb = new StringBuilder();
            
            // Header Preservation
            if (!string.IsNullOrEmpty(db.VersionLine)) 
                sb.AppendLine(db.VersionLine);
            
            if (!string.IsNullOrEmpty(db.ColumnHeader)) 
                sb.AppendLine(db.ColumnHeader);
            else 
                sb.AppendLine("ID|Name|IsBottom|Category|SubCategory|Index|Points|Description|LocateLimit|");

            foreach (var entry in AllEntries)
            {
                string nameSafe = StripAccents(entry.Name);
                string descSafe = StripAccents(entry.Description);

                sb.Append(entry.Id).Append("|");
                sb.Append(nameSafe).Append("|");
                sb.Append(entry.IsBottomWindow ? "1" : "").Append("|");
                sb.Append(entry.CategoryId).Append("|");
                sb.Append(entry.SubCategoryId?.ToString() ?? "").Append("|");
                sb.Append(entry.Index).Append("|");
                sb.Append(entry.Points).Append("|");
                sb.Append(descSafe).Append("|");
                sb.Append(entry.LocateLimit).AppendLine("|");
            }

            File.WriteAllText(path, sb.ToString(), big5);
        }

        private void SaveTCollection()
        {
            var ansi = Encoding.GetEncoding(1252);
            var sb = new StringBuilder();
            
            // Header (Assuming 2 columns based on schema)
            sb.AppendLine("ID|Name|");

            foreach (var entry in AllEntries)
            {
                sb.Append(entry.Id).Append("|");
                sb.Append(entry.Name).AppendLine("|");
            }

            if (!string.IsNullOrEmpty(db.TranslationFilePath))
            {
                File.WriteAllText(db.TranslationFilePath, sb.ToString(), ansi);
            }
        }

        private string StripAccents(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }

        private async Task<BitmapSource> GetSingleIconAsync(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            if (!itemIcons.TryGetValue(itemId, out var iconName) || string.IsNullOrEmpty(iconName)) return null;

            string[] folders = {
                Path.Combine(clientPath, "data", "icon"),
                Path.Combine(clientPath, "Data", "icon"),
                Path.Combine(clientPath, "UI", "itemicon"),
                Path.Combine(clientPath, "ui", "itemicon"),
                Path.Combine(clientPath, "UI", "Icon"),
                Path.Combine(clientPath, "ui", "icon"),
                Path.Combine(clientPath, "data", "item"),
                Path.Combine(clientPath, "Data", "item"),
                Path.Combine(clientPath, "ui", "item"),
                Path.Combine(clientPath, "UI", "item")
            };

            foreach (var folder in folders)
            {
                if (Directory.Exists(folder))
                {
                    string path = Path.Combine(folder, iconName + ".dds");
                    if (File.Exists(path)) return await DdsLoader.LoadAsync(path);
                }
            }
            return null;
        }

        private BitmapSource GetSingleIcon(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            if (!itemIcons.TryGetValue(itemId, out var iconName) || string.IsNullOrEmpty(iconName)) return null;

            string[] folders = {
                Path.Combine(clientPath, "data", "icon"),
                Path.Combine(clientPath, "Data", "icon"),
                Path.Combine(clientPath, "UI", "itemicon"),
                Path.Combine(clientPath, "ui", "itemicon"),
                Path.Combine(clientPath, "data", "item"),
                Path.Combine(clientPath, "Data", "item")
            };

            foreach (var folder in folders)
            {
                if (Directory.Exists(folder))
                {
                    string path = Path.Combine(folder, iconName + ".dds");
                    if (File.Exists(path)) return DdsLoader.Load(path);
                }
            }
            return null;
        }
    }

    // A simple window for adding items
    public class CollectionAddWindow : Window
    {
        public CollectionEntry NewEntry { get; private set; }
        
        private TextBox idBox, nameBox, pointsBox, descBox;
        private CheckBox isBottomCheck;
        private int catId;
        private int? subId;
        private int nextIdx;

        public CollectionAddWindow(int catId, int? subId, int nextIdx, CollectionEntry existing = null)
        {
            this.catId = catId;
            this.subId = subId;
            this.nextIdx = existing?.Index ?? nextIdx;
            
            Title = existing == null ? "Add Collection Item" : "Edit Collection Item";
            Width = 400;
            Height = 450;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30));

            var stack = new StackPanel { Margin = new Thickness(20) };
            Content = stack;

            stack.Children.Add(new TextBlock { Text = "Item ID:", Foreground = System.Windows.Media.Brushes.White });
            idBox = new TextBox { Margin = new Thickness(0, 5, 0, 10), Text = existing?.Id ?? "" };
            stack.Children.Add(idBox);

            stack.Children.Add(new TextBlock { Text = "Name (Internal):", Foreground = System.Windows.Media.Brushes.White });
            nameBox = new TextBox { Margin = new Thickness(0, 5, 0, 10), Text = existing?.Name ?? "" };
            stack.Children.Add(nameBox);

            stack.Children.Add(new TextBlock { Text = "Points:", Foreground = System.Windows.Media.Brushes.White });
            pointsBox = new TextBox { Margin = new Thickness(0, 5, 0, 10), Text = (existing?.Points ?? 1).ToString() };
            stack.Children.Add(pointsBox);

            stack.Children.Add(new TextBlock { Text = "Description:", Foreground = System.Windows.Media.Brushes.White });
            descBox = new TextBox { Height = 60, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 10), Text = existing?.Description ?? "" };
            stack.Children.Add(descBox);

            isBottomCheck = new CheckBox 
            { 
                Content = "Show in Bottom Window (Item Mall)", 
                Foreground = System.Windows.Media.Brushes.White, 
                Margin = new Thickness(0, 5, 0, 20),
                IsChecked = existing?.IsBottomWindow ?? false
            };
            stack.Children.Add(isBottomCheck);

            var btnStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okBtn = new Button { Content = existing == null ? "Add" : "Update", Width = 80, Margin = new Thickness(0, 0, 10, 0) };
            okBtn.Click += (s, e) => {
                if (string.IsNullOrWhiteSpace(idBox.Text)) { MessageBox.Show("ID is required."); return; }
                NewEntry = new CollectionEntry {
                    Id = idBox.Text,
                    Name = nameBox.Text,
                    IsBottomWindow = isBottomCheck.IsChecked == true,
                    CategoryId = catId,
                    SubCategoryId = subId,
                    Index = nextIdx,
                    Points = int.TryParse(pointsBox.Text, out var p) ? p : 1,
                    Description = descBox.Text
                };
                DialogResult = true;
            };
            btnStack.Children.Add(okBtn);
            
            var cancelBtn = new Button { Content = "Cancel", Width = 80 };
            cancelBtn.Click += (s, e) => DialogResult = false;
            btnStack.Children.Add(cancelBtn);
            
            stack.Children.Add(btnStack);
        }
    }
}
