using GrandFantasiaINIEditor.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GrandFantasiaINIEditor.Modules.ElfCombine
{
    public partial class ElfCombineView : UserControl
    {
        private readonly string clientPath;
        private readonly string schemasPath;
        private GenericIniDb db;

        private readonly ObservableCollection<CombineEntry> Entries = new();
        private readonly ObservableCollection<EffectRow>    Effects  = new();
        private readonly ObservableCollection<ElfCmdPart> SelectedCmdParts = new();

        private readonly Dictionary<string, string>      _itemNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string>      _itemIcons = new(StringComparer.OrdinalIgnoreCase);

        private bool   _loading;
        private bool   _suppressSelectionChanged;
        private List<string> _currentRow;
        private string _lastSelectedId;
        private List<string> _originalRowSnapshot;

        private CancellationTokenSource _searchCts;
        private CancellationTokenSource _resultLookupCts;
        private CancellationTokenSource _mat1LookupCts;
        private CancellationTokenSource _mat2LookupCts;
        private CancellationTokenSource _mat3LookupCts;

        // ── Column indices ────────────────────────────────────────────────────
        private const int IDX_ID           = 0;
        private const int IDX_SKILL_TYPE   = 1;
        private const int IDX_COMBINE_TYPE = 2;
        private const int IDX_LEVEL_MIN    = 3;
        private const int IDX_LEVEL_MAX    = 4;
        private const int IDX_EXP_RATE     = 5;
        private const int IDX_EXP          = 6;
        private const int IDX_DURATION     = 7;
        private const int IDX_NEED_ENERGY  = 8;
        private const int IDX_NEED_GOLD    = 9;
        private const int IDX_ITEM_ID      = 10;
        private const int IDX_ITEM_NAME    = 11;
        private const int IDX_MAT1_ID      = 12;
        private const int IDX_MAT1_NAME    = 13;
        private const int IDX_MAT1_QTY     = 14;
        private const int IDX_MAT2_ID      = 15;
        private const int IDX_MAT2_NAME    = 16;
        private const int IDX_MAT2_QTY     = 17;
        private const int IDX_MAT3_ID      = 18;
        private const int IDX_MAT3_NAME    = 19;
        private const int IDX_MAT3_QTY     = 20;
        private const int IDX_SUCCESS_RATE = 21;
        // PostProbabilityN at 22,24,26,28,30,32,34,36
        // OptionCmdN      at 23,25,27,29,31,33,35,37
        private const int IDX_TIP          = 38;

        private const int NUM_EFFECTS = 8;

        // ── SkillType dict ────────────────────────────────────────────────────
        private static readonly Dictionary<int, string> SKILL_TYPE = new()
        {
            {0,"None"},{1,"Mining"},{2,"Plant"},{3,"Hunting"},{4,"Decompose"},
            {5,"Sword"},{6,"Axe"},{7,"Mace"},{8,"Bow"},{9,"Gun"},{10,"Staff"},
            {11,"Shield"},{12,"HolyItem"},{13,"Fighter"},{14,"Hunter"},{15,"Caster"},
            {16,"Acolyte"},{17,"Machine"},{18,"HeavyMachine"},{19,"Mech"},
            {20,"CrystalKatana"},{21,"CrystalKey"},{22,"CrystalEquip"},
            {23,"Gas"},{24,"Pigment"},{25,"Map"},{26,"SoulCrystal"},
            {27,"Gas1"},{28,"Gas2"},{29,"Gas3"},{30,"Gas4"},{31,"Gas5"},
            {32,"Gas6"},{33,"Gas7"},{34,"Gas8"},{35,"Gas9"},{36,"Gas10"},
            {37,"Gas11"},{38,"Gas12"},
            {39,"Pigment1"},{40,"Pigment2"},{41,"Pigment3"},{42,"Pigment4"},
            {43,"Pigment5"},{44,"Pigment6"},{45,"Pigment7"},{46,"Pigment8"},
            {47,"Pigment9"},{48,"Pigment10"},{49,"Pigment11"},{50,"Pigment12"},
            {51,"Map1"},{52,"Map2"},{53,"Map3"},{54,"Map4"},{55,"Map5"},
            {56,"Map6"},{57,"Map7"},{58,"Map8"},{59,"Map9"},{60,"Map10"},
            {61,"Map11"},{62,"Map12"},
            {63,"SoulCrystal1"},{64,"SoulCrystal2"},{65,"SoulCrystal3"},
            {66,"SoulCrystal4"},{67,"SoulCrystal5"},{68,"SoulCrystal6"},
            {69,"SoulCrystal7"},{70,"SoulCrystal8"},{71,"SoulCrystal9"},
            {72,"SoulCrystal10"},{73,"SoulCrystal11"},{74,"SoulCrystal12"},
            {75,"Travel"},{76,"End"}
        };

        private static readonly Dictionary<int, string> COMBINE_TYPE = new()
        {
            {1,"Fortalecer (Strong)"},{2,"Combinar (Combine)"},
            {3,"Fabricar (Make)"},{4,"Cozinhar (Cook)"},{5,"Combinar (Match)"}
        };

        private static readonly Dictionary<int, string> REPUTATION = new()
        {
            { 1, "KASLOW" }, { 2, "JALE" }, { 3, "ILYA" }, { 4, "ELSALAND" },
            { 200, "SANTA KASLOW" }, { 201, "VINGANÇA DE ILYA" }, { 202, "MERCENÁRIOS DE JALE" },
            { 203, "ASSOCIAÇÃO DE GAS KASLOW" }, { 204, "ASSOCIAÇÃO DE ARTE JALE" },
            { 5, "QUATRO MARES" }, { 6, "COCO VERMELHO" }, { 7, "ANGONIELA" },
            { 11, "LIVROS DE QUILL" }, { 20, "GUARDIÃO DE SHAPAEL" }, { 12, "MINERAÇÃO JALE" },
            { 13, "COLETA ILYA" }, { 14, "CAÇA KASLOW" }, { 15, "CAÇADORES DE DEMÔNIOS" },
            { 17, "SPRITE SOMBRIO" }, { 21, "MENSAGEIRO SPRITE" }, { 16, "PVP" },
            { 18, "GVG" }, { 19, "CLUBE PK (CHANNEL PVP)" }, { 100, "CLASSE" },
            { 22, "BODOR" }, { 23, "ALICE" }, { 24, "RONTO" }, { 25, "SMULCA" },
            { 26, "EWAN" }, { 27, "BAHADO" }, { 28, "QUILL" }, { 29, "MOSUNK" },
            { 30, "JUNO" }, { 31, "SIROPAS" }, { 32, "CONGELADO = ILYANA" }, { 33, "GINNY" }
        };

        // ── Inner models ──────────────────────────────────────────────────────
        private sealed class CombineEntry
        {
            public string Id   { get; set; }
            public string Name { get; set; }
            public BitmapSource Icon { get; set; }
        }

        public sealed class EffectRow : INotifyPropertyChanged
        {
            private string _probability;
            private string _command;

            public string Label       { get; init; }
            public string Probability { get => _probability; set { _probability = value; OnPropertyChanged(); } }
            public string Command     { get => _command;     set { _command     = value; OnPropertyChanged(); } }

            public event PropertyChangedEventHandler PropertyChanged;
            private void OnPropertyChanged([CallerMemberName] string name = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private sealed class ComboOption
        {
            public int    Value { get; set; }
            public string Label { get; set; }
            public override string ToString() => Label;
        }

        // ── Constructor ───────────────────────────────────────────────────────
        public ElfCombineView(string clientPath, string schemasPath)
        {
            InitializeComponent();

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            this.clientPath  = clientPath;
            this.schemasPath = schemasPath;

            CombineList.ItemsSource = Entries;
            EffectsList.ItemsSource = Effects;
            CmdPartsList.ItemsSource = SelectedCmdParts;

            PopulateCombos();
            InitEffectRows();
            LoadItemLookups();
            LoadDatabase();
        }

        // ── Setup ─────────────────────────────────────────────────────────────
        private void PopulateCombos()
        {
            SkillTypeCombo.ItemsSource = SKILL_TYPE
                .Select(kv => new ComboOption { Value = kv.Key, Label = $"{kv.Value} ({kv.Key})" })
                .OrderBy(x => x.Value).ToList();

            CombineTypeCombo.ItemsSource = COMBINE_TYPE
                .Select(kv => new ComboOption { Value = kv.Key, Label = kv.Value })
                .OrderBy(x => x.Value).ToList();
        }

        private void InitEffectRows()
        {
            Effects.Clear();
            for (int i = 1; i <= NUM_EFFECTS; i++)
                Effects.Add(new EffectRow { Label = $"Efeito {i}", Probability = "0", Command = "" });
        }

        // ── Data loading ──────────────────────────────────────────────────────
        private void LoadDatabase()
        {
            try
            {
                db = GenericIniLoader.Load(clientPath, schemasPath, "S_ElfCombine.ini");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar S_ElfCombine.ini:\n{ex.Message}",
                    "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            RefreshList(filter: "");
        }

        private void LoadItemLookups()
        {
            _itemNames.Clear();

            // Names: T_Item.ini col 1
            LoadNamesFromFile(Path.Combine(clientPath, "data", "translate", "T_Item.ini"));
            LoadNamesFromFile(Path.Combine(clientPath, "data", "translate", "T_ItemMall.ini"));

            // Icons: S_Item.ini col 1
            string dataDb = Path.Combine(clientPath, "data", "db");
            if (!Directory.Exists(dataDb)) dataDb = Path.Combine(clientPath, "Data", "db");

            LoadIconsFromFile(Path.Combine(dataDb, "S_Item.ini"));
            LoadIconsFromFile(Path.Combine(dataDb, "S_ItemMall.ini"));
        }


        private void LoadNamesFromFile(string path)
        {
            if (!File.Exists(path)) return;
            var lines = File.ReadAllLines(path, Encoding.GetEncoding(1252));
            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split(new[] { '|' }, 3);
                if (parts.Length < 2) continue;
                string id   = (parts[0] ?? "").Trim();
                string name = (parts[1] ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(id))
                    _itemNames[id] = name;
            }
        }
        private void LoadIconsFromFile(string path)
        {
            if (!File.Exists(path)) return;
            var lines = File.ReadAllLines(path, Encoding.GetEncoding(950));
            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split('|');
                if (parts.Length < 2) continue;
                string id = parts[0].Trim();
                string icon = parts[1].Trim();
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(icon))
                    _itemIcons[id] = icon;
            }
        }

        // ── Icon helper ───────────────────────────────────────────────────────
        private BitmapSource LoadIconByItemId(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            if (!_itemIcons.TryGetValue(itemId, out var iconName) || string.IsNullOrEmpty(iconName)) return null;

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

        private string GetItemName(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return "—";
            return _itemNames.TryGetValue(itemId.Trim(), out var n) && !string.IsNullOrWhiteSpace(n) ? n : $"ID {itemId}";
        }

        // ── List management ───────────────────────────────────────────────────
        private void RefreshList(string filter)
        {
            if (db == null) return;
            _suppressSelectionChanged = true;

            Entries.Clear();
            var rows = db.Rows
                .Where(kv => ElfRowMatchesFilter(kv.Key, kv.Value, filter))
                .OrderBy(kv => kv.Key, Comparer<string>.Create(CompareIds));

            foreach (var kv in rows)
            {
                string resultId = GetValue(kv.Value, IDX_ITEM_ID);
                Entries.Add(new CombineEntry
                {
                    Id   = kv.Key,
                    Name = GetItemName(resultId),
                    Icon = LoadIconByItemId(resultId)
                });
            }

            _suppressSelectionChanged = false;

            if (_lastSelectedId != null)
            {
                var match = Entries.FirstOrDefault(e => e.Id == _lastSelectedId);
                if (match != null) CombineList.SelectedItem = match;
            }
        }

        // ── Selection / population ────────────────────────────────────────────
        private void EffectsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EffectsList.SelectedItem is not EffectRow effect)
            {
                SelectedCmdParts.Clear();
                return;
            }

            LoadCmdPartsFromRaw(effect.Command);
        }

        private void LoadCmdPartsFromRaw(string raw)
        {
            SelectedCmdParts.Clear();
            
            // Pattern: #:C1:C2:C3:C4:C5:#:A1:A2:A3:A4:A5:A6
            // Split by ':' typically yields 13 parts if fully populated with delimiters
            
            var parts = (raw ?? string.Empty).Split(':');
            var result = new List<string>();

            // We want exactly 11 editable slots (5 conditions, 6 actions)
            // We ignore the '#' markers at index 0 and 6
            
            for (int i = 0; i < 11; i++)
            {
                int sourceIdx = (i < 5) ? i + 1 : i + 2;
                string text = (sourceIdx < parts.Length) ? parts[sourceIdx].Trim() : string.Empty;
                
                var partValue = new ElfCmdPart
                {
                    Index = i,
                    Text = text,
                    Description = GetCmdPartDescription(text)
                };
                SelectedCmdParts.Add(partValue);
            }
        }

        private void CmdPart_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading || sender is not TextBox tb || tb.Tag is not ElfCmdPart part)
                return;

            part.Text = tb.Text;
            part.Description = GetCmdPartDescription(part.Text);

            // Update the main effect command
            if (EffectsList.SelectedItem is EffectRow selectedEffect)
            {
                selectedEffect.Command = BuildRawCommandFromParts();
            }
        }

        private string BuildRawCommandFromParts()
        {
            var parts = new string[13];
            parts[0] = "#";
            parts[6] = "#";

            for (int i = 0; i < 11; i++)
            {
                int destIdx = (i < 5) ? i + 1 : i + 2;
                parts[destIdx] = SelectedCmdParts[i].Text ?? string.Empty;
            }

            return string.Join(":", parts);
        }

        private string GetCmdPartDescription(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd)) return string.Empty;

            try
            {
                // If_Result 1
                if (cmd.StartsWith("If_Result", StringComparison.OrdinalIgnoreCase))
                {
                    return cmd.EndsWith("1") ? "se resultado for sucesso" : "se resultado negativo";
                }

                // If_ElfMood <= 200
                var match = Regex.Match(cmd, @"(If_)?Elf(Mood|Familiar|Energy)\s*([><=]+)\s*(\d+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string type = match.Groups[2].Value;
                    string op = match.Groups[3].Value;
                    string val = match.Groups[4].Value;
                    string label = type switch { "Mood" => "humor do sprite", "Familiar" => "relacionamento", "Energy" => "estamina", _ => type };
                    return $"{label} {GetOperatorLabelPT(op)} {val}";
                }

                // ElfMood + 210
                match = Regex.Match(cmd, @"Elf(Mood|Familiar|Energy)\s*([+-])\s*(\d+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string type = match.Groups[1].Value;
                    string op = match.Groups[2].Value;
                    string val = match.Groups[3].Value;
                    string label = type switch { "Mood" => "humor", "Familiar" => "relaçao", "Energy" => "estamina", _ => type };
                    return $"{op}{val} de {label}";
                }

                // TextIndex 6011
                match = Regex.Match(cmd, @"TextIndex\s+(\d+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return $"dialogo do evento {match.Groups[1].Value}";
                }

                // Reputation 21 - 100
                match = Regex.Match(cmd, @"Reputation\s+(\d+)\s*(-?\s*\d+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    int repId = int.Parse(match.Groups[1].Value);
                    string val = match.Groups[2].Value.Replace(" ", "");
                    string repName = REPUTATION.TryGetValue(repId, out var n) ? n : repId.ToString();
                    return $"reputação {repName} {val}";
                }

                // GiveItem / GivenItem 35247 1
                match = Regex.Match(cmd, @"Give[n]?Item\s+(\d+)\s+(\d+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string itemId = match.Groups[1].Value;
                    string qty = match.Groups[2].Value;
                    return $"ganha o item {GetItemName(itemId)} ({itemId}) x{qty}";
                }

                // SkillExp 5 40
                match = Regex.Match(cmd, @"SkillExp\s+(\d+)\s+(\d+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    int skillId = int.Parse(match.Groups[1].Value);
                    string qty = match.Groups[2].Value;
                    string skillName = SKILL_TYPE.TryGetValue(skillId, out var sn) ? sn : skillId.ToString();
                    return $"ganha {qty} de EXP em {skillName} ({skillId})";
                }

                // ElfLeave 30
                match = Regex.Match(cmd, @"ElfLeave\s+(\d+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return $"sprite foge por {match.Groups[1].Value}";
                }
                if (cmd.Equals("ElfLeave", StringComparison.OrdinalIgnoreCase))
                {
                    return "sprite foge permanentemente";
                }

            }
            catch { }

            return "Comando desconhecido";
        }

        private string GetOperatorLabelPT(string op)
        {
            return op switch
            {
                ">=" => "maior ou igual a",
                "<=" => "menor ou igual a",
                "==" => "igual a",
                ">" => "maior que",
                "<" => "menor que",
                _ => op
            };
        }

        public class ElfCmdPart : INotifyPropertyChanged
        {
            public int Index { get; set; }
            private string _text;
            private string _description;

            public string Text
            {
                get => _text;
                set { _text = value; OnPropertyChanged(nameof(Text)); }
            }

            public string Description
            {
                get => _description;
                set { _description = value; OnPropertyChanged(nameof(Description)); OnPropertyChanged(nameof(LegendText)); }
            }

            public string LegendText => string.IsNullOrWhiteSpace(Description) ? string.Empty : $"[ {Description} ]";

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void CombineList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionChanged) return;
            if (CombineList.SelectedItem is not CombineEntry entry) return;

            if (_lastSelectedId != null && _lastSelectedId != entry.Id)
            {
                if (!ConfirmSaveIfNeeded())
                {
                    _suppressSelectionChanged = true;
                    CombineList.SelectedItem = Entries.FirstOrDefault(x => x.Id == _lastSelectedId);
                    _suppressSelectionChanged = false;
                    return;
                }
            }

            if (!db.Rows.TryGetValue(entry.Id, out var row)) return;

            int cols = db?.Schema?.Columns ?? 40;
            var normalizedRow = new List<string>(row);
            while (normalizedRow.Count < cols) normalizedRow.Add("");

            _loading = true;
            _lastSelectedId      = entry.Id;
            _currentRow          = new List<string>(normalizedRow);

            PopulateFields(normalizedRow);
            _lastSelectedId      = entry.Id;
            _originalRowSnapshot = BuildRowFromControls();
            _loading = false;

            // Select first effect by default to show structured editor
            if (EffectsList.Items.Count > 0)
                EffectsList.SelectedIndex = 0;
        }

        private bool HasChanges()
        {
            if (_currentRow == null || _originalRowSnapshot == null) return false;
            var current = BuildRowFromControls();
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
                try
                {
                    Save_Click(null, null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Erro ao salvar: " + ex.Message);
                    return false;
                }
            }
            return true;
        }

        private void PopulateFields(List<string> row)
        {
            SetText(IdBox,           GetValue(row, IDX_ID));
            SetText(SuccessRateBox,  GetValue(row, IDX_SUCCESS_RATE));
            SetText(LevelMinBox,     GetValue(row, IDX_LEVEL_MIN));
            SetText(LevelMaxBox,     GetValue(row, IDX_LEVEL_MAX));
            SetText(ExpRateBox,      GetValue(row, IDX_EXP_RATE));
            SetText(ExpBox,          GetValue(row, IDX_EXP));
            SetText(DurationBox,     GetValue(row, IDX_DURATION));
            SetText(NeedEnergyBox,   GetValue(row, IDX_NEED_ENERGY));
            SetText(NeedGoldBox,     GetValue(row, IDX_NEED_GOLD));
            SetText(TipBox,          GetValue(row, IDX_TIP));

            // SkillType combo
            // SkillType combo: treat empty/invalid as 0 (None)
            string sSt = GetValue(row, IDX_SKILL_TYPE);
            SkillTypeCombo.SelectedValue = int.TryParse(sSt, out int st) ? st : 0;

            // CombineType combo: treat empty/invalid as 1 (Mining default)
            string sCt = GetValue(row, IDX_COMBINE_TYPE);
            CombineTypeCombo.SelectedValue = int.TryParse(sCt, out int ct) ? ct : 1;

            // Result item
            string resultId = GetValue(row, IDX_ITEM_ID);
            _loading = true;
            SetText(ResultItemIdBox, resultId);
            _loading = false;
            ResultItemNameText.Text = GetItemName(resultId);
            ResultIcon.Source       = LoadIconByItemId(resultId);

            // Materials
            SetMaterialField(Mat1IdBox, Mat1NameText, Mat1Icon, Mat1QtyBox,
                GetValue(row, IDX_MAT1_ID), GetValue(row, IDX_MAT1_QTY));
            SetMaterialField(Mat2IdBox, Mat2NameText, Mat2Icon, Mat2QtyBox,
                GetValue(row, IDX_MAT2_ID), GetValue(row, IDX_MAT2_QTY));
            SetMaterialField(Mat3IdBox, Mat3NameText, Mat3Icon, Mat3QtyBox,
                GetValue(row, IDX_MAT3_ID), GetValue(row, IDX_MAT3_QTY));

            // Effects: Refined mapping based on user documentation (40 cols)
            // 21: SuccessRate, 22: Unknown, 23: Prob1, 24: Cmd1... 37: Prob8, 38: Cmd8, 39: Tip
            int count = row.Count;
            int probBase = (count >= 40) ? 23 : 22;
            int cmdBase  = (count >= 40) ? 24 : 23;
            int tipIdx   = count - 1;

            for (int i = 0; i < NUM_EFFECTS; i++)
            {
                int probIdx = probBase + i * 2;
                int cmdIdx  = cmdBase  + i * 2;
                
                if (probIdx < count) Effects[i].Probability = GetValue(row, probIdx);
                if (cmdIdx <  count) Effects[i].Command     = GetValue(row, cmdIdx);
            }

            if (tipIdx >= 0 && tipIdx < row.Count) SetText(TipBox, GetValue(row, tipIdx));
        }

        private void SetMaterialField(TextBox idBox, TextBlock nameBlock, Image iconImg,
            TextBox qtyBox, string itemId, string qty)
        {
            _loading = true;
            SetText(idBox, itemId);
            _loading = false;
            nameBlock.Text  = GetItemName(itemId);
            iconImg.Source  = LoadIconByItemId(itemId);
            SetText(qtyBox, qty);
        }

        // ── Async icon/name lookup on ID change ───────────────────────────────
        private void ResultItemId_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;
            string id = ResultItemIdBox.Text.Trim();
            _resultLookupCts?.Cancel();
            _resultLookupCts = new CancellationTokenSource();
            var token = _resultLookupCts.Token;
            _ = UpdateItemLookupAsync(id, ResultItemNameText, ResultIcon, token);
        }

        private void Mat1Id_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;
            string id = Mat1IdBox.Text.Trim();
            _mat1LookupCts?.Cancel();
            _mat1LookupCts = new CancellationTokenSource();
            _ = UpdateItemLookupAsync(id, Mat1NameText, Mat1Icon, _mat1LookupCts.Token);
        }

        private void Mat2Id_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;
            string id = Mat2IdBox.Text.Trim();
            _mat2LookupCts?.Cancel();
            _mat2LookupCts = new CancellationTokenSource();
            _ = UpdateItemLookupAsync(id, Mat2NameText, Mat2Icon, _mat2LookupCts.Token);
        }

        private void Mat3Id_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;
            string id = Mat3IdBox.Text.Trim();
            _mat3LookupCts?.Cancel();
            _mat3LookupCts = new CancellationTokenSource();
            _ = UpdateItemLookupAsync(id, Mat3NameText, Mat3Icon, _mat3LookupCts.Token);
        }

        private async Task UpdateItemLookupAsync(string itemId, TextBlock nameBlock,
            Image iconImg, CancellationToken token)
        {
            await Task.Delay(300, token).ConfigureAwait(false);
            if (token.IsCancellationRequested) return;

            string name   = GetItemName(itemId);
            var    bitmap = await Task.Run(() => LoadIconByItemId(itemId), token).ConfigureAwait(false);
            if (token.IsCancellationRequested) return;

            await Dispatcher.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested) return;
                nameBlock.Text  = name;
                iconImg.Source  = bitmap;
            });
        }

        // ── Search ────────────────────────────────────────────────────────────
        /// <summary>
        /// Returns true if the elf-combine row matches the filter.
        /// Checks: combine ID, result item ID+name, and all 3 material IDs+names.
        /// </summary>
        private bool ElfRowMatchesFilter(string combineId, List<string> row, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            // Combine row ID
            if (combineId.Contains(filter, StringComparison.OrdinalIgnoreCase))
                return true;

            // Result item
            string resultId = GetValue(row, IDX_ITEM_ID);
            if (!string.IsNullOrWhiteSpace(resultId) &&
                resultId.Contains(filter, StringComparison.OrdinalIgnoreCase))
                return true;
            if (_itemNames.TryGetValue(resultId, out var resultName) &&
                resultName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                return true;

            // Materials
            foreach (int col in new[] { IDX_MAT1_ID, IDX_MAT2_ID, IDX_MAT3_ID })
            {
                string matId = GetValue(row, col);
                if (string.IsNullOrWhiteSpace(matId)) continue;
                if (matId.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (_itemNames.TryGetValue(matId, out var matName) &&
                    matName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (db == null) return;
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token  = _searchCts.Token;
            string filter = (SearchBox.Text ?? "").Trim();

            try
            {
                await Task.Delay(200, token);
                if (!token.IsCancellationRequested)
                    RefreshList(filter);
            }
            catch (OperationCanceledException) { }
        }

        // ── Build row from controls ───────────────────────────────────────────
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
                if (!string.Equals(lineId, oldId, StringComparison.Ordinal)) continue;

                // Atualizar os campos
                int needed = Math.Max(parts.Count, editedRow.Count);
                while (parts.Count < needed) parts.Add(string.Empty);

                for (int j = 0; j < editedRow.Count; j++)
                {
                    parts[j] = editedRow[j] ?? string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(newId))
                    parts[IDX_ID] = newId;

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
                int pipe = line.IndexOf('|');
                bool startsWithId = pipe > 0 && int.TryParse(line.Substring(0, pipe).Trim(), out _);

                if (startsWithId)
                {
                    string currentId = line.Substring(0, pipe).Trim();
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

            int pipe = block.IndexOf('|');
            if (pipe > 0)
            {
                string newBlock = newId + block.Substring(pipe);
                string content = File.ReadAllText(path, encoding);
                using var sw = new StreamWriter(path, true, encoding);
                if (!string.IsNullOrEmpty(content) && !content.EndsWith("\n") && !content.EndsWith("\r"))
                {
                    sw.WriteLine();
                }
                sw.WriteLine(newBlock);
            }
        }

        private List<string> BuildRowFromControls()
        {
            var row = _currentRow != null
                ? new List<string>(_currentRow)
                : new List<string>(Enumerable.Repeat("", db.Schema.Columns));

            while (row.Count < db.Schema.Columns)
                row.Add("");

            Set(row, IDX_SKILL_TYPE,   (SkillTypeCombo.SelectedValue as int?)?.ToString() ?? "0");
            Set(row, IDX_COMBINE_TYPE, (CombineTypeCombo.SelectedValue as int?)?.ToString() ?? "1");
            Set(row, IDX_LEVEL_MIN,    GetBoxText(LevelMinBox));
            Set(row, IDX_LEVEL_MAX,    GetBoxText(LevelMaxBox));
            Set(row, IDX_EXP_RATE,     GetBoxText(ExpRateBox));
            Set(row, IDX_EXP,          GetBoxText(ExpBox));
            Set(row, IDX_DURATION,     GetBoxText(DurationBox));
            Set(row, IDX_NEED_ENERGY,  GetBoxText(NeedEnergyBox));
            Set(row, IDX_NEED_GOLD,    GetBoxText(NeedGoldBox));
            Set(row, IDX_SUCCESS_RATE, GetBoxText(SuccessRateBox));
            // Tip will be set at the end based on row count

            Set(row, IDX_ITEM_ID,   GetBoxText(ResultItemIdBox));
            Set(row, IDX_ITEM_NAME, "");  // raw name from ini — leave blank; game resolves via T_

            Set(row, IDX_MAT1_ID,   GetBoxText(Mat1IdBox));
            Set(row, IDX_MAT1_NAME, "");
            Set(row, IDX_MAT1_QTY,  GetBoxText(Mat1QtyBox));
            Set(row, IDX_MAT2_ID,   GetBoxText(Mat2IdBox));
            Set(row, IDX_MAT2_NAME, "");
            Set(row, IDX_MAT2_QTY,  GetBoxText(Mat2QtyBox));
            Set(row, IDX_MAT3_ID,   GetBoxText(Mat3IdBox));
            Set(row, IDX_MAT3_NAME, "");
            Set(row, IDX_MAT3_QTY,  GetBoxText(Mat3QtyBox));

            // Effects & Tip: Refined mapping (40 cols)
            int count = row.Count;
            int probBase = (count >= 40) ? 23 : 22;
            int cmdBase  = (count >= 40) ? 24 : 23;
            int tipIdx   = count - 1;

            for (int i = 0; i < NUM_EFFECTS; i++)
            {
                int probIdx = probBase + i * 2;
                int cmdIdx  = cmdBase  + i * 2;
                if (probIdx < count) Set(row, probIdx, Effects[i].Probability ?? "0");
                if (cmdIdx <  count) Set(row, cmdIdx,  Effects[i].Command     ?? "");
            }

            if (tipIdx >= 0) Set(row, tipIdx, GetBoxText(TipBox));

            return row;
        }

        // ── Save ──────────────────────────────────────────────────────────────
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (db == null || _currentRow == null) return;
            if (string.IsNullOrWhiteSpace(IdBox.Text)) return;

            string id = IdBox.Text.Trim();
            var newRow = BuildRowFromControls();
            
            _currentRow      = new List<string>(newRow);
            _originalRowSnapshot = new List<string>(newRow);

            try
            {
                string sPath = db.FilePath;
                string cPath = Path.Combine(Path.GetDirectoryName(sPath) ?? string.Empty, "C_ElfCombine.ini");

                PatchIniRowInPlace(sPath, Encoding.GetEncoding(950), id, id, newRow);
                if (File.Exists(cPath))
                    PatchIniRowInPlace(cPath, Encoding.GetEncoding(950), id, id, newRow);

                db.Rows[id] = newRow;

                MessageBox.Show("Salvo com sucesso!", "ElfCombine", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshList((SearchBox.Text ?? "").Trim());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao salvar:\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Clone ─────────────────────────────────────────────────────────────
        private void Clone_Click(object sender, RoutedEventArgs e)
        {
            if (db == null || _currentRow == null) return;

            var dlg = new InputDialog("Novo ID para o clone:") { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.InputText)) return;

            string newId = dlg.InputText.Trim();
            if (db.Rows.ContainsKey(newId))
            {
                MessageBox.Show("ID já existe.", "Erro", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string sPath = db.FilePath;
                string cPath = Path.Combine(Path.GetDirectoryName(sPath) ?? string.Empty, "C_ElfCombine.ini");

                AppendClonedBlock(sPath, Encoding.GetEncoding(950), _lastSelectedId, newId);
                if (File.Exists(cPath))
                    AppendClonedBlock(cPath, Encoding.GetEncoding(950), _lastSelectedId, newId);

                // Atualizar o banco em memória para refletir a nova linha
                var cloned = new List<string>(_currentRow);
                if (cloned.Count > IDX_ID) cloned[IDX_ID] = newId;
                db.Rows[newId] = cloned;

                RefreshList((SearchBox.Text ?? "").Trim());
                _lastSelectedId = newId;
                var match = Entries.FirstOrDefault(e2 => e2.Id == newId);
                if (match != null) CombineList.SelectedItem = match;

                MessageBox.Show("Clonado com sucesso!", "ElfCombine", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao salvar clone:\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Reload ────────────────────────────────────────────────────────────
        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            _currentRow          = null;
            _originalRowSnapshot = null;
            _lastSelectedId      = null;
            LoadItemLookups();
            LoadDatabase();
        }

        // ── File I/O ──────────────────────────────────────────────────────────
        private void SaveDataIni(string path, Encoding encoding)
        {
            if (!File.Exists(path)) return;

            var rawLines = File.ReadAllLines(path, encoding).ToList();
            string header = rawLines.Count > 0 ? rawLines[0] : "";

            var newLines = new List<string> { header };
            foreach (var kv in db.Rows.OrderBy(x => x.Key, Comparer<string>.Create(CompareIds)))
                newLines.Add(string.Join("|", NormalizeRow(kv.Value, db.Schema.Columns)));

            using var sw = new StreamWriter(path, false, encoding);
            foreach (var line in newLines)
                sw.WriteLine(line);
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static string GetValue(List<string> row, int index)
        {
            if (row == null || index < 0 || index >= row.Count) return "";
            return row[index]?.Trim() ?? "";
        }

        private static void Set(List<string> row, int index, string value)
        {
            if (index >= 0 && index < row.Count)
                row[index] = value ?? "";
        }

        private static string GetBoxText(TextBox box) => box?.Text?.Trim() ?? "";

        private static void SetText(TextBox box, string value)
        {
            if (box != null) box.Text = value ?? "";
        }

        private static List<string> NormalizeRow(List<string> row, int cols)
        {
            var r = new List<string>(row);
            while (r.Count < cols) r.Add("");
            return r.Take(cols).ToList();
        }

        private static int CompareIds(string a, string b)
        {
            if (int.TryParse(a, out var ai) && int.TryParse(b, out var bi))
                return ai.CompareTo(bi);
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }

    }

