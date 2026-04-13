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

        private string CaptureOriginalBlock(string path, Encoding encoding, string id)
        {
            if (!File.Exists(path)) return null;

            var lines = File.ReadAllLines(path, encoding);
            var block = new StringBuilder();
            bool found = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                var parts = line.Split('|');
                if (parts.Length == 0) continue;

                string currentId = parts[0].Trim();
                bool startsWithId = !string.IsNullOrWhiteSpace(currentId) && int.TryParse(currentId, out _);

                if (startsWithId)
                {
                    if (currentId == id)
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

            string firstLine = block.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)[0];
            var firstLineParts = firstLine.Split('|').ToList();
            if (firstLineParts.Count > 0)
            {
                firstLineParts[0] = newId;
                string newFirstLine = string.Join("|", firstLineParts);
                string newBlock = newFirstLine + block.Substring(firstLine.Length);

                string content = File.ReadAllText(path, encoding);
                using var sw = new StreamWriter(path, true, encoding);
                if (!string.IsNullOrEmpty(content) && !content.EndsWith("\n") && !content.EndsWith("\r"))
                {
                    sw.WriteLine();
                }
                sw.WriteLine(newBlock);
            }
        }

        private void PatchIniRowInPlace(string path, Encoding encoding, string oldId, string newId, string[] editedRow)
        {
            if (!File.Exists(path)) return;

            var lines = File.ReadAllLines(path, encoding);
            bool found = false;

            for (int i = 1; i < lines.Length; i++)
            {
                string rawLine = lines[i];
                var parts = rawLine.Split(new[] { '|' }, StringSplitOptions.None).ToList();

                if (parts.Count == 0) continue;

                if (parts[0].Trim() != oldId) continue;

                // Atualizar campos presentes no editedRow
                int needed = Math.Max(parts.Count, editedRow.Length);
                while (parts.Count < needed) parts.Add(string.Empty);

                for (int j = 0; j < editedRow.Length; j++)
                {
                    parts[j] = editedRow[j] ?? string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(newId))
                    parts[0] = newId;

                string newLine = string.Join("|", parts);

                // Preservar pipe final
                if (rawLine.TrimEnd().EndsWith("|") && !newLine.EndsWith("|"))
                    newLine += "|";

                lines[i] = newLine;
                found = true;
                break;
            }

            if (found)
                File.WriteAllLines(path, lines, encoding);
        }

        private void PatchTranslateRowInPlace(string path, Encoding encoding, string id, string name, string desc)
        {
            if (!File.Exists(path)) return;
            var lines = File.ReadAllLines(path, encoding);
            bool found = false;

            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split('|').ToList();
                if (parts.Count > 0 && parts[0].Trim() == id)
                {
                    while (parts.Count < 3) parts.Add(string.Empty);
                    parts[1] = name ?? "";
                    parts[2] = desc ?? "";
                    
                    string newLine = string.Join("|", parts);
                    if (lines[i].TrimEnd().EndsWith("|") && !newLine.EndsWith("|"))
                        newLine += "|";
                    
                    lines[i] = newLine;
                    found = true;
                    break;
                }
            }

            if (found)
                File.WriteAllLines(path, lines, encoding);
        }

        private void CloneButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNpc == null)
            {
                MessageBox.Show("Selecione um NPC para clonar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string oldId = SelectedNpc.Id;
            string newId = PromptNewId("Clonar NPC", "Informe o novo ID:");
            if (string.IsNullOrWhiteSpace(newId)) return;

            if (db.Rows.ContainsKey(newId))
            {
                MessageBox.Show("Este ID já existe.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var sPath = db.FilePath;
                var dir = Path.GetDirectoryName(sPath);
                var cPath = Path.Combine(dir, "C_Npc.ini");
                var tPath = db.TranslationFilePath;

                var big5 = Encoding.GetEncoding(950);
                var w1252 = Encoding.GetEncoding(1252);

                AppendClonedBlock(sPath, big5, oldId, newId);
                if (File.Exists(cPath))
                    AppendClonedBlock(cPath, big5, oldId, newId);
                
                if (!string.IsNullOrEmpty(tPath))
                    AppendClonedBlock(tPath, w1252, oldId, newId);

                MessageBox.Show("NPC clonado com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                
                LoadDataAsync();
                
                // Tenta selecionar o novo
                var newItem = AllEntries.FirstOrDefault(x => x.Id == newId);
                if (newItem != null) SelectedNpc = newItem;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao clonar: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string PromptNewId(string title, string message)
        {
            var dialog = new InputDialog(title, message);
            if (dialog.ShowDialog() == true)
            {
                return dialog.InputText;
            }
            return null;
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
            if (SelectedNpc == null) return;

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
                    var big5 = Encoding.GetEncoding(950);
                    var w1252 = Encoding.GetEncoding(1252);

                    var rowData = new string[20];
                    rowData[0] = SelectedNpc.Id ?? "";
                    rowData[1] = SelectedNpc.ModelString ?? "";
                    rowData[2] = SelectedNpc.NpcName ?? "";
                    rowData[3] = SelectedNpc.NpcControl ?? "";
                    rowData[4] = ((int)SelectedNpc.CursorType).ToString();
                    rowData[5] = SelectedNpc.DialogId1 ?? "";
                    rowData[6] = SelectedNpc.DialogId2 ?? "";
                    rowData[7] = SelectedNpc.DialogRate ?? "";
                    rowData[8] = SelectedNpc.NpcCmd ?? "";
                    rowData[9] = SelectedNpc.OptionCmd1 ?? "";
                    rowData[10] = SelectedNpc.OptionCmd2 ?? "";
                    rowData[11] = SelectedNpc.OptionCmd3 ?? "";
                    rowData[12] = SelectedNpc.OptionCmd4 ?? "";
                    rowData[13] = SelectedNpc.OptionCmd5 ?? "";
                    rowData[14] = SelectedNpc.OptionCmd6 ?? "";
                    rowData[15] = SelectedNpc.OptionCmd7 ?? "";
                    rowData[16] = SelectedNpc.OptionCmd8 ?? "";
                    rowData[17] = SelectedNpc.LocateLimit ?? "";
                    rowData[18] = SelectedNpc.ControlFlag ?? "";
                    rowData[19] = SelectedNpc.Note ?? "";

                    // Patch S and C
                    PatchIniRowInPlace(sPath, big5, SelectedNpc.Id, SelectedNpc.Id, rowData);
                    if (File.Exists(cPath))
                        PatchIniRowInPlace(cPath, big5, SelectedNpc.Id, SelectedNpc.Id, rowData);

                    // Patch T
                    if (!string.IsNullOrEmpty(tPath))
                    {
                        PatchTranslateRowInPlace(tPath, w1252, SelectedNpc.Id, 
                            SelectedNpc.TranslatedName, SelectedNpc.TranslatedDesc);
                    }
                });

                StatusText.Text = LocalizationManager.Instance.GetLocalizedString("Npc.Dialogs.SuccessSaving");
                MessageBox.Show(LocalizationManager.Instance.GetLocalizedString("Npc.Dialogs.SuccessSaving"), 
                                LocalizationManager.Instance.GetLocalizedString("Common.Success") ?? "Success",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                
                LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao salvar: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
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
