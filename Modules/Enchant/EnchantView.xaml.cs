using GrandFantasiaINIEditor.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.ComponentModel;

namespace GrandFantasiaINIEditor.Modules.Enchant
{
    public partial class EnchantView : UserControl
    {
        private readonly string clientPath;
        private readonly string schemasPath;
        private GenericIniDb db;
        private readonly ObservableCollection<EnchantEntry> EnchantListSource = new();
        private readonly Dictionary<string, EnchantEntry> Enchants = new();
        private Dictionary<string, JsonElement> commandLegends = new();
        private bool _loading;
        private List<string> _currentRow;
        private List<string> _originalRowSnapshot;
        private EnchantEntry _selectedEntry;
        private readonly List<(ulong bit, CheckBox cb)> _classChecks = new();

        // Indices
        private const int IDX_ID = 0;
        private const int IDX_ICON = 1;
        private const int IDX_ANIM = 2;
        private const int IDX_EFFECT = 3;
        private const int IDX_NODE = 4;
        private const int IDX_NAME = 5;
        private const int IDX_TYPE = 6;
        private const int IDX_FLAG = 7;
        private const int IDX_CATEGORY = 8;
        private const int IDX_IMMUNE = 9;
        
        private const int IDX_CMD1 = 10;
        private const int IDX_P1_1 = 11;
        
        private const int IDX_CMD2 = 17;
        private const int IDX_P2_1 = 18;
        
        private const int IDX_CMD3 = 24;
        private const int IDX_P3_1 = 25;
        
        private const int IDX_CMD4 = 31;
        private const int IDX_P4_1 = 32;

        private const int IDX_PERIOD = 38;
        private const int IDX_HIWORD = 39;
        private const int IDX_LOWWORD = 40;
        private const int IDX_NEXT_LVL = 41;
        private const int IDX_TRANS_TYPE = 42;
        private const int IDX_TRANS_RATE = 43;
        private const int IDX_TRANS_DUR = 44;
        private const int IDX_TRANS_PERIOD = 45;
        private const int IDX_TRANS_ICON = 46;
        private const int IDX_TRANS_ENC_TYPE = 47;
        private const int IDX_TRANS_FLAG = 48;
        private const int IDX_TRANS_CAT = 49;
        private const int IDX_TRANS_ANIM = 50;
        private const int IDX_TRANS_EFF = 51;
        private const int IDX_TRANS_NODE = 52;
        private const int IDX_TRANS_EFF_DUR = 53;
        private const int IDX_TRANS_DUR_NODE = 54;
        private const int IDX_COOLDOWN = 55;
        private const int IDX_WEAPON_FLAG = 56;
        private const int IDX_TRANS_HIWORD = 57;
        private const int IDX_TRANS_LOWWORD = 58;
        private const int IDX_TOOLTIP = 59;
        private const int IDX_TRANS_TOOLTIP = 60;
        private const int IDX_TRANS_NAME = 61;
        private const int IDX_MAX_STACK = 62;

        public EnchantView(string clientPath, string schemasPath)
        {
            InitializeComponent();
            this.clientPath = clientPath;
            this.schemasPath = schemasPath;
            EnchantList.ItemsSource = EnchantListSource;

            commandLegends = LocalizationManager.Instance.GetCommandLegends();
            PopulateCombos();
            InitClassesUi();
            HookClassEvents();
            LoadDatabase();
        }


        private void PopulateCombos()
        {
            var enchantTypes = LocalizationManager.Instance.GetDictionary("Enchant.Types")
                .Select(kv => new ComboOption { Value = int.Parse(kv.Key), Label = kv.Value })
                .ToList();
            TypeCombo.ItemsSource = enchantTypes;
            TransEncTypeCombo.ItemsSource = enchantTypes;

            var transitions = LocalizationManager.Instance.GetDictionary("Enchant.Transitions")
                .Select(kv => new ComboOption { Value = int.Parse(kv.Key), Label = kv.Value })
                .ToList();
            TransTypeCombo.ItemsSource = transitions;

            var categories = LocalizationManager.Instance.GetDictionary("Enchant.Categories")
                .Select(kv => new ComboOption { Value = int.Parse(kv.Key), Label = kv.Value })
                .ToList();
            CategoryCombo.ItemsSource = categories;
            TransCategoryCombo.ItemsSource = categories;

            // Flags
            var flagsDict = LocalizationManager.Instance.GetDictionary("Enchant.Flags");
            var flagsList = flagsDict
                .Select(kv => new FlagItem(kv.Value, ulong.Parse(kv.Key)))
                .OrderBy(x => x.Value)
                .ToList();
            FlagsItemsControl.ItemsSource = flagsList;
        }

