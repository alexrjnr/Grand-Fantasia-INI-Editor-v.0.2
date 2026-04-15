using GrandFantasiaINIEditor.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GrandFantasiaINIEditor.Modules.Store
{
    public partial class StoreView : UserControl
    {
        private readonly string clientPath;
        private readonly string schemasPath;
        private GenericIniDb db;

        private readonly ObservableCollection<StoreEntry> Entries = new();
        private readonly Dictionary<string, string> itemIcons = new(StringComparer.OrdinalIgnoreCase);
        private readonly ObservableCollection<StoreSlotView> LeftSlots = new();
        private readonly ObservableCollection<StoreSlotView> RightSlots = new();

        private readonly Dictionary<string, string> _itemNames = new(StringComparer.OrdinalIgnoreCase);

        private bool _loading;
        private bool _suppressSelectionChanged;

        private List<string> _currentRow;
        private string _lastSelectedStoreId;
        private List<string> _originalRowSnapshot;

        private int _currentPage;

        private CancellationTokenSource _searchCts;
        private CancellationTokenSource _slotLookupCts;

        private const string STORE_FILE_NAME = "S_Store.ini";

        private const int IDX_ID = 0;
        private const int IDX_DISCOUNT = 1;

        // 0 = id, 1 = desconto, depois comeÃ§a itemId/nomeBruto/qtd
        private const int FIRST_SLOT_COLUMN = 2;
        private const int SLOT_STRIDE = 3;
        private const int ITEMS_PER_PAGE = 10;

        public StoreView(string clientPath, string schemasPath)
        {
            InitializeComponent();

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            this.clientPath = clientPath;
            this.schemasPath = schemasPath;

            StoreList.ItemsSource = Entries;
            LeftSlotsList.ItemsSource = LeftSlots;
            RightSlotsList.ItemsSource = RightSlots;

            PopulateCombos();
            LoadItemLookups();
            LoadDatabases();
            UpdatePageUi();
        }

        private sealed class StoreEntry : INotifyPropertyChanged
        {
            private string _id;
            private string _name;
            private BitmapSource _icon;
            private string _discount;
            private string _discountText;

            public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
            public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
            public BitmapSource Icon { get => _icon; set { _icon = value; OnPropertyChanged(); } }
            public string Discount { get => _discount; set { _discount = value; OnPropertyChanged(); } }
            public string DiscountText { get => _discountText; set { _discountText = value; OnPropertyChanged(); } }

            public event PropertyChangedEventHandler PropertyChanged;
            private void OnPropertyChanged([CallerMemberName] string name = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }



        private sealed class ComboOption
        {
            public int Value { get; set; }
            public string Label { get; set; }

            public override string ToString() => $"{Label} ({Value})";
        }

        private sealed class StoreSlotView : INotifyPropertyChanged
        {
            private string _itemId;
            private string _quantity;
            private string _visualName;
            private string _visualInfo;
            private BitmapSource _icon;

            public int SlotIndex { get; set; }
            public int BaseColumnIndex { get; set; }

            public string SlotLabel => $"Slot {SlotIndex}";

            public string ItemId
            {
                get => _itemId;
                set
                {
                    if (_itemId == value)
                        return;

                    _itemId = value;
                    OnPropertyChanged(nameof(ItemId));
                }
            }

            public string Quantity
            {
                get => _quantity;
                set
                {
                    if (_quantity == value)
                        return;

                    _quantity = value;
                    OnPropertyChanged(nameof(Quantity));
                }
            }

            public string VisualName
            {
                get => _visualName;
                set
                {
                    if (_visualName == value)
                        return;

                    _visualName = value;
                    OnPropertyChanged(nameof(VisualName));
                }
            }

            public string VisualInfo
            {
                get => _visualInfo;
                set
                {
                    if (_visualInfo == value)
                        return;

                    _visualInfo = value;
                    OnPropertyChanged(nameof(VisualInfo));
                }
            }

            public BitmapSource Icon
            {
                get => _icon;
                set
                {
                    if (_icon == value)
                        return;

                    _icon = value;
                    OnPropertyChanged(nameof(Icon));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            private void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private readonly Dictionary<int, string> ALIGNMENT_DISCOUNT = new()
        {
            { 0, "None" },
            { 1, "City01" },
            { 2, "City02" },
            { 3, "City03" },
            { 4, "City04" },
            { 5, "City05" },
            { 6, "City06" },
            { 7, "City07" },
            { 8, "City08" },
            { 9, "City09" },
            { 10, "City10" },
            { 11, "Group01" },
            { 12, "Group02" },
            { 13, "Group03" },
            { 14, "Group04" },
            { 15, "Group05" },
            { 16, "Group06" },
            { 17, "Group07" },
            { 18, "Group08" },
            { 19, "Group09" },
            { 20, "Group10" },
            { 21, "Elf01" },
            { 22, "Elf02" },
            { 23, "Elf03" },
            { 24, "Elf04" },
            { 25, "Elf05" },
            { 26, "Elf06" },
            { 27, "Elf07" },
            { 28, "Elf08" },
            { 29, "Elf09" },
            { 30, "Elf10" },
            { 31, "Elf11" },
            { 32, "Elf12" },
            { 33, "Elf13" },
            { 34, "Elf14" },
            { 35, "Elf15" },
            { 36, "Elf16" },
            { 100, "PKPalace" },
            { 200, "Group11" },
            { 201, "Group12" },
            { 202, "Group13" },
            { 203, "Group14" },
            { 204, "Group15" },
            { 299, "GroupEnd" },
            { 300, "End" },
        };

        private void PopulateCombos()
        {
            if (DiscountCombo != null)
            {
                var discountTypes = LocalizationManager.Instance.GetDictionary("Store.DiscountTypes");
                if (discountTypes != null && discountTypes.Count > 0)
                {
                    DiscountCombo.ItemsSource = discountTypes
                        .Select(kv => new ComboOption
                        {
                            Value = int.TryParse(kv.Key, out int v) ? v : 0,
                            Label = kv.Value
                        })
                        .OrderBy(x => x.Value)
                        .ToList();
                }
                else
                {
                    DiscountCombo.ItemsSource = ALIGNMENT_DISCOUNT
                        .Select(kv => new ComboOption
                        {
                            Value = kv.Key,
                            Label = kv.Value
                        })
                        .OrderBy(x => x.Value)
                        .ToList();
                }
            }
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

        private void LoadDatabases()
        {
            db = GenericIniLoader.Load(clientPath, schemasPath, "S_Store.ini");
            Entries.Clear();

            foreach (var r in db.Rows.OrderBy(x => x.Key, Comparer<string>.Create(CompareStoreIds)))
            {
                string discountRaw = GetValue(r.Value, IDX_DISCOUNT);
                
                string firstItem = "";
                for (int s = FIRST_SLOT_COLUMN; s + SLOT_STRIDE - 1 < r.Value.Count; s += SLOT_STRIDE)
                {
                    if (!string.IsNullOrWhiteSpace(r.Value[s])) { firstItem = r.Value[s]; break; }
                }

                string nameTemplate = LocalizationManager.Instance.GetLocalizedString("Store.Legends.NameFormat") ?? "Loja {0}";
                var entry = new StoreEntry
                {
                    Id = r.Key,
                    Name = string.Format(nameTemplate, r.Key),
                    Icon = null,
                    Discount = discountRaw,
                    DiscountText = GetDiscountDisplayText(discountRaw)
                };
                Entries.Add(entry);

                if (!string.IsNullOrEmpty(firstItem))
                {
                    _ = LoadIconForEntryAsync(entry, firstItem);
                }
            }
        }

        private async Task LoadIconForEntryAsync(StoreEntry entry, string itemId)
        {
            entry.Icon = await LoadIconByItemIdAsync(itemId);
        }




        private List<string> NormalizeRowLength(List<string> row, int requiredColumns)
        {
            var result = row.Select(x => x?.Trim() ?? string.Empty).ToList();

            while (result.Count < requiredColumns)
                result.Add(string.Empty);

            if (result.Count > requiredColumns)
                result = result.Take(requiredColumns).ToList();

            return result;
        }

        private void LoadItemLookups()
        {
            _itemNames.Clear();
            itemIcons.Clear();

            LoadItemTranslations();
            LoadItemIconMappings();
        }

        private void LoadItemIconMappings()
        {
            LoadIcons("S_Item.ini");
            LoadIcons("S_ItemMall.ini");
        }

        private void LoadItemTranslations()
        {
            string tPath = Path.Combine(clientPath, "data", "translate", "T_Item.ini");
            if (!File.Exists(tPath))
                return;

            var rawLines = File.ReadAllLines(tPath, Encoding.GetEncoding(1252));

            string currentId = null;
            string currentName = null;

            void Flush()
            {
                if (!string.IsNullOrWhiteSpace(currentId) && !_itemNames.ContainsKey(currentId))
                    _itemNames[currentId] = currentName?.Trim() ?? string.Empty;
            }

            for (int i = 1; i < rawLines.Length; i++)
            {
                string line = rawLines[i] ?? string.Empty;

                int firstPipe = line.IndexOf('|');
                bool startsWithId =
                    firstPipe > 0 &&
                    int.TryParse(line.Substring(0, firstPipe).Trim(), out _);

                if (!startsWithId)
                    continue;

                Flush();

                var parts = line.Split(new[] { '|' }, 3);
                currentId = parts.Length > 0 ? parts[0].Trim() : string.Empty;
                currentName = parts.Length > 1 ? parts[1].Trim() : string.Empty;
            }

            Flush();
        }


        private static int CompareStoreIds(string a, string b)
        {
            if (int.TryParse(a, out var ai) && int.TryParse(b, out var bi))
                return ai.CompareTo(bi);

            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetBoxText(TextBox box)
        {
            return box?.Text?.Trim() ?? string.Empty;
        }

        private static string GetValue(List<string> row, int index)
        {
            if (row == null || index < 0 || index >= row.Count)
                return string.Empty;

            return row[index]?.Trim() ?? string.Empty;
        }

        private static void SetText(TextBox box, string value)
        {
            if (box != null)
                box.Text = value ?? string.Empty;
        }

        private void SelectDiscountByValue(string raw)
        {
            if (DiscountCombo == null)
                return;

            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                DiscountCombo.SelectedIndex = -1;
                return;
            }

            foreach (var item in DiscountCombo.Items)
            {
                if (item is ComboOption option && option.Value == value)
                {
                    DiscountCombo.SelectedItem = option;
                    return;
                }
            }

            DiscountCombo.SelectedIndex = -1;
        }

        private string GetDiscountValueOrKeepOriginal()
        {
            if (DiscountCombo?.SelectedItem is ComboOption option)
                return option.Value.ToString(CultureInfo.InvariantCulture);

            if (_currentRow != null)
                return GetValue(_currentRow, IDX_DISCOUNT);

            return string.Empty;
        }

        private string GetDiscountDisplayText(string raw)
        {
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                var discountTypes = LocalizationManager.Instance.GetDictionary("Store.DiscountTypes");
                if (discountTypes != null && discountTypes.TryGetValue(value.ToString(), out var localizedLabel))
                    return $"{localizedLabel} ({value})";

                if (ALIGNMENT_DISCOUNT.TryGetValue(value, out var label))
                    return $"{label} ({value})";
            }

            return string.IsNullOrWhiteSpace(raw) ? "None (0)" : raw;
        }

        private int GetTotalSlotCount()
        {
            if (db == null || db.Schema.Columns <= FIRST_SLOT_COLUMN)
                return 0;

            return (db.Schema.Columns - FIRST_SLOT_COLUMN) / SLOT_STRIDE;
        }

        private void RenderCurrentPage()
        {
            LeftSlots.Clear();
            RightSlots.Clear();

            if (_currentRow == null)
            {
                UpdatePageUi();
                return;
            }

            int totalSlots = GetTotalSlotCount();
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalSlots / (double)ITEMS_PER_PAGE));

            if (_currentPage < 0)
                _currentPage = 0;

            if (_currentPage >= totalPages)
                _currentPage = totalPages - 1;

            int startSlot = _currentPage * ITEMS_PER_PAGE;
            int endSlotExclusive = Math.Min(startSlot + ITEMS_PER_PAGE, totalSlots);

            var pageSlots = new List<StoreSlotView>();

            for (int slotZeroBased = startSlot; slotZeroBased < endSlotExclusive; slotZeroBased++)
            {
                int baseCol = FIRST_SLOT_COLUMN + (slotZeroBased * SLOT_STRIDE);

                if (_currentRow.Count <= baseCol + 2)
                {
                    while (_currentRow.Count <= baseCol + 2)
                        _currentRow.Add(string.Empty);
                }

                string itemId = GetValue(_currentRow, baseCol);
                string quantity = GetValue(_currentRow, baseCol + 2);

                string visualName;
                string visualInfo;
                if (string.IsNullOrWhiteSpace(itemId))
                {
                    visualName = LocalizationManager.Instance.GetLocalizedString("Store.Legends.EmptySlotName") ?? "(vazio)";
                    visualInfo = LocalizationManager.Instance.GetLocalizedString("Store.Legends.EmptySlotInfo") ?? "Slot livre";
                }
                else
                {
                    if (_itemNames.TryGetValue(itemId, out var translatedName) && !string.IsNullOrWhiteSpace(translatedName))
                        visualName = translatedName;
                    else
                        visualName = string.Format(LocalizationManager.Instance.GetLocalizedString("Store.Legends.VisualNameFallback") ?? "Item {0}", itemId);
                    
                    visualInfo = itemId; // Or some description if available
                }

                var view = new StoreSlotView
                {
                    SlotIndex = slotZeroBased + 1,
                    BaseColumnIndex = baseCol,
                    ItemId = itemId,
                    Quantity = quantity,
                    VisualName = visualName,
                    VisualInfo = visualInfo,
                    Icon = null
                };


                if (!string.IsNullOrWhiteSpace(itemId))
                {
                    _ = LoadIconForSlotAsync(view, itemId);
                }

                pageSlots.Add(view);
            }

            while (pageSlots.Count < ITEMS_PER_PAGE && (startSlot + pageSlots.Count) < totalSlots)
            {
                int slotZeroBased = startSlot + pageSlots.Count;
                int baseCol = FIRST_SLOT_COLUMN + (slotZeroBased * SLOT_STRIDE);

                pageSlots.Add(new StoreSlotView
                {
                    SlotIndex = slotZeroBased + 1,
                    BaseColumnIndex = baseCol,
                    ItemId = string.Empty,
                    Quantity = string.Empty,
                    VisualName = LocalizationManager.Instance.GetLocalizedString("Store.Legends.EmptySlotName") ?? "(vazio)",
                    VisualInfo = LocalizationManager.Instance.GetLocalizedString("Store.Legends.EmptySlotInfo") ?? "Slot livre",
                    Icon = null
                });
            }

            for (int i = 0; i < pageSlots.Count; i++)
            {
                if (i < 5)
                    LeftSlots.Add(pageSlots[i]);
                else
                    RightSlots.Add(pageSlots[i]);
            }

            UpdatePageUi();
        }

        private void UpdatePageUi()
        {
            int totalSlots = GetTotalSlotCount();
            int totalPages = Math.Max(1, (int)Math.Ceiling(Math.Max(0, totalSlots) / (double)ITEMS_PER_PAGE));

            if (PageInfoText != null)
            {
                string template = LocalizationManager.Instance.GetLocalizedString("Store.Legends.PageInfo") ?? "Página {0} / {1}";
                PageInfoText.Text = string.Format(template, _currentPage + 1, totalPages);
            }

            if (StoreSummaryText != null)
            {
                string template = LocalizationManager.Instance.GetLocalizedString("Store.Legends.Summary") ?? "Slots: {0}";
                StoreSummaryText.Text = totalSlots > 0 ? string.Format(template, totalSlots) : string.Empty;
            }

            if (PrevPageButton != null)
                PrevPageButton.IsEnabled = _currentPage > 0;

            if (NextPageButton != null)
                NextPageButton.IsEnabled = _currentPage < totalPages - 1;
        }

        private async Task LoadIconForSlotAsync(StoreSlotView slot, string itemId)
        {
            slot.Icon = await LoadIconByItemIdAsync(itemId);
        }

        private async Task<BitmapSource> LoadIconByItemIdAsync(string itemId)
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

        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (db == null)
                return;

            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            string filter = (SearchBox.Text ?? string.Empty).Trim();

            try
            {
                var result = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();

                    return db.Rows
                        .Where(x => StoreRowMatchesFilter(x.Key, x.Value, filter))
                        .OrderBy(x => x.Key, Comparer<string>.Create(CompareStoreIds))
                        .Select(x =>
                        {
                            string firstItem = "";
                            for (int s = FIRST_SLOT_COLUMN; s + SLOT_STRIDE - 1 < x.Value.Count; s += SLOT_STRIDE)
                            {
                                if (!string.IsNullOrWhiteSpace(x.Value[s])) { firstItem = x.Value[s]; break; }
                            }
                            var storeEntry = new StoreEntry
                            {
                                Id = x.Key,
                                Name = $"Loja {x.Key}",
                                Icon = null,
                                Discount = GetValue(x.Value, IDX_DISCOUNT),
                                DiscountText = GetDiscountDisplayText(GetValue(x.Value, IDX_DISCOUNT))
                            };
                            if (!string.IsNullOrEmpty(firstItem))
                            {
                                _ = LoadIconForEntryAsync(storeEntry, firstItem);
                            }
                            return storeEntry;
                        })
                        .ToList();
                }, token);

                Entries.Clear();
                foreach (var item in result)
                    Entries.Add(item);
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>
        /// Returns true if the store row matches the filter string.
        /// Checks: store ID, discount label, and â€“ for every slot â€“ item ID and item name.
        /// </summary>
        private bool StoreRowMatchesFilter(string storeId, List<string> row, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            // Store ID
            if (storeId.Contains(filter, StringComparison.OrdinalIgnoreCase))
                return true;

            // Discount label
            if (GetDiscountDisplayText(GetValue(row, IDX_DISCOUNT))
                    .Contains(filter, StringComparison.OrdinalIgnoreCase))
                return true;

            // Each slot: itemId at FIRST_SLOT_COLUMN + s*SLOT_STRIDE
            int totalCols = row.Count;
            for (int start = FIRST_SLOT_COLUMN; start + SLOT_STRIDE - 1 < totalCols; start += SLOT_STRIDE)
            {
                string slotItemId = GetValue(row, start);
                if (string.IsNullOrWhiteSpace(slotItemId)) continue;

                // Match on raw item ID
                if (slotItemId.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    return true;

                // Match on resolved item name from T_Item
                if (_itemNames.TryGetValue(slotItemId, out var name) &&
                    name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private void StoreList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionChanged)
                return;

            if (StoreList.SelectedItem is not StoreEntry entry)
                return;

            if (_lastSelectedStoreId != null && _lastSelectedStoreId != entry.Id)
            {
                if (!ConfirmSaveIfNeeded())
                {
                    _suppressSelectionChanged = true;
                    StoreList.SelectedItem = Entries.FirstOrDefault(x => x.Id == _lastSelectedStoreId);
                    _suppressSelectionChanged = false;
                    return;
                }
            }

            if (!db.Rows.TryGetValue(entry.Id, out var rowFromDb))
                return;

            _currentRow = NormalizeRowLength(rowFromDb, db.Schema.Columns);
            _loading = true;

            try
            {
                SetText(StoreIdBox, GetValue(_currentRow, IDX_ID));
                SelectDiscountByValue(GetValue(_currentRow, IDX_DISCOUNT));

                _currentPage = 0;
                RenderCurrentPage();

                _lastSelectedStoreId = entry.Id;
                _originalRowSnapshot = BuildEditedRowFromControls();
            }
            finally
            {
                _loading = false;
            }
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentRow == null)
                return;

            CopyVisibleSlotsToCurrentRow();

            if (_currentPage <= 0)
                return;

            _currentPage--;
            RenderCurrentPage();
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentRow == null)
                return;

            CopyVisibleSlotsToCurrentRow();

            int totalPages = Math.Max(1, (int)Math.Ceiling(GetTotalSlotCount() / (double)ITEMS_PER_PAGE));
            if (_currentPage >= totalPages - 1)
                return;

            _currentPage++;
            RenderCurrentPage();
        }

        private void CopyVisibleSlotsToCurrentRow()
        {
            if (_currentRow == null)
                return;

            SetRowValue(IDX_ID, GetBoxText(StoreIdBox));
            SetRowValue(IDX_DISCOUNT, GetDiscountValueOrKeepOriginal());

            foreach (var slot in LeftSlots.Concat(RightSlots))
                CopySlotToRow(slot);
        }

        private void CopySlotToRow(StoreSlotView slot)
        {
            if (_currentRow == null || slot == null)
                return;

            int baseCol = slot.BaseColumnIndex;

            SetRowValue(baseCol, slot.ItemId ?? string.Empty);
            // Ignore column + 1 as it's the raw name which shouldn't be overridden with empty string maliciously
            SetRowValue(baseCol + 2, slot.Quantity ?? string.Empty);
        }

        private void SetRowValue(int idx, string value)
        {
            if (_currentRow == null)
                return;

            while (_currentRow.Count <= idx)
                _currentRow.Add(string.Empty);

            _currentRow[idx] = value ?? string.Empty;
        }

        private void SetListValue(List<string> row, int idx, string value)
        {
            while (row.Count <= idx)
                row.Add(string.Empty);

            row[idx] = value ?? string.Empty;
        }

        private List<string> BuildEditedRowFromControls()
        {
            var row = _currentRow != null
                ? new List<string>(_currentRow)
                : new List<string>();

            row = NormalizeRowLength(row, db.Schema.Columns);

            SetListValue(row, IDX_ID, GetBoxText(StoreIdBox));
            SetListValue(row, IDX_DISCOUNT, GetDiscountValueOrKeepOriginal());

            foreach (var slot in LeftSlots.Concat(RightSlots))
            {
                int baseCol = slot.BaseColumnIndex;
                SetListValue(row, baseCol, slot.ItemId ?? string.Empty);
                // Keep baseCol + 1 intact
                SetListValue(row, baseCol + 2, slot.Quantity ?? string.Empty);
            }

            row = NormalizeRowLength(row, db.Schema.Columns);

            return row;
        }

        private HashSet<int> GetChangedColumnIndices(List<string> originalRow, List<string> editedRow)
        {
            var changed = new HashSet<int>();
            int max = Math.Max(originalRow?.Count ?? 0, editedRow?.Count ?? 0);

            for (int i = 0; i < max; i++)
            {
                string original = i < (originalRow?.Count ?? 0) ? (originalRow[i] ?? string.Empty) : string.Empty;
                string edited = i < (editedRow?.Count ?? 0) ? (editedRow[i] ?? string.Empty) : string.Empty;

                if (!string.Equals(original, edited, StringComparison.Ordinal))
                    changed.Add(i);
            }

            return changed;
        }

        private void PatchIniRowInPlace(string path, Encoding encoding, string oldStoreId, string newStoreId, HashSet<int> changedColumns, List<string> editedRow)
        {
            if (!File.Exists(path) || changedColumns == null || changedColumns.Count == 0)
                return;

            var lines = File.ReadAllLines(path, encoding);

            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split(new[] { '|' }, StringSplitOptions.None).ToList();

                if (parts.Count == 0)
                    continue;

                string lineId = (parts[0] ?? string.Empty).Trim();
                if (!string.Equals(lineId, oldStoreId, StringComparison.Ordinal))
                    continue;

                int needed = Math.Max(parts.Count, editedRow.Count);
                while (parts.Count < needed)
                    parts.Add(string.Empty);

                foreach (int idx in changedColumns)
                {
                    string newValue = idx < editedRow.Count ? (editedRow[idx] ?? string.Empty) : string.Empty;
                    parts[idx] = newValue;
                }

                if (!string.IsNullOrWhiteSpace(newStoreId))
                    parts[IDX_ID] = newStoreId;

                while (parts.Count < db.Schema.Columns)
                    parts.Add(string.Empty);

                if (parts.Count > db.Schema.Columns)
                    parts = parts.Take(db.Schema.Columns).ToList();

                string rawLine = lines[i];
                lines[i] = string.Join("|", parts);

                // Garantir que o pipe final seja preservado se existia na linha original
                if (rawLine.TrimEnd().EndsWith("|") && !lines[i].EndsWith("|"))
                {
                    lines[i] += "|";
                }

                File.WriteAllLines(path, lines, encoding);
                return;
            }

            throw new InvalidOperationException($"Store {oldStoreId} não foi encontrada em {Path.GetFileName(path)}.");
        }

        private string CaptureOriginalBlock(string path, Encoding encoding, string storeId)
        {
            if (!File.Exists(path)) return null;

            var lines = File.ReadAllLines(path, encoding);
            var block = new StringBuilder();
            bool found = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int pipe = line.IndexOf('|');
                bool startsWithId = pipe > 0 && int.TryParse(line.Substring(0, pipe).Trim(), out _);

                if (startsWithId)
                {
                    string currentId = line.Substring(0, pipe).Trim();
                    if (currentId == storeId)
                    {
                        found = true;
                        block.AppendLine(line);
                        continue;
                    }
                    else if (found)
                    {
                        break;
                    }
                }
                else if (found)
                {
                    block.AppendLine(line);
                }
            }

            return found ? block.ToString().TrimEnd('\r', '\n') : null;
        }

        private void AppendClonedBlock(string path, Encoding encoding, string oldId, string newId)
        {
            if (!File.Exists(path)) return;

            string block = CaptureOriginalBlock(path, encoding, oldId);
            if (string.IsNullOrEmpty(block)) return;

            // Substituir o ID na primeira linha do bloco
            int pipe = block.IndexOf('|');
            if (pipe > 0)
            {
                string newBlock = newId + block.Substring(pipe);
                
                // Garantir que há uma quebra de linha antes de anexar se o arquivo não terminar com uma
                string content = File.ReadAllText(path, encoding);
                using var sw = new StreamWriter(path, true, encoding);
                if (!string.IsNullOrEmpty(content) && !content.EndsWith("\n") && !content.EndsWith("\r"))
                {
                    sw.WriteLine();
                }
                sw.WriteLine(newBlock);
            }
        }

        private bool HasCurrentStoreChanges()
        {
            if (_currentRow == null || _originalRowSnapshot == null)
                return false;

            var editedRow = BuildEditedRowFromControls();
            int max = Math.Max(editedRow.Count, _originalRowSnapshot.Count);

            for (int i = 0; i < max; i++)
            {
                string a = i < editedRow.Count ? (editedRow[i] ?? string.Empty) : string.Empty;
                string b = i < _originalRowSnapshot.Count ? (_originalRowSnapshot[i] ?? string.Empty) : string.Empty;

                if (!string.Equals(a, b, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private bool ConfirmSaveIfNeeded()
        {
            if (!HasCurrentStoreChanges())
                return true;

            var result = MessageBox.Show(
                LocalizationManager.Instance.GetLocalizedString("Store.Dialogs.Modified") ?? "Esta loja foi modificada. Deseja salvar antes de trocar para outra loja?",
                LocalizationManager.Instance.GetLocalizedString("Store.Dialogs.SaveTitle") ?? "Salvar alterações",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
                return false;

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    SaveCurrentStoreFiles();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        string.Format(LocalizationManager.Instance.GetLocalizedString("Store.Dialogs.ErrorSaving") ?? "Erro ao salvar arquivos:\n\n{0}", ex.Message),
                        LocalizationManager.Instance.GetLocalizedString("Common.Error") ?? "Erro",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return false;
                }
            }

            return true;
        }

        private void SaveCurrentStoreFiles()
        {
            if (_currentRow == null)
                return;

            CopyVisibleSlotsToCurrentRow();

            string oldId = _lastSelectedStoreId ?? GetValue(_currentRow, IDX_ID);
            if (string.IsNullOrWhiteSpace(oldId))
                return;

            var editedRow = BuildEditedRowFromControls();
            string newId = GetValue(editedRow, IDX_ID);

            if (string.IsNullOrWhiteSpace(newId))
                throw new InvalidOperationException(LocalizationManager.Instance.GetLocalizedString("Store.Dialogs.IdEmpty") ?? "O ID da Loja não pode ficar vazio.");

            if (!string.Equals(oldId, newId, StringComparison.Ordinal) && db.Rows.ContainsKey(newId))
                throw new InvalidOperationException(string.Format(LocalizationManager.Instance.GetLocalizedString("Store.Dialogs.IdExists") ?? "Já existe uma Loja com ID {0}.", newId));

            var changedColumns = GetChangedColumnIndices(_originalRowSnapshot, editedRow);

            string cStorePath = Path.Combine(clientPath, "data", "db", "C_Store.ini");

            PatchIniRowInPlace(db.FilePath, Encoding.GetEncoding(950), oldId, newId, changedColumns, editedRow);
            if (File.Exists(cStorePath))
                PatchIniRowInPlace(cStorePath, Encoding.GetEncoding(950), oldId, newId, changedColumns, editedRow);
            
            // SaveDataIni redundante removido para evitar perda de formatação (pipes)

            if (!string.Equals(oldId, newId, StringComparison.Ordinal))
                db.Rows.Remove(oldId);

            _currentRow = editedRow;
            db.Rows[newId] = new List<string>(editedRow);

            _originalRowSnapshot = editedRow.Select(x => x ?? string.Empty).ToList();
            _lastSelectedStoreId = newId;

            LoadDatabases();

            _suppressSelectionChanged = true;
            StoreList.SelectedItem = Entries.FirstOrDefault(x => x.Id == newId);
            _suppressSelectionChanged = false;

            StoreList.SelectedItem = Entries.FirstOrDefault(x => x.Id == newId);
        }

        private async void SlotItemId_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading)
                return;

            if (sender is not TextBox tb || tb.Tag is not StoreSlotView slot)
                return;

            slot.ItemId = tb.Text?.Trim() ?? string.Empty;
            CopySlotToRow(slot);

            _slotLookupCts?.Cancel();
            _slotLookupCts = new CancellationTokenSource();
            var token = _slotLookupCts.Token;

            string itemId = slot.ItemId;

            if (string.IsNullOrWhiteSpace(itemId))
            {
                slot.VisualName = LocalizationManager.Instance.GetLocalizedString("Store.Legends.EmptySlotName") ?? "(vazio)";
                slot.VisualInfo = LocalizationManager.Instance.GetLocalizedString("Store.Legends.EmptySlotInfo") ?? "Slot livre";
                slot.Icon = null;
                return;
            }

            try
            {
                var result = await Task.Run(async () =>
                {
                    token.ThrowIfCancellationRequested();

                    string name = _itemNames.TryGetValue(itemId, out var translatedName) && !string.IsNullOrWhiteSpace(translatedName)
                        ? translatedName
                        : string.Format(LocalizationManager.Instance.GetLocalizedString("Store.Legends.VisualNameFallback") ?? "Item {0}", itemId);

                    BitmapSource icon = await LoadIconByItemIdAsync(itemId);

                    return (name, icon);
                }, token);

                if (!string.Equals(slot.ItemId, itemId, StringComparison.Ordinal))
                    return;

                slot.VisualName = result.name;
                slot.VisualInfo = string.IsNullOrWhiteSpace(slot.Quantity)
                    ? $"ID {itemId}"
                    : string.Format(LocalizationManager.Instance.GetLocalizedString("Store.Legends.VisualInfoFormat") ?? "ID {0} | Qtd {1}", itemId, slot.Quantity);
                slot.Icon = result.icon;
            }
            catch (OperationCanceledException)
            {
            }
        }


        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveCurrentStoreFiles();

                MessageBox.Show(
                    LocalizationManager.Instance.GetLocalizedString("Store.Dialogs.SaveSuccess") ?? "Alterações salvas com sucesso em S_Store.ini.",
                    LocalizationManager.Instance.GetLocalizedString("Common.Save") ?? "Salvar",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(LocalizationManager.Instance.GetLocalizedString("Store.Dialogs.ErrorSaving") ?? "Erro ao salvar arquivos:\n\n{0}", ex.Message),
                    LocalizationManager.Instance.GetLocalizedString("Common.Error") ?? "Erro",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Clone_Click(object sender, RoutedEventArgs e)
        {
            if (StoreList.SelectedItem is not StoreEntry selected || db == null)
                return;

            if (!db.Rows.TryGetValue(selected.Id, out var row))
                return;

            var dlg = new InputDialog(
                LocalizationManager.Instance.GetLocalizedString("Store.Dialogs.ClonePrompt") ?? "Informe o novo ID:", 
                LocalizationManager.Instance.GetLocalizedString("Store.Dialogs.CloneTitle") ?? "Clonar loja") 
                { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.InputText))
                return;

            string newId = dlg.InputText.Trim();
            if (db.Rows.ContainsKey(newId))
            {
                MessageBox.Show(
                    LocalizationManager.Instance.GetLocalizedString("Store.Dialogs.IdAlreadyExists") ?? "Esse ID já existe. Escolha outro ID.", 
                    LocalizationManager.Instance.GetLocalizedString("Store.Dialogs.CloneTitle") ?? "Clonar loja", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Warning);
                return;
            }

            var clonedRow = new List<string>(row);
            while (clonedRow.Count < db.Schema.Columns)
                clonedRow.Add(string.Empty);

            clonedRow[IDX_ID] = newId;
            db.Rows[newId] = clonedRow;

            try
            {
                string sStorePath = Path.Combine(clientPath, "data", "db", STORE_FILE_NAME);
                string cStorePath = Path.Combine(clientPath, "data", "db", "C_Store.ini");
                
                // Usar anexo de bloco para preservar formatação original em ambos os arquivos
                AppendClonedBlock(sStorePath, Encoding.GetEncoding(950), selected.Id, newId);
                if (File.Exists(cStorePath))
                    AppendClonedBlock(cStorePath, Encoding.GetEncoding(950), selected.Id, newId);

                // Executar atualização de banco e ícones de forma segura na thread principal
                LoadDatabases();

                string dataDb = Path.Combine(clientPath, "data", "db");
                if (!Directory.Exists(dataDb)) dataDb = Path.Combine(clientPath, "Data", "db");
                
                LoadIcons(Path.Combine(dataDb, "S_Item.ini"));
                LoadIcons(Path.Combine(dataDb, "S_ItemMall.ini"));
                _suppressSelectionChanged = true;
                StoreList.SelectedItem = Entries.FirstOrDefault(x => x.Id == newId);
                _suppressSelectionChanged = false;

                MessageBox.Show(
                    string.Format(LocalizationManager.Instance.GetLocalizedString("Store.Dialogs.CloneSuccess") ?? "Loja clonada com sucesso para o ID {0}.", newId), 
                    LocalizationManager.Instance.GetLocalizedString("Store.Dialogs.CloneTitle") ?? "Clonar loja", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(LocalizationManager.Instance.GetLocalizedString("Store.Dialogs.ErrorSaving") ?? "Erro ao salvar arquivos:\n\n{0}", ex.Message), 
                    LocalizationManager.Instance.GetLocalizedString("Common.Error") ?? "Erro", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
        }

        private void SaveDataIni(string path, Encoding encoding)
        {
            string header = string.Empty;
            if (File.Exists(path))
            {
                using var sr = new StreamReader(path, encoding, detectEncodingFromByteOrderMarks: false);
                header = sr.ReadLine() ?? string.Empty;
            }

            var lines = new List<string>();
            if (!string.IsNullOrEmpty(header))
                lines.Add(header);

            foreach (var kv in db.Rows.OrderBy(x => x.Key, Comparer<string>.Create(CompareStoreIds)))
            {
                var row = NormalizeRowLength(kv.Value, db.Schema.Columns);
                lines.Add(string.Join("|", row));
            }

            using var sw = new StreamWriter(path, false, encoding);
            foreach (var line in lines)
                sw.WriteLine(line);
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfirmSaveIfNeeded())
                return;

            LoadItemLookups();
            LoadDatabases();

            _currentRow = null;
            _originalRowSnapshot = null;
            _lastSelectedStoreId = null;
            _currentPage = 0;

            LeftSlots.Clear();
            RightSlots.Clear();

            SetText(StoreIdBox, string.Empty);
            if (DiscountCombo != null)
                DiscountCombo.SelectedIndex = -1;

            UpdatePageUi();
        }
    }
}
