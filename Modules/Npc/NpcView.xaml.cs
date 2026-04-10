using GrandFantasiaINIEditor.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace GrandFantasiaINIEditor.Modules.Npc
{
    public partial class NpcView : UserControl, INotifyPropertyChanged
    {
        private const string DefaultNpcCmdTemplate = "2001##";
        private const string DefaultOptionCmdTemplate = "#:Talk 0 0:#:::::::#:::::::#:0:#";

        private readonly string clientPath;
        private readonly string schemasPath;
        private GenericIniDb db;
        private readonly Dictionary<string, string> _npcNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _itemNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _monsterNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _nodeNames = new(StringComparer.OrdinalIgnoreCase);

        public ObservableCollection<NpcEntry> AllEntries { get; set; } = new();
        private ICollectionView entriesView;

        private NpcEntry _selectedNpc;
        public NpcEntry SelectedNpc
        {
            get => _selectedNpc;
            set
            {
                _selectedNpc = value;
                OnPropertyChanged(nameof(SelectedNpc));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public NpcView(string clientPath, string schemasPath)
        {
            InitializeComponent();
            DataContext = this;
            this.clientPath = clientPath;
            this.schemasPath = schemasPath;

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            LoadTranslationLookups();
            ConfigureLegendResolvers();

            var cursorTypes = LocalizationManager.Instance.GetDictionary("Npc.CursorTypes");
            CursorTypeCombo.ItemsSource = cursorTypes
                .Select(kv => new ComboOption { Value = int.Parse(kv.Key), Label = kv.Value })
                .OrderBy(x => x.Value)
                .ToList();
            CursorTypeCombo.DisplayMemberPath = "Label";
            CursorTypeCombo.SelectedValuePath = "Value";
            
            entriesView = CollectionViewSource.GetDefaultView(AllEntries);
            entriesView.Filter = SearchFilter;

            LoadDataAsync();
        }

        private void ConfigureLegendResolvers()
        {
            NpcEntry.ItemNameResolver = GetItemName;
            NpcEntry.NpcNameResolver = GetNpcName;
            NpcEntry.MonsterNameResolver = GetMonsterName;
            NpcEntry.NodeNameResolver = GetNodeName;
        }

        private void LoadTranslationLookups()
        {
            _npcNames.Clear();
            _itemNames.Clear();
            _monsterNames.Clear();
            _nodeNames.Clear();

            LoadMultilineNameLookup(Path.Combine(clientPath, "data", "translate", "T_Npc.ini"), _npcNames, Encoding.GetEncoding(1252));
            LoadSimpleNameLookup(Path.Combine(clientPath, "data", "translate", "T_Item.ini"), _itemNames, 0, 1, Encoding.GetEncoding(1252));
            LoadSimpleNameLookup(Path.Combine(clientPath, "data", "translate", "T_Monster.ini"), _monsterNames, 0, 1, Encoding.GetEncoding(1252));
            LoadSimpleNameLookup(Path.Combine(clientPath, "data", "translate", "T_Node.ini"), _nodeNames, 0, 1, Encoding.GetEncoding(1252));
        }

        private void LoadSimpleNameLookup(string path, Dictionary<string, string> target, int idColumn, int nameColumn, Encoding encoding)
        {
            if (!File.Exists(path))
                return;

            var lines = File.ReadAllLines(path, encoding);

            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split('|');

                if (parts.Length <= Math.Max(idColumn, nameColumn))
                    continue;

                string id = (parts[idColumn] ?? string.Empty).Trim();
                string name = (parts[nameColumn] ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (!target.ContainsKey(id))
                    target[id] = name;
            }
        }

        private void LoadMultilineNameLookup(string path, Dictionary<string, string> target, Encoding encoding)
        {
            if (!File.Exists(path))
                return;

            var rawLines = File.ReadAllLines(path, encoding);

            string currentId = null;
            var currentName = new StringBuilder();

            void FlushCurrent()
            {
                if (string.IsNullOrWhiteSpace(currentId))
                    return;

                string finalName = currentName.ToString().Trim();

                if (!target.ContainsKey(currentId))
                    target[currentId] = finalName;
            }

            for (int i = 1; i < rawLines.Length; i++)
            {
                string line = rawLines[i] ?? string.Empty;

                int firstPipe = line.IndexOf('|');
                bool startsWithId = firstPipe > 0 && int.TryParse(line.Substring(0, firstPipe).Trim(), out _);

                if (startsWithId)
                {
                    FlushCurrent();

                    var parts = line.Split(new[] { '|' }, 3);
                    currentId = parts.Length > 0 ? parts[0].Trim() : string.Empty;

                    currentName.Clear();
                    if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
                        currentName.Append(parts[1].Trim());
                }
                else
                {
                    if (currentId == null)
                        continue;

                    string extra = line.Trim().Trim('|').Trim();
                    if (string.IsNullOrWhiteSpace(extra))
                        continue;

                    if (currentName.Length > 0)
                        currentName.Append(' ');

                    currentName.Append(extra);
                }
            }

            FlushCurrent();
        }

        private string GetNpcName(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return $"NPC {id}";

            return _npcNames.TryGetValue(id.Trim(), out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : $"NPC {id}";
        }

        private string GetItemName(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return $"Item {id}";

            return _itemNames.TryGetValue(id.Trim(), out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : $"Item {id}";
        }

        private string GetMonsterName(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return $"Monstro {id}";

            return _monsterNames.TryGetValue(id.Trim(), out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : $"Monstro {id}";
        }

        private string GetNodeName(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return $"No {id}";

            return _nodeNames.TryGetValue(id.Trim(), out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : $"No {id}";
        }

        private static string SanitizeTranslationField(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            string cleaned = value.Trim();

            while (cleaned.EndsWith("|", StringComparison.Ordinal))
                cleaned = cleaned.Substring(0, cleaned.Length - 1).TrimEnd();

            return cleaned;
        }

        private async void LoadDataAsync()
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            try
            {
                await Task.Run(() =>
                {
                    db = GenericIniLoader.Load(clientPath, schemasPath, "S_Npc.ini", "T_Npc.ini");
                });

                AllEntries.Clear();

                foreach (var kvp in db.Rows)
                {
                    var r = kvp.Value;
                    string id = kvp.Key;
                    
                    var trans = db.Translations.TryGetValue(id, out var t) ? t : null;

                    var entry = new NpcEntry
                    {
                        Id = id,
                        ModelString = r.Count > 1 ? r[1] : "",
                        NpcName = r.Count > 2 ? r[2] : "",
                        NpcControl = r.Count > 3 ? r[3] : "",
                        CursorType = Enum.TryParse<NpcCursorType>(r.Count > 4 ? r[4] : "1", out var ct) ? ct : NpcCursorType.None,
                        DialogId1 = r.Count > 5 ? r[5] : "",
                        DialogId2 = r.Count > 6 ? r[6] : "",
                        DialogRate = r.Count > 7 ? r[7] : "",
                        NpcCmd = r.Count > 8 ? r[8] : "",
                        OptionCmd1 = r.Count > 9 ? r[9] : "",
                        OptionCmd2 = r.Count > 10 ? r[10] : "",
                        OptionCmd3 = r.Count > 11 ? r[11] : "",
                        OptionCmd4 = r.Count > 12 ? r[12] : "",
                        OptionCmd5 = r.Count > 13 ? r[13] : "",
                        OptionCmd6 = r.Count > 14 ? r[14] : "",
                        OptionCmd7 = r.Count > 15 ? r[15] : "",
                        OptionCmd8 = r.Count > 16 ? r[16] : "",
                        LocateLimit = r.Count > 17 ? r[17] : "",
                        ControlFlag = r.Count > 18 ? r[18] : "",
                        Note = r.Count > 19 ? r[19] : "",
                        TranslatedName = trans != null ? SanitizeTranslationField(trans.Name) : "",
                        TranslatedDesc = trans != null ? SanitizeTranslationField(trans.Description) : ""
                    };

                    AllEntries.Add(entry);
                }

                NpcGrid.ItemsSource = entriesView;
                StatusText.Text = string.Format(LocalizationManager.Instance.GetLocalizedString("Npc.Status.Loaded"), AllEntries.Count);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LocalizationManager.Instance.GetLocalizedString("Npc.Dialogs.ErrorLoading"), ex.Message), 
                                LocalizationManager.Instance.GetLocalizedString("Common.Error") ?? "Erro");
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private bool SearchFilter(object item)
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
                return true;

            var entry = item as NpcEntry;
            if (entry == null) return false;

            string query = SearchBox.Text.ToLowerIgnoreCase();
            return (entry.Id?.ToLowerIgnoreCase().Contains(query) == true) ||
                   (entry.DisplayName?.ToLowerIgnoreCase().Contains(query) == true) ||
                   (entry.NpcName?.ToLowerIgnoreCase().Contains(query) == true);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            entriesView.Refresh();
        }

        private void NpcGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedNpc = NpcGrid.SelectedItem as NpcEntry;
        }

        private void CommandTokenLine_LostFocus(object sender, RoutedEventArgs e)
        {
            if (SelectedNpc == null)
                return;

            if (sender is not TextBox tb)
                return;

            if (tb.DataContext is not NpcEntry.CommandTokenLine line)
                return;

            string commandField = tb.Tag as string;
            if (string.IsNullOrWhiteSpace(commandField))
                return;

            string updated = ApplyEditedToken(commandField, line.SectionIndex, line.TokenIndex, tb.Text ?? string.Empty);

            switch (commandField)
            {
                case "NpcCmd":
                    SelectedNpc.NpcCmd = updated;
                    break;
                case "OptionCmd1":
                    SelectedNpc.OptionCmd1 = updated;
                    break;
                case "OptionCmd2":
                    SelectedNpc.OptionCmd2 = updated;
                    break;
                case "OptionCmd3":
                    SelectedNpc.OptionCmd3 = updated;
                    break;
                case "OptionCmd4":
                    SelectedNpc.OptionCmd4 = updated;
                    break;
                case "OptionCmd5":
                    SelectedNpc.OptionCmd5 = updated;
                    break;
                case "OptionCmd6":
                    SelectedNpc.OptionCmd6 = updated;
                    break;
                case "OptionCmd7":
                    SelectedNpc.OptionCmd7 = updated;
                    break;
                case "OptionCmd8":
                    SelectedNpc.OptionCmd8 = updated;
                    break;
            }
        }

        private string ApplyEditedToken(string commandField, int sectionIndex, int tokenIndex, string newToken)
        {
            string source = commandField switch
            {
                "NpcCmd" => SelectedNpc.NpcCmd,
                "OptionCmd1" => SelectedNpc.OptionCmd1,
                "OptionCmd2" => SelectedNpc.OptionCmd2,
                "OptionCmd3" => SelectedNpc.OptionCmd3,
                "OptionCmd4" => SelectedNpc.OptionCmd4,
                "OptionCmd5" => SelectedNpc.OptionCmd5,
                "OptionCmd6" => SelectedNpc.OptionCmd6,
                "OptionCmd7" => SelectedNpc.OptionCmd7,
                "OptionCmd8" => SelectedNpc.OptionCmd8,
                _ => string.Empty
            };

            return NpcEntry.ReplaceTokenInSection(source, sectionIndex, tokenIndex, newToken);
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var entry = new NpcEntry
            {
                Id = "9999",
                NpcName = "New NPC",
                CursorType = NpcCursorType.None,
                NpcCmd = DefaultNpcCmdTemplate,
                OptionCmd1 = DefaultOptionCmdTemplate,
                OptionCmd2 = DefaultOptionCmdTemplate,
                OptionCmd3 = DefaultOptionCmdTemplate,
                OptionCmd4 = DefaultOptionCmdTemplate,
                OptionCmd5 = DefaultOptionCmdTemplate,
                OptionCmd6 = DefaultOptionCmdTemplate,
                OptionCmd7 = DefaultOptionCmdTemplate,
                OptionCmd8 = DefaultOptionCmdTemplate
            };

            AllEntries.Add(entry);
            SelectedNpc = entry;
            NpcGrid.ScrollIntoView(entry);
            SearchBox.Text = "";
            StatusText.Text = LocalizationManager.Instance.GetLocalizedString("Npc.Status.Added");
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNpc == null) return;

            var confirmMsg = string.Format(LocalizationManager.Instance.GetLocalizedString("Npc.Dialogs.ConfirmRemoval"), SelectedNpc.Id);
            var confirmTitle = LocalizationManager.Instance.GetLocalizedString("Npc.Dialogs.ConfirmRemovalTitle");

            if (MessageBox.Show(confirmMsg, confirmTitle, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                AllEntries.Remove(SelectedNpc);
                SelectedNpc = null;
                StatusText.Text = LocalizationManager.Instance.GetLocalizedString("Npc.Status.Removed");
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var sPath = db.FilePath;
            if (string.IsNullOrEmpty(sPath)) return;
            var dir = Path.GetDirectoryName(sPath);

            var cPath = Path.Combine(dir, "C_Npc.ini");
            var tPath = db.TranslationFilePath;

            LoadingOverlay.Visibility = Visibility.Visible;
            StatusText.Text = LocalizationManager.Instance.GetLocalizedString("Common.Saving") ?? "Saving...";
            
            try
            {
                await Task.Run(() =>
                {
                    // Save translation (T_Npc.ini)
                    if (!string.IsNullOrEmpty(tPath))
                    {
                        using var tWriter = new StreamWriter(tPath, false, System.Text.Encoding.GetEncoding("Big5"));
                        if (!string.IsNullOrEmpty(db.VersionLine)) tWriter.WriteLine(db.VersionLine);
                        tWriter.WriteLine("ID|Name|Description|");
                        foreach (var entry in AllEntries.OrderBy(x => int.TryParse(x.Id, out int i) ? i : 999999))
                        {
                            string safeName = SanitizeTranslationField(entry.TranslatedName);
                            string safeDesc = SanitizeTranslationField(entry.TranslatedDesc);
                            tWriter.WriteLine($"{entry.Id}|{safeName}|{safeDesc}|");
                        }
                    }

                    var lines = new System.Collections.Generic.List<string>();
                    
                    if (!string.IsNullOrEmpty(db.VersionLine)) lines.Add(db.VersionLine);
                    
                    string header = "NpcID|ModelString|NpcName|NpcControl|CursorType|DialogId1|DialogId2|DialogRate|NpcCmd|OptionCmd1|OptionCmd2|OptionCmd3|OptionCmd4|OptionCmd5|OptionCmd6|OptionCmd7|OptionCmd8|LocateLimit|ControlFlag|Note|";
                    if (!string.IsNullOrEmpty(db.ColumnHeader)) header = db.ColumnHeader;
                    lines.Add(header);

                    foreach (var entry in AllEntries.OrderBy(x => int.TryParse(x.Id, out int i) ? i : 999999))
                    {
                        var row = new string[20];
                        row[0] = entry.Id ?? "";
                        row[1] = entry.ModelString ?? "";
                        row[2] = entry.NpcName ?? "";
                        row[3] = entry.NpcControl ?? "";
                        row[4] = ((int)entry.CursorType).ToString();
                        row[5] = entry.DialogId1 ?? "";
                        row[6] = entry.DialogId2 ?? "";
                        row[7] = entry.DialogRate ?? "";
                        row[8] = entry.NpcCmd ?? "";
                        row[9] = entry.OptionCmd1 ?? "";
                        row[10] = entry.OptionCmd2 ?? "";
                        row[11] = entry.OptionCmd3 ?? "";
                        row[12] = entry.OptionCmd4 ?? "";
                        row[13] = entry.OptionCmd5 ?? "";
                        row[14] = entry.OptionCmd6 ?? "";
                        row[15] = entry.OptionCmd7 ?? "";
                        row[16] = entry.OptionCmd8 ?? "";
                        row[17] = entry.LocateLimit ?? "";
                        row[18] = entry.ControlFlag ?? "";
                        row[19] = entry.Note ?? "";
                        
                        lines.Add(string.Join("|", row) + "|");
                    }

                    // Write to S_Npc.ini
                    File.WriteAllLines(sPath, lines, System.Text.Encoding.GetEncoding("Big5"));
                    // Write strictly mirroring to C_Npc.ini
                    File.WriteAllLines(cPath, lines, System.Text.Encoding.GetEncoding("Big5"));
                });

                StatusText.Text = LocalizationManager.Instance.GetLocalizedString("Npc.Dialogs.SuccessSaving");
                MessageBox.Show(LocalizationManager.Instance.GetLocalizedString("Npc.Dialogs.SuccessSaving"), 
                                LocalizationManager.Instance.GetLocalizedString("Common.Success") ?? "Success");
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LocalizationManager.Instance.GetLocalizedString("Npc.Dialogs.ErrorSaving"), ex.Message));
                StatusText.Text = LocalizationManager.Instance.GetLocalizedString("Common.Error") ?? "Error";
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }
    }

    public class ComboOption
    {
        public int Value { get; set; }
        public string Label { get; set; }
    }

    public static class StringExtensions
    {
        public static string ToLowerIgnoreCase(this string source)
        {
            return source?.ToLower() ?? string.Empty;
        }
    }
}