        private void InitClassesUi()
        {
            if (class_grid != null)
            {
                class_grid.Children.Clear();
                _classChecks.Clear();

                foreach (var kv in LocalizationManager.Instance.GetDictionary("Item.Classes"))
                {
                    if (ulong.TryParse(kv.Key, out ulong bit))
                    {
                        var cb = new CheckBox
                        {
                            Content = $"{kv.Value} (0x{bit:X})",
                            Foreground = Brushes.AliceBlue,
                            Margin = new Thickness(0, 0, 14, 10)
                        };
                        _classChecks.Add((bit, cb));
                        class_grid.Children.Add(cb);
                    }
                }
            }
        }

        private void HookClassEvents()
        {
            foreach (var item in _classChecks)
            {
                item.cb.Checked += ClassCheckChanged;
                item.cb.Unchecked += ClassCheckChanged;
            }
        }

        private void ClassCheckChanged(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            RecomputeClasses();
        }

        private void RecomputeClasses()
        {
            ulong total = 0;
            foreach (var item in _classChecks)
            {
                if (item.cb.IsChecked == true) total |= item.bit;
            }

            _loading = true;
            ClassBox.Text = total.ToString();
            restrictclass_value.Text = $"{total}  |  0x{total:X}";
            _loading = false;
        }

        private void ClassBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;
            if (ulong.TryParse(ClassBox.Text, out ulong val))
            {
                UpdateClassChecks(val);
                restrictclass_value.Text = $"{val}  |  0x{val:X}";
            }
        }

        private void UpdateClassChecks(ulong val)
        {
            _loading = true;
            foreach (var item in _classChecks)
            {
                item.cb.IsChecked = (val & item.bit) != 0;
            }
            _loading = false;
        }

        private void LoadDatabase()
        {
            try
            {
                db = GenericIniLoader.Load(clientPath, schemasPath, "S_Enchant.ini", "T_Enchant.ini");
                Enchants.Clear();
                EnchantListSource.Clear();

                foreach (var r in db.Rows.OrderBy(x => int.TryParse(x.Key, out var id) ? id : 0))
                {
                    string name = "Unknown";
                    if (db.Translations.TryGetValue(r.Key, out var trans))
                    {
                        name = trans.Values.Count > 1 ? trans.Values[1] : trans.Name;
                    }
                    var entry = new EnchantEntry { Id = r.Key, Name = name };
                    Enchants.Add(r.Key, entry);
                    EnchantListSource.Add(entry);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao carregar banco de dados: " + ex.Message);
            }
        }

        private void LoadIcon(string iconName, Image imageControl)
        {
            if (imageControl == null) return;
            if (string.IsNullOrWhiteSpace(iconName))
            {
                imageControl.Source = null;
                return;
            }

            string path = Path.Combine(clientPath, "UI", "skillicon", iconName + ".dds");
            if (File.Exists(path))
            {
                imageControl.Source = DdsLoader.Load(path);
            }
            else
            {
                imageControl.Source = null;
            }
        }

        private void PatchIniRowInPlace(string path, Encoding encoding, string oldId, string newId, List<string> editedRow)
        {
            if (!File.Exists(path)) return;

            var lines = File.ReadAllLines(path, encoding);
            bool found = false;

            for (int i = 1; i < lines.Length; i++)
            {
                string rawLine = lines[i];
                var parts = rawLine.Split(new[] { '|' }, StringSplitOptions.None).ToList();

                if (parts.Count == 0) continue;

                string lineId = parts[0].Trim();
                // Alguns arquivos T começam com |ID|... (o primeiro elemento é vazio)
                int idIdx = 0;
                if (string.IsNullOrWhiteSpace(lineId) && parts.Count > 1)
                {
                    idIdx = 1;
                    lineId = parts[1].Trim();
                }

                if (!string.Equals(lineId, oldId, StringComparison.Ordinal)) continue;

                // Atualizar os campos
                int needed = Math.Max(parts.Count, editedRow.Count + idIdx);
                while (parts.Count < needed) parts.Add(string.Empty);

                for (int j = 0; j < editedRow.Count; j++)
                {
                    parts[j + idIdx] = editedRow[j] ?? string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(newId))
                    parts[idIdx + IDX_ID] = newId;

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
                int idIdx = (parts.Length > 1 && string.IsNullOrWhiteSpace(parts[0])) ? 1 : 0;
                
                bool startsWithId = parts.Length > idIdx && int.TryParse(parts[idIdx].Trim(), out _);

                if (startsWithId)
                {
                    string currentId = parts[idIdx].Trim();
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

            var parts = block.Split('|').ToList();
            int idIdx = (parts.Count > 1 && string.IsNullOrWhiteSpace(parts[0])) ? 1 : 0;
            
            if (parts.Count > idIdx)
            {
                // Reconstruir o bloco trocando o ID na primeira linha do bloco
                string firstLine = block.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)[0];
                var firstLineParts = firstLine.Split('|').ToList();
                firstLineParts[idIdx] = newId;
                
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

        private void EnchantList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EnchantList.SelectedItem is EnchantEntry entry)
            {
                _loading = true;
                _selectedEntry = entry;
                _currentRow = db.Rows[entry.Id];
                _originalRowSnapshot = new List<string>(_currentRow);
                
                PopulateFields();
                LoadIcon(IconFileBox.Text, EnchantIcon);
                LoadIcon(TransIconBox.Text, TransEnchantIcon);
                _loading = false;
            }
        }

        private void IconFileBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;
            LoadIcon(IconFileBox.Text, EnchantIcon);
        }

        private void TransIconBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;
            LoadIcon(TransIconBox.Text, TransEnchantIcon);
        }

