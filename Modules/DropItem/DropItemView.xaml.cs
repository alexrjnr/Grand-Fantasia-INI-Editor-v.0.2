using GrandFantasiaINIEditor.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace GrandFantasiaINIEditor.Modules.DropItem
{
    public partial class DropItemView : UserControl, INotifyPropertyChanged
    {
        private readonly string clientPath;
        private readonly string schemasPath;
        private GenericIniDb db;

        private readonly ObservableCollection<DropEntry> Entries = new();
        private readonly List<DropItemSlot> AllSlots = new();
        private readonly ObservableCollection<DropItemSlot> CurrentPageSlots = new();
        private readonly ObservableCollection<DropItemSlot> PbSlots = new();

        private readonly Dictionary<string, string> _itemNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _monsterNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _monsterIcons = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _itemIconsLookup = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, BitmapSource> _iconCache = new(StringComparer.OrdinalIgnoreCase);

        private bool _loading;
        private int _currentPage = 1;
        private const int ITEMS_PER_PAGE = 8;
        private const int TOTAL_PAGES = 5;
        private List<string> _currentRow;
        private List<string> _originalRowSnapshot;
        private string _lastSelectedId;
        private bool _suppressSelectionChanged;

        private CancellationTokenSource _searchCts;

        private const int IDX_ID = 0;
        private const int IDX_NAME = 1;
        private const int IDX_LEVEL = 2;
        private const int IDX_GOLD_RATE = 3;
        private const int IDX_AVG_GOLD = 4;
        private const int IDX_RAND_GOLD = 5;
        private const int IDX_RAND_TIMES = 6;
        private const int IDX_NOT_DROP = 7;
        private const int IDX_GREEN = 8;
        private const int IDX_BLUE = 9;
        private const int IDX_ORANGE = 10;
        private const int IDX_YELLOW = 11;
        private const int IDX_PURPLE = 12;
        private const int START_DROPS = 13;
        private const int START_PB = 173;
        private const int IDX_MARQUEE = 193;

        public DropItemView(string clientPath, string schemasPath)
        {
            InitializeComponent();
            DataContext = this;

            this.clientPath = clientPath;
            this.schemasPath = schemasPath;

            DropList.ItemsSource = Entries;
            DropSlotsControl.ItemsSource = CurrentPageSlots;
            PbSlotsControl.ItemsSource = PbSlots;

            Loaded += async (s, e) => await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                _loading = true;
                await Task.Run(() => LoadReferenceData());
                await LoadMainDatabase();
            }
            finally
            {
                _loading = false;
            }
        }

        private void LoadReferenceData()
        {
            Encoding chi = Encoding.GetEncoding(1252);


            // 2. Item Names (T_Item.ini)
            string tItemPath = Path.Combine(clientPath, "data", "translate", "T_Item.ini");
            if (File.Exists(tItemPath))
            {
                var lines = File.ReadAllLines(tItemPath, chi);
                string currentId = null;
                var nameBuilder = new StringBuilder();

                void Flush()
                {
                    if (currentId != null) _itemNames[currentId] = nameBuilder.ToString().Trim();
                    nameBuilder.Clear();
                }

                foreach (var line in lines.Skip(1))
                {
                    if (string.IsNullOrEmpty(line)) continue;
                    int pipe = line.IndexOf('|');
                    if (pipe > 0 && int.TryParse(line.Substring(0, pipe), out _))
                    {
                        Flush();
                        var parts = line.Split('|');
                        currentId = parts[0];
                        nameBuilder.Append(parts.Length > 1 ? parts[1] : "");
                    }
                    else if (currentId != null)
                    {
                        // Desativado: Não capturar descrição (linhas extras)
                        // nameBuilder.AppendLine();
                        // nameBuilder.Append(line);
                    }
                }
                Flush();
            }

            // 3. Monster Names (T_Monster.ini)
            string tMonPath = Path.Combine(clientPath, "data", "translate", "T_Monster.ini");
            if (File.Exists(tMonPath))
            {
                string currentMonId = null;
                var monNameBuilder = new StringBuilder();

                void FlushMon()
                {
                    if (currentMonId != null)
                        _monsterNames[currentMonId] = monNameBuilder.ToString().Trim();
                }

                foreach (var line in File.ReadLines(tMonPath, chi).Skip(1))
                {
                    if (string.IsNullOrEmpty(line)) continue;

                    int pipe = line.IndexOf('|');
                    bool startsWithId = pipe > 0 && int.TryParse(line.Substring(0, pipe), out _);

                    if (startsWithId)
                    {
                        FlushMon();
                        var parts = line.Split('|');
                        currentMonId = parts[0];
                        monNameBuilder.Clear();
                        if (parts.Length > 1) monNameBuilder.Append(parts[1]);
                    }
                    else if (currentMonId != null)
                    {
                        string extra = line.Trim().Trim('|').Trim();
                        if (!string.IsNullOrEmpty(extra))
                        {
                            if (monNameBuilder.Length > 0) monNameBuilder.Append(" ");
                            monNameBuilder.Append(extra);
                        }
                    }
                }
                FlushMon();
            }

            // 4. Monster Codigos (S_Monster.ini)
            Dictionary<string, string> monsterCodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string sMonPath = Path.Combine(clientPath, "data", "db", "S_Monster.ini");
            if (File.Exists(sMonPath))
            {
                foreach (var line in File.ReadLines(sMonPath, chi).Skip(1))
                {
                    var parts = line.Split('|');
                    if (parts.Length > 1) monsterCodes[parts[0]] = parts[1];
                }
            }

            // 5. Monster Icons (MonsterList.ini)
            string monListPath = Path.Combine(clientPath, "data", "db", "MonsterList.ini");
            if (File.Exists(monListPath))
            {
                foreach (var line in File.ReadLines(monListPath, chi))
                {
                    if (line.StartsWith("DB"))
                    {
                        int eqIdx = line.IndexOf('=');
                        if (eqIdx > 0)
                        {
                            string data = line.Substring(eqIdx + 1);
                            var parts = data.Split(',');
                            if (parts.Length > 1)
                            {
                                string code = parts[0].Trim();
                                string iconRaw = parts[1].Trim(); // The .kfm file name
                                if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(iconRaw))
                                {
                                    string iconName = Path.GetFileNameWithoutExtension(iconRaw);
                                    
                                    // Map ID to icon via code
                                    foreach (var kvp in monsterCodes.Where(x => x.Value.Equals(code, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        _monsterIcons[kvp.Key] = iconName;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // 6. Item Icons (S_Item.ini)
            string dataDb = Path.Combine(clientPath, "data", "db");
            if (!Directory.Exists(dataDb)) dataDb = Path.Combine(clientPath, "Data", "db");
            
            void LoadIcons(string fileName) 
            {
                string path = Path.Combine(dataDb, fileName);
                if (File.Exists(path))
                {
                    foreach (var line in File.ReadLines(path, chi).Skip(1))
                    {
                        var parts = line.Split('|');
                        if (parts.Length > 1) _itemIconsLookup[parts[0].Trim()] = parts[1].Trim();
                    }
                }
            }
            LoadIcons("S_Item.ini");
            LoadIcons("S_ItemMall.ini");
        }

        private async Task LoadMainDatabase()
        {
            try
            {
                db = GenericIniLoader.Load(clientPath, schemasPath, "S_DropItem.ini");
            }
            catch { return; }

            await Task.Run(() =>
            {
                var entryList = db.Rows.Select(kv => new DropEntry
                {
                    Id = kv.Key,
                    Name = ResolveName(kv.Key),
                    Icon = GetEntryIcon(kv.Key)
                }).OrderBy(e => int.TryParse(e.Id, out var id) ? id : 999999).ToList();

                Dispatcher.Invoke(() =>
                {
                    Entries.Clear();
                    foreach (var e in entryList) Entries.Add(e);
                });
            });
        }

        private string ResolveName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            if (_itemNames.TryGetValue(id, out var itemName)) return itemName;
            if (_monsterNames.TryGetValue(id, out var monName)) return monName;
            return "Desconhecido";
        }

        private BitmapSource GetEntryIcon(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            
            // If it's an item, load directly
            if (_itemNames.ContainsKey(id))
            {
                if (!_itemIconsLookup.TryGetValue(id, out var itemIconName) || string.IsNullOrEmpty(itemIconName)) return null;

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
                        string path = Path.Combine(folder, itemIconName + ".dds");
                        if (File.Exists(path)) return DdsLoader.Load(path);
                    }
                }
                return null;
            }

            // Otherwise check monster icons
            if (!_monsterIcons.TryGetValue(id, out string monIconName) || string.IsNullOrEmpty(monIconName)) 
                return null;
            
            string cacheKey = "uiicon_" + monIconName;
            if (_iconCache.TryGetValue(cacheKey, out var cached)) return cached;

            string iconPath = Path.Combine(clientPath, "UI", "uiicon", monIconName + ".dds");
            
            try
            {
                var bitmap = DdsLoader.Load(iconPath);
                if (bitmap != null)
                {
                    _iconCache[cacheKey] = bitmap;
                }
                return bitmap;
            }
            catch { return null; }
        }

        private void DropList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || _suppressSelectionChanged) return;
            if (DropList.SelectedItem is not DropEntry selected) return;

            if (_lastSelectedId != null && _lastSelectedId != selected.Id)
            {
                if (!ConfirmSaveIfNeeded())
                {
                    _suppressSelectionChanged = true;
                    DropList.SelectedItem = Entries.FirstOrDefault(x => x.Id == _lastSelectedId);
                    _suppressSelectionChanged = false;
                    return;
                }
            }

            if (!db.Rows.TryGetValue(selected.Id, out var row)) return;

            var normalizedRow = new List<string>(row);
            while (normalizedRow.Count <= IDX_MARQUEE) normalizedRow.Add("");

            _currentRow = row; 
            _loading = true;

            IdBox.Text = normalizedRow[IDX_ID];
            NameBox.Text = ResolveName(normalizedRow[IDX_ID]);
            LevelBox.Text = normalizedRow[IDX_LEVEL];
            DropGoldRateBox.Text = normalizedRow[IDX_GOLD_RATE];
            AvgGoldBox.Text = normalizedRow[IDX_AVG_GOLD];
            RandGoldBox.Text = normalizedRow[IDX_RAND_GOLD];
            RandTimesBox.Text = normalizedRow[IDX_RAND_TIMES];
            NotDropRateBox.Text = normalizedRow[IDX_NOT_DROP];
            GreenRateBox.Text = normalizedRow[IDX_GREEN];
            BlueRateBox.Text = normalizedRow[IDX_BLUE];
            OrangeRateBox.Text = normalizedRow[IDX_ORANGE];
            YellowRateBox.Text = normalizedRow[IDX_YELLOW];
            PurpleRateBox.Text = normalizedRow[IDX_PURPLE];
            MarqueeBox.Text = normalizedRow[IDX_MARQUEE];

            AllSlots.Clear();
            for (int i = 0; i < 40; i++)
            {
                int start = START_DROPS + (i * 4);
                var slot = new DropItemSlot { Index = i + 1, OnIdChanged = HandleSlotIdChanged };
                if (row.Count > start + 3)
                {
                    slot.ItemId = row[start];
                    slot.ItemName = ResolveName(slot.ItemId);
                    slot.Stack = row[start + 2];
                    slot.Rate = row[start + 3];
                    slot.Icon = GetEntryIcon(slot.ItemId);
                }
                AllSlots.Add(slot);
            }

            PbSlots.Clear();
            for (int i = 0; i < 5; i++)
            {
                int start = START_PB + (i * 4);
                var slot = new DropItemSlot { Index = i + 1, OnIdChanged = HandleSlotIdChanged };
                if (row.Count > start + 3)
                {
                    slot.ItemId = row[start];
                    slot.ItemName = ResolveName(slot.ItemId);
                    slot.Stack = row[start + 2];
                    slot.Rate = row[start + 3];
                    slot.Icon = GetEntryIcon(slot.ItemId);
                }
                PbSlots.Add(slot);
            }

            _currentPage = 1;
            UpdatePagination();

            _originalRowSnapshot = BuildCurrentRow();
            _lastSelectedId = selected.Id;
            _loading = false;
        }

        private void HandleSlotIdChanged(DropItemSlot slot, string newId)
        {
            if (_loading) return;
            if (string.IsNullOrWhiteSpace(newId))
            {
                slot.ItemName = "";
                slot.Icon = null;
                return;
            }

            slot.ItemName = ResolveName(newId);
            slot.Icon = GetEntryIcon(newId);
        }

        private void UpdatePagination()
        {
            CurrentPageSlots.Clear();
            int startIdx = (_currentPage - 1) * ITEMS_PER_PAGE;
            for (int i = startIdx; i < startIdx + ITEMS_PER_PAGE && i < AllSlots.Count; i++)
            {
                CurrentPageSlots.Add(AllSlots[i]);
            }
            PageText.Text = $" (Página {_currentPage}/{TOTAL_PAGES})";
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                UpdatePagination();
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < TOTAL_PAGES)
            {
                _currentPage++;
                UpdatePagination();
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;
            var text = SearchBox.Text;

            Task.Delay(300, token).ContinueWith(t =>
            {
                if (t.IsCanceled) return;
                Dispatcher.Invoke(() =>
                {
                    if (string.IsNullOrWhiteSpace(text)) DropList.ItemsSource = Entries;
                    else
                    {
                        var filtered = Entries.Where(en =>
                            en.Id.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                            en.Name.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
                        DropList.ItemsSource = filtered;
                    }
                });
            }, token);
        }

        private void Reload_Click(object sender, RoutedEventArgs e) => _ = LoadMainDatabase();

        private List<string> BuildCurrentRow()
        {
            if (_currentRow == null) return null;
            var row = new List<string>(_currentRow);
            while (row.Count <= IDX_MARQUEE) row.Add("");

            row[IDX_ID] = IdBox.Text;
            row[1] = NameBox.Text; 
            row[2] = LevelBox.Text;
            row[3] = DropGoldRateBox.Text;
            row[4] = AvgGoldBox.Text;
            row[5] = RandGoldBox.Text;
            row[6] = RandTimesBox.Text;
            row[7] = NotDropRateBox.Text;
            row[8] = GreenRateBox.Text;
            row[9] = BlueRateBox.Text;
            row[10] = OrangeRateBox.Text;
            row[11] = YellowRateBox.Text;
            row[12] = PurpleRateBox.Text;

            for (int i = 0; i < 40; i++)
            {
                int start = START_DROPS + (i * 4);
                var slot = AllSlots[i];
                while (row.Count <= start + 3) row.Add("");
                row[start] = slot.ItemId ?? "";
                row[start + 1] = slot.ItemName ?? "";
                row[start + 2] = slot.Stack ?? "";
                row[start + 3] = slot.Rate ?? "";
            }

            for (int i = 0; i < 5; i++)
            {
                int start = START_PB + (i * 4);
                var slot = PbSlots[i];
                while (row.Count <= start + 3) row.Add("");
                row[start] = slot.ItemId ?? "";
                row[start + 1] = slot.ItemName ?? "";
                row[start + 2] = slot.Stack ?? "";
                row[start + 3] = slot.Rate ?? "";
            }

            row[IDX_MARQUEE] = MarqueeBox.Text;
            return row;
        }

        private bool HasChanges()
        {
            if (_currentRow == null || _originalRowSnapshot == null) return false;
            var current = BuildCurrentRow();
            if (current == null) return false;
            if (current.Count != _originalRowSnapshot.Count) return true;
            for (int i = 0; i < current.Count; i++)
            {
                if (!string.Equals(current[i], _originalRowSnapshot[i], StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private bool ConfirmSaveIfNeeded()
        {
            if (!HasChanges()) return true;

            var result = MessageBox.Show("Deseja salvar as alterações antes de mudar de ID?", "Salvar",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel) return false;
            if (result == MessageBoxResult.Yes)
            {
                Save_Click(null, null);
            }
            return true;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_currentRow == null || db == null) return;

            var row = BuildCurrentRow();
            _currentRow = row;
            _originalRowSnapshot = new List<string>(row);
            db.Rows[row[0]] = row;

            try
            {
                SaveDataIni(db.FilePath, Encoding.GetEncoding(950));
                MessageBox.Show("Salvo com sucesso!");
            }
            catch (Exception ex) { MessageBox.Show("Erro ao salvar: " + ex.Message); }
        }

        private void SaveDataIni(string path, Encoding encoding)
        {
            string header = string.Empty;
            if (File.Exists(path))
            {
                using var sr = new StreamReader(path, encoding);
                header = sr.ReadLine() ?? string.Empty;
            }

            var lines = new List<string>();
            if (!string.IsNullOrEmpty(header)) lines.Add(header);

            foreach (var kv in db.Rows.OrderBy(x => int.TryParse(x.Key, out var id) ? id : 999999))
            {
                var row = kv.Value;
                while (row.Count < (db.Schema?.Columns ?? 194)) row.Add("");
                lines.Add(string.Join("|", row));
            }

            using var sw = new StreamWriter(path, false, encoding);
            foreach (var line in lines) sw.WriteLine(line);
        }

        private void Clone_Click(object sender, RoutedEventArgs e)
        {
            if (_currentRow == null || db == null) return;

            var dlg = new InputDialog("Novo ID para o clone:") { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.InputText)) return;

            string newId = dlg.InputText.Trim();
            if (db.Rows.ContainsKey(newId))
            {
                MessageBox.Show("ID já existe.", "Erro", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newRow = new List<string>(_currentRow);
            newRow[0] = newId;
            db.Rows[newId] = newRow;

            var entry = new DropEntry
            {
                Id = newId,
                Name = ResolveName(newId),
                Icon = GetEntryIcon(newId)
            };
            Entries.Add(entry);
            DropList.SelectedItem = entry;
            DropList.ScrollIntoView(entry);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