        private void PopulateFields()
        {
            IdBox.Text = GetField(IDX_ID);
            NameBox.Text = _selectedEntry?.Name ?? "";
            IconFileBox.Text = GetField(IDX_ICON);
            AnimIdBox.Text = GetField(IDX_ANIM);
            EffectIdBox.Text = GetField(IDX_EFFECT);
            EffectNodeBox.Text = GetField(IDX_NODE);
            PeriodBox.Text = GetField(IDX_PERIOD);
            CooldownBox.Text = GetField(IDX_COOLDOWN);
            MaxStackBox.Text = GetField(IDX_MAX_STACK);
            ImmuneMobBox.Text = GetField(IDX_IMMUNE);
            HiWordBox.Text = GetField(IDX_HIWORD);
            LowWordBox.Text = GetField(IDX_LOWWORD);
            NextLevelIdBox.Text = GetField(IDX_NEXT_LVL);
            ClassBox.Text = GetField(IDX_WEAPON_FLAG);
            UpdateClassChecks(GetFieldULong(IDX_WEAPON_FLAG));
            restrictclass_value.Text = $"{GetField(IDX_WEAPON_FLAG)}  |  0x{GetFieldULong(IDX_WEAPON_FLAG):X}";

            // Combos
            SelectComboByValue(TypeCombo, GetFieldInt(IDX_TYPE));
            SelectComboByValue(CategoryCombo, GetFieldInt(IDX_CATEGORY));
            SelectComboByValue(TransTypeCombo, GetFieldInt(IDX_TRANS_TYPE));
            SelectComboByValue(TransEncTypeCombo, GetFieldInt(IDX_TRANS_ENC_TYPE));
            SelectComboByValue(TransCategoryCombo, GetFieldInt(IDX_TRANS_CAT));

            // Trans Info
            TransRateBox.Text = GetField(IDX_TRANS_RATE);
            TransDurationBox.Text = GetField(IDX_TRANS_DUR);
            TransPeriodBox.Text = GetField(IDX_TRANS_PERIOD);
            TransIconBox.Text = GetField(IDX_TRANS_ICON);
            TransAnimBox.Text = GetField(IDX_TRANS_ANIM);
            TransEffectBox.Text = GetField(IDX_TRANS_EFF);
            TransNodeBox.Text = GetField(IDX_TRANS_NODE);
            TransEffDurBox.Text = GetField(IDX_TRANS_EFF_DUR);
            TransDurNodeBox.Text = GetField(IDX_TRANS_DUR_NODE);
            TransHiWordBox.Text = GetField(IDX_TRANS_HIWORD);
            TransLowWordBox.Text = GetField(IDX_TRANS_LOWWORD);
            TransFlagBox.Text = GetField(IDX_TRANS_FLAG);

            // Translations from T_Enchant
            if (db.Translations.TryGetValue(entry_id_global_hack, out var trans))
            {
                NameBox.Text = trans.Values.Count > 1 ? trans.Values[1] : "";
                TooltipBox.Text = trans.Values.Count > 2 ? trans.Values[2] : "";
                TransTooltipBox.Text = trans.Values.Count > 3 ? trans.Values[3] : "";
                TransNameBox.Text = trans.Values.Count > 4 ? trans.Values[4] : "";
            }
            else
            {
                TooltipBox.Text = "";
                TransTooltipBox.Text = "";
                TransNameBox.Text = "";
            }

            // Flags
            ulong flags = GetFieldULong(IDX_FLAG);
            FlagValueText.Text = $"Flag: {flags} (0x{flags:X})";
            if (FlagsItemsControl.ItemsSource is List<FlagItem> flagsList)
            {
                foreach (var item in flagsList)
                {
                    item.IsChecked = (flags & item.Value) != 0;
                }
                FlagsItemsControl.Items.Refresh();
            }

            // Commands and Params
            PopulateCommand(1, IDX_CMD1, IDX_P1_1);
            PopulateCommand(2, IDX_CMD2, IDX_P2_1);
            PopulateCommand(3, IDX_CMD3, IDX_P3_1);
            PopulateCommand(4, IDX_CMD4, IDX_P4_1);
        }

        private string entry_id_global_hack => _currentRow?[IDX_ID] ?? "";

        private void PopulateCommand(int num, int cmdIdx, int pStartIdx)
        {
            var box = this.FindName($"CommandBox{num}") as TextBox;
            var cmdId = GetField(cmdIdx);
            box.Text = cmdId;

            for (int i = 0; i < 6; i++)
            {
                var pBox = this.FindName($"ParamBox{num}_{i + 1}") as TextBox;
                pBox.Text = GetField(pStartIdx + i);
            }
            UpdateParamLabels(num, cmdId);
        }

        private void CommandBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;
            var box = sender as TextBox;
            var num = int.Parse(box.Tag.ToString());
            UpdateParamLabels(num, box.Text);
        }

        private void UpdateParamLabels(int num, string cmdId)
        {
            var nameBlock = this.FindName($"CommandName{num}") as TextBlock;
            cmdId = cmdId?.Trim() ?? "";

            if (commandLegends.TryGetValue(cmdId, out var legend) && legend.ValueKind == JsonValueKind.Object)
            {
                string cmdName = "Unknown";
                if (legend.TryGetProperty("name", out var nameProp))
                    cmdName = nameProp.GetString()?.Trim() ?? "Unknown";

                List<string> paramsList = new();
                if (legend.TryGetProperty("params", out var paramsProp) && paramsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in paramsProp.EnumerateArray())
                        paramsList.Add(p.GetString() ?? "");
                }

                // Se houver um 7º parâmetro, ele é um comentário explicativo
                if (paramsList.Count >= 7)
                {
                    string comment = paramsList[6];
                    if (!string.IsNullOrWhiteSpace(comment))
                    {
                        comment = comment.Trim().TrimStart('#').Trim();
                        cmdName += " #" + comment;
                    }
                }

                nameBlock.Text = cmdName;

                for (int i = 0; i < 6; i++)
                {
                    var label = this.FindName($"ParamLabel{num}_{i + 1}") as TextBlock;
                    string paramName = i < paramsList.Count ? paramsList[i] : null;
                    label.Text = string.IsNullOrWhiteSpace(paramName) ? $"Param {num}.{i + 1}" : paramName;
                }
            }
            else
            {
                nameBlock.Text = (string.IsNullOrWhiteSpace(cmdId) || cmdId == "0") ? "None" : "Unknown Command";
                for (int i = 0; i < 6; i++)
                {
                    var label = this.FindName($"ParamLabel{num}_{i + 1}") as TextBlock;
                    label.Text = $"Param {num}.{i + 1}";
                }
            }
        }

        private string PromptNewId(string title, string label)
        {
            var window = new Window
            {
                Title = title,
                Width = 360,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1C22")),
                Foreground = Brushes.White,
                Owner = Window.GetWindow(this)
            };

            var root = new Grid { Margin = new Thickness(15) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var text = new TextBlock
            {
                Text = label,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = Brushes.AliceBlue
            };

            var input = new TextBox
            {
                Height = 30,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#23262E")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333")),
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(5, 0, 0, 0)
            };

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };

            var btnOk = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 28,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E8B57")),
                Foreground = Brushes.White
            };

            var btnCancel = new Button
            {
                Content = "Cancelar",
                Width = 80,
                Height = 28,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A52A2A")),
                Foreground = Brushes.White
            };

            string resultValue = null;
            btnOk.Click += (s, e) => { resultValue = input.Text; window.Close(); };
            btnCancel.Click += (s, e) => { resultValue = null; window.Close(); };

            buttons.Children.Add(btnOk);
            buttons.Children.Add(btnCancel);

            Grid.SetRow(text, 0);
            Grid.SetRow(input, 1);
            Grid.SetRow(buttons, 2);

            root.Children.Add(text);
            root.Children.Add(input);
            root.Children.Add(buttons);

            window.Content = root;
            window.ShowDialog();

            return resultValue;
        }

        private void Clone_Click(object sender, RoutedEventArgs e)
        {
            if (EnchantList.SelectedItem is not EnchantEntry selectedEntry)
            {
                MessageBox.Show("Selecione um encantamento para clonar.", "Clonar", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string selectedId = selectedEntry.Id;
            string newId = PromptNewId("Clonar Encantamento", "Informe o novo ID:");
            if (newId == null) return;

            newId = newId.Trim();
            if (string.IsNullOrWhiteSpace(newId) || db.Rows.ContainsKey(newId))
            {
                MessageBox.Show("O ID não pode ser vazio ou já existente.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                string sEnchPath = Path.Combine(clientPath, "data", "db", "S_Enchant.ini");
                string cEnchPath = Path.Combine(clientPath, "data", "db", "C_Enchant.ini");
                string tEnchPath = Path.Combine(clientPath, "data", "translate", "T_Enchant.ini");

                AppendClonedBlock(sEnchPath, Encoding.GetEncoding(950), selectedId, newId);
                if (File.Exists(cEnchPath))
                    AppendClonedBlock(cEnchPath, Encoding.GetEncoding(950), selectedId, newId);
                
                // Para o T_Enchant, usamos AppendClonedBlock também pois ele preserva o formato pipe-first
                AppendClonedBlock(tEnchPath, Encoding.GetEncoding(1252), selectedId, newId);

                MessageBox.Show("Clonado com sucesso!", "Enchant", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadDatabase();
                
                var newItem = EnchantListSource.FirstOrDefault(x => x.Id == newId);
                if (newItem != null) EnchantList.SelectedItem = newItem;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao salvar clone:\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Flag_Click(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            ulong total = 0;
            if (FlagsItemsControl.ItemsSource is List<FlagItem> flagsList)
            {
                foreach (var item in flagsList)
                {
                    if (item.IsChecked) total |= item.Value;
                }
                FlagValueText.Text = $"Flag: {total} (0x{total:X})";
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = SearchBox.Text.ToLower();
            var filtered = db.Rows.Select(r => new EnchantEntry 
            { 
                Id = r.Key, 
                Name = db.Translations.TryGetValue(r.Key, out var t) ? t.Name : "Unknown" 
            })
            .Where(x => x.Id.Contains(filter) || x.Name.ToLower().Contains(filter))
            .OrderBy(x => int.TryParse(x.Id, out var id) ? id : 0);

            Enchants.Clear();
            EnchantListSource.Clear();
            foreach (var item in filtered) 
            {
                Enchants.Add(item.Id, item);
                EnchantListSource.Add(item);
            }
        }

        private void Reload_Click(object sender, RoutedEventArgs e) => LoadDatabase();

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_currentRow == null) return;

            string id = entry_id_global_hack;
            var editedRow = BuildEditedRowFromControls();

            try
            {
                string sEnchPath = Path.Combine(clientPath, "data", "db", "S_Enchant.ini");
                string cEnchPath = Path.Combine(clientPath, "data", "db", "C_Enchant.ini");
                string tEnchPath = Path.Combine(clientPath, "data", "translate", "T_Enchant.ini");

                PatchIniRowInPlace(sEnchPath, Encoding.GetEncoding(950), id, id, editedRow);
                if (File.Exists(cEnchPath))
                    PatchIniRowInPlace(cEnchPath, Encoding.GetEncoding(950), id, id, editedRow);

                // Patch translations
                PatchTranslateRowInPlace(tEnchPath, Encoding.GetEncoding(1252), id, 
                    NameBox.Text, TooltipBox.Text, TransTooltipBox.Text, TransNameBox.Text);

                MessageBox.Show("Alterações salvas com sucesso!", "Enchant", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadDatabase();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao salvar: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<string> BuildEditedRowFromControls()
        {
            var row = new List<string>(_currentRow);
            row[IDX_ICON] = IconFileBox.Text;
            row[IDX_ANIM] = AnimIdBox.Text;
            row[IDX_EFFECT] = EffectIdBox.Text;
            row[IDX_NODE] = EffectNodeBox.Text;
            row[IDX_TYPE] = (TypeCombo.SelectedItem as ComboOption)?.Value.ToString() ?? GetField(IDX_TYPE);
            row[IDX_CATEGORY] = (CategoryCombo.SelectedItem as ComboOption)?.Value.ToString() ?? GetField(IDX_CATEGORY);
            row[IDX_PERIOD] = PeriodBox.Text;
            row[IDX_COOLDOWN] = CooldownBox.Text;
            row[IDX_MAX_STACK] = MaxStackBox.Text;
            row[IDX_IMMUNE] = ImmuneMobBox.Text;
            row[IDX_HIWORD] = HiWordBox.Text;
            row[IDX_LOWWORD] = LowWordBox.Text;
            row[IDX_NEXT_LVL] = NextLevelIdBox.Text;
            row[IDX_WEAPON_FLAG] = ClassBox.Text;

            // Transition
            row[IDX_TRANS_TYPE] = (TransTypeCombo.SelectedItem as ComboOption)?.Value.ToString() ?? GetField(IDX_TRANS_TYPE);
            row[IDX_TRANS_RATE] = TransRateBox.Text;
            row[IDX_TRANS_DUR] = TransDurationBox.Text;
            row[IDX_TRANS_PERIOD] = TransPeriodBox.Text;
            row[IDX_TRANS_ICON] = TransIconBox.Text;
            row[IDX_TRANS_ENC_TYPE] = (TransEncTypeCombo.SelectedItem as ComboOption)?.Value.ToString() ?? GetField(IDX_TRANS_ENC_TYPE);
            row[IDX_TRANS_CAT] = (TransCategoryCombo.SelectedItem as ComboOption)?.Value.ToString() ?? GetField(IDX_TRANS_CAT);
            row[IDX_TRANS_ANIM] = TransAnimBox.Text;
            row[IDX_TRANS_EFF] = TransEffectBox.Text;
            row[IDX_TRANS_NODE] = TransNodeBox.Text;
            row[IDX_TRANS_EFF_DUR] = TransEffDurBox.Text;
            row[IDX_TRANS_DUR_NODE] = TransDurNodeBox.Text;
            row[IDX_TRANS_HIWORD] = TransHiWordBox.Text;
            row[IDX_TRANS_LOWWORD] = TransLowWordBox.Text;
            row[IDX_TRANS_FLAG] = TransFlagBox.Text;

            // Flags
            ulong flagValue = 0;
            if (FlagsItemsControl.ItemsSource is List<FlagItem> flagsList)
            {
                foreach (var item in flagsList)
                {
                    if (item.IsChecked) flagValue |= item.Value;
                }
            }
            row[IDX_FLAG] = flagValue.ToString();

            // Commands
            SetCommandRow(row, 1, IDX_CMD1, IDX_P1_1);
            SetCommandRow(row, 2, IDX_CMD2, IDX_P2_1);
            SetCommandRow(row, 3, IDX_CMD3, IDX_P3_1);
            SetCommandRow(row, 4, IDX_CMD4, IDX_P4_1);

            return row;
        }

        private void SetCommandRow(List<string> row, int num, int cmdIdx, int pStartIdx)
        {
            var box = this.FindName($"CommandBox{num}") as TextBox;
            row[cmdIdx] = box.Text;
            for (int i = 0; i < 6; i++)
            {
                var pBox = this.FindName($"ParamBox{num}_{i + 1}") as TextBox;
                row[pStartIdx + i] = pBox.Text;
            }
        }

        private HashSet<int> GetChangedColumnIndices(List<string> original, List<string> edited)
        {
            var changed = new HashSet<int>();
            int max = Math.Max(original.Count, edited.Count);
            for (int i = 0; i < max; i++)
            {
                string o = i < original.Count ? original[i] : "";
                string e = i < edited.Count ? edited[i] : "";
                if (o != e) changed.Add(i);
            }
            return changed;
        }

        private void PatchIniRowInPlace(string path, Encoding enc, string id, HashSet<int> changed, List<string> row)
        {
            if (!File.Exists(path)) return;
            var lines = File.ReadAllLines(path, enc);
            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split('|').ToList();
                if (parts.Count > 0 && parts[0].Trim() == id)
                {
                    while (parts.Count < row.Count) parts.Add("");
                    foreach (int idx in changed) parts[idx] = row[idx];
                    lines[i] = string.Join("|", parts);
                    File.WriteAllLines(path, lines, enc);
                    return;
                }
            }
        }

        private void PatchTranslateRowInPlace(string path, Encoding enc, string id, string n, string t1, string t2, string n2)
        {
            if (!File.Exists(path)) return;
            var lines = File.ReadAllLines(path, enc);
            bool found = false;
            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split('|').ToList();
                int idIdx = 0;
                if (parts.Count > 0 && string.IsNullOrWhiteSpace(parts[0]))
                {
                    idIdx = 1;
                }

                if (parts.Count > idIdx && parts[idIdx].Trim() == id)
                {
                    while (parts.Count < (idIdx + 5)) parts.Add("");
                    parts[idIdx + 1] = n;
                    parts[idIdx + 2] = t1;
                    parts[idIdx + 3] = t2;
                    parts[idIdx + 4] = n2;
                    
                    // Re-join while preserving leading pipe if it existed
                    lines[i] = string.Join("|", parts);
                    found = true;
                    break;
                }
            }

            if (found)
            {
                File.WriteAllLines(path, lines, enc);
            }
            else
            {
                // If not found, append with format that matches the module (usually |ID|Name|...)
                var newList = lines.ToList();
                newList.Add($"|{id}|{n}|{t1}|{t2}|{n2}|");
                File.WriteAllLines(path, newList, enc);
            }
        }

        // Helpers
        private string GetField(int idx) => (_currentRow != null && idx < _currentRow.Count) ? _currentRow[idx] : "";
        private int GetFieldInt(int idx) => int.TryParse(GetField(idx), out var v) ? v : 0;
        private ulong GetFieldULong(int idx) => ulong.TryParse(GetField(idx), out var v) ? v : 0;

        private void SelectComboByValue(ComboBox combo, int value)
        {
            if (combo == null) return;
            foreach (var item in combo.Items)
            {
                if (item is ComboOption opt && opt.Value == value)
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
            combo.SelectedIndex = -1;
        }

        public class EnchantEntry
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public override string ToString() => $"{Id} - {Name}";
        }

        public class ComboOption
        {
            public int Value { get; set; }
            public string Label { get; set; }
            public override string ToString() => Label;
        }

        public class FlagItem : INotifyPropertyChanged
        {
            public string Label { get; set; }
            public ulong Value { get; set; }
            private bool _isChecked;
            public bool IsChecked 
            { 
                get => _isChecked; 
                set { _isChecked = value; OnPropertyChanged(nameof(IsChecked)); } 
            }
            public FlagItem(string l, ulong v) { Label = l; Value = v; }
            
            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
