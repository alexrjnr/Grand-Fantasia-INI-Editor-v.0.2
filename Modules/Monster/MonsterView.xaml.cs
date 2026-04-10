using GrandFantasiaINIEditor.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace GrandFantasiaINIEditor.Modules.Monster
{
    public partial class MonsterView : UserControl
    {
        private readonly string clientPath;
        private readonly string schemasPath;
        private GenericIniDb db;

        private readonly ObservableCollection<MonsterEntry> Entries = new();
        private readonly ObservableCollection<MonsterColumnItem> AllColumnItems = new();
        private readonly ObservableCollection<MonsterColumnItem> SpellsColumnItems = new();
        private readonly ObservableCollection<MonsterColumnItem> OtherColumnItems = new();
        private readonly ObservableCollection<MonsterColumnItem> LimitsColumnItems = new();
        private ICollectionView entriesView;
        private List<string> _columnNames = new();

        private readonly Dictionary<string, string> _translatedMonsterNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _monsterIcons = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, BitmapSource> _iconCache = new(StringComparer.OrdinalIgnoreCase);

        private bool _loading;
        private MonsterEntry _selected;

        private sealed class ComboOption
        {
            public int Value { get; set; }
            public string Label { get; set; }
        }

        private sealed class MonsterEntry : INotifyPropertyChanged
        {
            private BitmapSource _icon;
            private string _name;
            private string _translatedName;
            public string Id { get; set; }
            public string Name
            {
                get => _name;
                set
                {
                    if (_name == value)
                        return;
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                    OnPropertyChanged(nameof(DisplayName));
                    OnPropertyChanged(nameof(CompactDisplayName));
                }
            }

            public string TranslatedName
            {
                get => _translatedName;
                set
                {
                    if (_translatedName == value)
                        return;
                    _translatedName = value;
                    OnPropertyChanged(nameof(TranslatedName));
                    OnPropertyChanged(nameof(DisplayName));
                    OnPropertyChanged(nameof(CompactDisplayName));
                }
            }
            public List<string> Row { get; set; } = new();

            public string DisplayName => string.IsNullOrWhiteSpace(TranslatedName) ? Name : TranslatedName;
            public string CompactDisplayName => CollapseLineBreaks(DisplayName);

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
            private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

            private static string CollapseLineBreaks(string value)
            {
                if (string.IsNullOrEmpty(value))
                    return string.Empty;

                return value.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Trim();
            }
        }

        private sealed class MonsterColumnItem : INotifyPropertyChanged
        {
            private string _value;
            public int Index { get; set; }
            public string Name { get; set; }

            public string Value
            {
                get => _value;
                set
                {
                    if (_value == value)
                        return;
                    _value = value;
                    OnPropertyChanged();
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            private void OnPropertyChanged([CallerMemberName] string propertyName = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static readonly Dictionary<int, string> MONSTER_ALIGN = new()
        {
            { 1, "Normal" },
            { 2, "Savage" },
            { 3, "Npc" },
            { 4, "PlayerRed" },
            { 5, "PlayerBlue" },
            { 6, "Friendly" },
        };

        private static readonly Dictionary<int, string> MONSTER_RANK = new()
        {
            { 1, "Monster" },
            { 2, "Elite" },
            { 3, "Boss" },
            { 4, "SceneObj" },
            { 5, "InstanceElite" },
            { 6, "FieldBoss" },
            { 7, "Attacker" },
            { 8, "Defender" },
            { 9, "TerriroryCrysta" },
            { 10, "PBBoss" },
            { 11, "DynamicBlock" },
            { 100, "AltarStart" },
            { 101, "AltarA" },
            { 102, "AltarB" },
            { 103, "AltarC" },
            { 104, "AltarD" },
            { 105, "AltarE" },
            { 106, "AltarF" },
            { 107, "AltarG" },
            { 108, "AltarEnd" },
        };

        private static readonly Dictionary<int, string> MONSTER_TYPE = new()
        {
            { 1, "Animal" },
            { 2, "Plant" },
            { 4, "Human" },
            { 7, "Size" },
            { 8, "Element" },
            { 16, "Machine" },
            { 32, "Undead" },
            { 64, "Demon" },
        };

        private static readonly Dictionary<int, string> E_SUMMON_TYPE = new()
        {
            { 1, "Idle" },
            { 2, "Battle" },
            { 3, "Dead" },
        };

        private static readonly Dictionary<int, string> ITEM_ATTRIBUTE = new()
        {
            {0, "None" },
            {1, "Luz" },
            {2, "Escuridao" },
            {3, "Relampago" },
            {4, "Fogo" },
            {5, "Gelo" },
            {6, "Natureza" },
        };

        private const int IDX_ID = 0;
        private const int IDX_MODEL_IDS = 1;
        private const int IDX_NAME = 2;
        private const int IDX_LEVEL = 3;
        private const int IDX_RANK = 4;
        private const int IDX_ZONE_ICON = 5;
        private const int IDX_TYPE = 6;
        private const int IDX_SPECIAL_FLAG = 7;
        private const int IDX_MAX_HP = 8;
        private const int IDX_MAX_MP = 9;
        private const int IDX_FEAR_TYPE = 10;
        private const int IDX_PART_HP = 11;
        private const int IDX_PART_BREAKING_ACTION = 12;
        private const int IDX_AVG_PHYSICO_DAMAGE = 13;
        private const int IDX_RAND_PHYSICO_DAMAGE = 14;
        private const int IDX_ATTACK_RANGE = 15;
        private const int IDX_ATTACK_SPEED = 16;
        private const int IDX_ATTACK = 17;
        private const int IDX_PHYSICO_DEFENCE = 18;
        private const int IDX_MAGIC_DAMAGE = 19;
        private const int IDX_MAGIC_DEFENCE = 20;
        private const int IDX_HIT_RATE = 21;
        private const int IDX_DODGE_RATE = 22;
        private const int IDX_PHYSICO_CRITICAL_RATE = 23;
        private const int IDX_PHYSICO_CRITICAL_DAMAGE = 24;
        private const int IDX_MAGIC_CRITICAL_RATE = 25;
        private const int IDX_MAGIC_CRITICAL_DAMAGE = 26;
        private const int IDX_PHYSICAL_PENETRATION = 27;
        private const int IDX_MAGICAL_PENETRATION = 28;
        private const int IDX_PHYSICAL_PENETRATION_DEFENCE = 29;
        private const int IDX_MAGICAL_PENETRATION_DEFENCE = 30;
        private const int IDX_ATTRIBUTE = 31;
        private const int IDX_ATTRIBUTE_DAMAGE = 32;
        private const int IDX_ATTRIBUTE_RATE = 33;
        private const int IDX_ATTRIBUTE_RESIST = 34;
        private const int IDX_INNATE_BUFF1 = 35;
        private const int IDX_CASTING_EFFECT_ID = 38;
        private const int IDX_ROAM_SPEED = 39;
        private const int IDX_MAX_CALL_HELP = 44;
        private const int IDX_MONSTER_ALIGNMENT = 45;
        private const int IDX_IDLE_SPELL_ID = 46;
        private const int IDX_SUMMON_MONSTER_ID = 51;
        private const int IDX_SUMMON_TYPE = 52;
        private const int IDX_EXP = 56;
        private const int IDX_SHOUT_ID1 = 57;
        private const int IDX_LOCATE_LIMIT = 66;
        private const int IDX_AUTO_SPELL_DURING = 67;
        private const int IDX_SPELL_SHOUT_CMDS = 71;
        private const int IDX_LOWER_LIMIT1 = 72;
        private const int IDX_RESERVED_PADDING2 = 85;

        private static readonly string[] MONSTER_COLUMN_NAMES =
        {
            "Id","ModelIds","Name","Level","Rank","ZoneIcon","Type","SpecialFlag","MaxHp","MaxMp","FearType","PartHP","PartBreakingAction",
            "AvgPhysicoDamage","RandPhysicoDamage","AttackRange","AttackSpeed","Attack","PhysicoDefence","MagicDamage","MagicDefence","HitRate","DodgeRate",
            "PhysicoCriticalRate","PhysicoCriticalDamage","MagicCriticalRate","MagicCriticalDamage","PhysicalPenetration","MagicalPenetration",
            "PhysicalPenetrationDefence","MagicalPenetrationDefence","Attribute","AttributeDamage","AttributeRate","AttributeResist","InnateBuff1","InnateBuff2",
            "InnateBuff3","CastingEffectId","RoamSpeed","PursuitSpeed","MoveRange","DetectRange","AiId","MaxCallHelp","MonsterAlignment","IdleSpellId",
            "BattleSpell1","BattleSpell2","BattleSpell3","RestoreSpellId","SummonMonsterId","SummonType","SummonRate","SummonMax","SummonEffectId","Exp",
            "ShoutId1","ShoutId2","ShoutRate","ShoutForManId1","ShoutForManId2","ShoutForManRate","ShoutForWomanId1","ShoutForWomanId2","ShoutForWomanRate",
            "LocateLimit","AutoSpellDuring","AutoSpellID","RandBuffNum","RandomBuffs","SpellShoutCmds","LowerLimit1","LowerLimit2","LowerLimit3","LowerLimit4",
            "LowerLimit5","LowerLimit6","LowerLimit7","LowerLimit8","LowerLimit9","LowerLimit10","LowerLimit11","LowerLimit12","ReservedPadding1","ReservedPadding2"
        };

        public MonsterView(string clientPath, string schemasPath)
        {
            InitializeComponent();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            this.clientPath = clientPath;
            this.schemasPath = schemasPath;

            MonsterList.ItemsSource = Entries;
            AllColumnsList.ItemsSource = AllColumnItems;
            SpellsColumnsList.ItemsSource = SpellsColumnItems;
            OtherColumnsList.ItemsSource = OtherColumnItems;
            LimitsColumnsList.ItemsSource = LimitsColumnItems;
            entriesView = CollectionViewSource.GetDefaultView(Entries);
            entriesView.Filter = FilterMonster;

            PopulateCombos();
            LoadReferences();
            LoadMonsters();
        }

        private void PopulateCombos()
        {
            RankCombo.ItemsSource = MONSTER_RANK.Select(x => new ComboOption { Value = x.Key, Label = x.Value }).OrderBy(x => x.Value).ToList();
            MonsterAlignmentCombo.ItemsSource = MONSTER_ALIGN.Select(x => new ComboOption { Value = x.Key, Label = x.Value }).OrderBy(x => x.Value).ToList();
            FearTypeCombo.ItemsSource = MONSTER_ALIGN.Select(x => new ComboOption { Value = x.Key, Label = x.Value }).OrderBy(x => x.Value).ToList();
            TypeCombo.ItemsSource = MONSTER_TYPE.Select(x => new ComboOption { Value = x.Key, Label = x.Value }).OrderBy(x => x.Value).ToList();
            SummonTypeCombo.ItemsSource = E_SUMMON_TYPE.Select(x => new ComboOption { Value = x.Key, Label = x.Value }).OrderBy(x => x.Value).ToList();
            AttributeCombo.ItemsSource = ITEM_ATTRIBUTE.Select(x => new ComboOption { Value = x.Key, Label = x.Value }).OrderBy(x => x.Value).ToList();
        }

        private void LoadReferences()
        {
            LoadMonsterNames();
            LoadMonsterIcons();
        }

        private void LoadMonsterNames()
        {
            _translatedMonsterNames.Clear();

            string path = Path.Combine(clientPath, "data", "translate", "T_Monster.ini");
            if (!File.Exists(path))
                return;

            var rawLines = File.ReadAllLines(path, Encoding.GetEncoding(950));
            string currentId = null;
            var currentName = new StringBuilder();

            void FlushCurrent()
            {
                if (string.IsNullOrWhiteSpace(currentId))
                    return;

                string finalName = SanitizeTranslationField(currentName.ToString());
                if (!_translatedMonsterNames.ContainsKey(currentId))
                    _translatedMonsterNames[currentId] = finalName;
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
                        currentName.Append(SanitizeTranslationField(parts[1]));
                }
                else if (currentId != null)
                {
                    string extra = (line ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(extra))
                        continue;

                    if (currentName.Length > 0)
                        currentName.AppendLine();

                    currentName.Append(SanitizeTranslationField(extra));
                }
            }

            FlushCurrent();
        }

        private void LoadMonsterIcons()
        {
            _monsterIcons.Clear();

            string dbPath = Path.Combine(clientPath, "data", "db");
            if (!Directory.Exists(dbPath)) dbPath = Path.Combine(clientPath, "Data", "db");

            var codeById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string sMonster = Path.Combine(dbPath, "S_Monster.ini");
            if (File.Exists(sMonster))
            {
                foreach (var line in File.ReadLines(sMonster, Encoding.GetEncoding(950)).Skip(1))
                {
                    var parts = line.Split('|');
                    if (parts.Length > 1)
                        codeById[(parts[0] ?? string.Empty).Trim()] = (parts[1] ?? string.Empty).Trim();
                }
            }

            string monsterList = Path.Combine(dbPath, "MonsterList.ini");
            if (!File.Exists(monsterList))
                return;

            foreach (var line in File.ReadLines(monsterList, Encoding.GetEncoding(950)))
            {
                if (!line.StartsWith("DB", StringComparison.OrdinalIgnoreCase))
                    continue;

                int eq = line.IndexOf('=');
                if (eq < 0)
                    continue;

                string payload = line.Substring(eq + 1);
                var parts = payload.Split(',');
                if (parts.Length < 2)
                    continue;

                string code = parts[0].Trim();
                string iconName = Path.GetFileNameWithoutExtension(parts[1].Trim());
                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(iconName))
                    continue;

                foreach (var row in codeById.Where(x => string.Equals(x.Value, code, StringComparison.OrdinalIgnoreCase)))
                    _monsterIcons[row.Key] = iconName;
            }
        }

        private void LoadMonsters()
        {
            _loading = true;
            try
            {
                db = GenericIniLoader.Load(clientPath, schemasPath, "S_Monster.ini");
                _columnNames = ParseColumnNames(db.ColumnHeader, db.Schema.Columns);
                Entries.Clear();
                AllColumnItems.Clear();

                foreach (var kv in db.Rows.OrderBy(x => ParseIntOrMax(x.Key)))
                {
                    var row = kv.Value.ToList();
                    string id = kv.Key;

                    var entry = new MonsterEntry
                    {
                        Id = id,
                        Name = GetValue(row, IDX_NAME),
                        TranslatedName = _translatedMonsterNames.TryGetValue(id, out var tname) ? tname : string.Empty,
                        Row = row,
                        Icon = LoadMonsterIcon(id)
                    };

                    Entries.Add(entry);
                }

                StatusText.Text = $"Loaded {Entries.Count} monsters.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar S_Monster.ini:\n{ex.Message}", "Monster", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _loading = false;
            }
        }

        private bool FilterMonster(object obj)
        {
            if (obj is not MonsterEntry entry)
                return false;

            string q = (SearchBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(q))
                return true;

            return entry.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (entry.Name ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase)
                || (entry.TranslatedName ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase);
        }

        private BitmapSource LoadMonsterIcon(string id)
        {
            if (!_monsterIcons.TryGetValue(id, out var icon) || string.IsNullOrWhiteSpace(icon))
                return null;

            string cacheKey = "uiicon_" + icon;
            if (_iconCache.TryGetValue(cacheKey, out var cached))
                return cached;

            string[] paths =
            {
                Path.Combine(clientPath, "UI", "uiicon", icon + ".dds"),
                Path.Combine(clientPath, "ui", "uiicon", icon + ".dds"),
                Path.Combine(clientPath, "Data", "UI", "uiicon", icon + ".dds")
            };

            foreach (var p in paths)
            {
                if (!File.Exists(p))
                    continue;

                var bitmap = DdsLoader.Load(p);
                if (bitmap != null)
                {
                    _iconCache[cacheKey] = bitmap;
                    return bitmap;
                }
            }

            return null;
        }

        private static int ParseIntOrMax(string value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : int.MaxValue;
        }

        private static string GetValue(List<string> row, int index)
        {
            if (row == null || index < 0 || index >= row.Count)
                return string.Empty;
            return row[index] ?? string.Empty;
        }

        private static void SetValue(List<string> row, int index, string value)
        {
            while (row.Count <= index)
                row.Add(string.Empty);
            row[index] = (value ?? string.Empty).Trim();
        }

        private void MonsterList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading)
                return;

            _selected = MonsterList.SelectedItem as MonsterEntry;
            if (_selected == null)
                return;

            _loading = true;
            try
            {
                var row = _selected.Row;
                IdBox.Text = _selected.Id;
                NameBox.Text = _selected.Name ?? string.Empty;
                TranslatedNameBox.Text = _selected.TranslatedName ?? string.Empty;
                ModelIdsBox.Text = GetValue(row, IDX_MODEL_IDS);
                LevelBox.Text = GetValue(row, IDX_LEVEL);
                ZoneIconBox.Text = GetValue(row, IDX_ZONE_ICON);
                SpecialFlagBox.Text = GetValue(row, IDX_SPECIAL_FLAG);
                MaxHpBox.Text = GetValue(row, IDX_MAX_HP);
                MaxMpBox.Text = GetValue(row, IDX_MAX_MP);
                MaxHpQuickBox.Text = MaxHpBox.Text;
                MaxMpQuickBox.Text = MaxMpBox.Text;
                PartHpBox.Text = GetValue(row, IDX_PART_HP);
                PartBreakingActionBox.Text = GetValue(row, IDX_PART_BREAKING_ACTION);
                AvgPhysicoDamageBox.Text = GetValue(row, IDX_AVG_PHYSICO_DAMAGE);
                RandPhysicoDamageBox.Text = GetValue(row, IDX_RAND_PHYSICO_DAMAGE);
                AttackRangeBox.Text = GetValue(row, IDX_ATTACK_RANGE);
                AttackSpeedBox.Text = GetValue(row, IDX_ATTACK_SPEED);
                AttackBox.Text = GetValue(row, IDX_ATTACK);
                PhysicoDefenceBox.Text = GetValue(row, IDX_PHYSICO_DEFENCE);
                MagicDamageBox.Text = GetValue(row, IDX_MAGIC_DAMAGE);
                MagicDefenceBox.Text = GetValue(row, IDX_MAGIC_DEFENCE);
                HitRateBox.Text = GetValue(row, IDX_HIT_RATE);
                DodgeRateBox.Text = GetValue(row, IDX_DODGE_RATE);
                PhysicoCriticalRateBox.Text = GetValue(row, IDX_PHYSICO_CRITICAL_RATE);
                PhysicoCriticalDamageBox.Text = GetValue(row, IDX_PHYSICO_CRITICAL_DAMAGE);
                MagicCriticalRateBox.Text = GetValue(row, IDX_MAGIC_CRITICAL_RATE);
                MagicCriticalDamageBox.Text = GetValue(row, IDX_MAGIC_CRITICAL_DAMAGE);
                PhysicalPenetrationBox.Text = GetValue(row, IDX_PHYSICAL_PENETRATION);
                MagicalPenetrationBox.Text = GetValue(row, IDX_MAGICAL_PENETRATION);
                PhysicalPenetrationDefenceBox.Text = GetValue(row, IDX_PHYSICAL_PENETRATION_DEFENCE);
                MagicalPenetrationDefenceBox.Text = GetValue(row, IDX_MAGICAL_PENETRATION_DEFENCE);
                AttributeDamageBox.Text = GetValue(row, IDX_ATTRIBUTE_DAMAGE);
                AttributeRateBox.Text = GetValue(row, IDX_ATTRIBUTE_RATE);
                AttributeResistBox.Text = GetValue(row, IDX_ATTRIBUTE_RESIST);

                SelectByValue(RankCombo, GetValue(row, IDX_RANK));
                SelectByValue(MonsterAlignmentCombo, GetValue(row, IDX_MONSTER_ALIGNMENT));
                SelectByValue(FearTypeCombo, GetValue(row, IDX_FEAR_TYPE));
                SelectByValue(TypeCombo, GetValue(row, IDX_TYPE));
                SelectByValue(SummonTypeCombo, GetValue(row, IDX_SUMMON_TYPE));
                SelectByValue(AttributeCombo, GetValue(row, IDX_ATTRIBUTE));

                MonsterIcon.Source = _selected.Icon;
                LoadAllColumnsFromRow(row);
            }
            finally
            {
                _loading = false;
            }
        }

        private static void SelectByValue(ComboBox combo, string raw)
        {
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int target))
            {
                combo.SelectedIndex = -1;
                return;
            }

            foreach (var item in combo.Items)
            {
                if (item is ComboOption opt && opt.Value == target)
                {
                    combo.SelectedItem = opt;
                    return;
                }
            }

            combo.SelectedIndex = -1;
        }

        private static string GetComboValueOrText(ComboBox combo, TextBox fallback)
        {
            if (combo.SelectedItem is ComboOption option)
                return option.Value.ToString(CultureInfo.InvariantCulture);

            return (fallback?.Text ?? string.Empty).Trim();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null || db == null)
                return;

            try
            {
                WriteCurrentToSelected();
                WriteMonsterFile();
                StatusText.Text = "S_Monster.ini salvo com sucesso.";
                MessageBox.Show("S_Monster.ini salvo com sucesso.", "Monster", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao salvar:\n{ex.Message}", "Monster", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WriteCurrentToSelected()
        {
            if (_selected == null)
                return;

            var row = _selected.Row;
            SetValue(row, IDX_MODEL_IDS, ModelIdsBox.Text);
            SetValue(row, IDX_NAME, NameBox.Text);
            SetValue(row, IDX_LEVEL, LevelBox.Text);
            SetValue(row, IDX_RANK, GetComboValueOrText(RankCombo, null));
            SetValue(row, IDX_ZONE_ICON, ZoneIconBox.Text);
            SetValue(row, IDX_TYPE, GetComboValueOrText(TypeCombo, null));
            SetValue(row, IDX_SPECIAL_FLAG, SpecialFlagBox.Text);
            SetValue(row, IDX_MAX_HP, MaxHpBox.Text);
            SetValue(row, IDX_MAX_MP, MaxMpBox.Text);
            SetValue(row, IDX_FEAR_TYPE, GetComboValueOrText(FearTypeCombo, null));
            SetValue(row, IDX_PART_HP, PartHpBox.Text);
            SetValue(row, IDX_PART_BREAKING_ACTION, PartBreakingActionBox.Text);
            SetValue(row, IDX_SUMMON_TYPE, GetComboValueOrText(SummonTypeCombo, null));
            SetValue(row, IDX_MONSTER_ALIGNMENT, GetComboValueOrText(MonsterAlignmentCombo, null));
            SetValue(row, IDX_AVG_PHYSICO_DAMAGE, AvgPhysicoDamageBox.Text);
            SetValue(row, IDX_RAND_PHYSICO_DAMAGE, RandPhysicoDamageBox.Text);
            SetValue(row, IDX_ATTACK_RANGE, AttackRangeBox.Text);
            SetValue(row, IDX_ATTACK_SPEED, AttackSpeedBox.Text);
            SetValue(row, IDX_ATTACK, AttackBox.Text);
            SetValue(row, IDX_PHYSICO_DEFENCE, PhysicoDefenceBox.Text);
            SetValue(row, IDX_MAGIC_DAMAGE, MagicDamageBox.Text);
            SetValue(row, IDX_MAGIC_DEFENCE, MagicDefenceBox.Text);
            SetValue(row, IDX_HIT_RATE, HitRateBox.Text);
            SetValue(row, IDX_DODGE_RATE, DodgeRateBox.Text);
            SetValue(row, IDX_PHYSICO_CRITICAL_RATE, PhysicoCriticalRateBox.Text);
            SetValue(row, IDX_PHYSICO_CRITICAL_DAMAGE, PhysicoCriticalDamageBox.Text);
            SetValue(row, IDX_MAGIC_CRITICAL_RATE, MagicCriticalRateBox.Text);
            SetValue(row, IDX_MAGIC_CRITICAL_DAMAGE, MagicCriticalDamageBox.Text);
            SetValue(row, IDX_PHYSICAL_PENETRATION, PhysicalPenetrationBox.Text);
            SetValue(row, IDX_MAGICAL_PENETRATION, MagicalPenetrationBox.Text);
            SetValue(row, IDX_PHYSICAL_PENETRATION_DEFENCE, PhysicalPenetrationDefenceBox.Text);
            SetValue(row, IDX_MAGICAL_PENETRATION_DEFENCE, MagicalPenetrationDefenceBox.Text);
            SetValue(row, IDX_ATTRIBUTE, GetComboValueOrText(AttributeCombo, null));
            SetValue(row, IDX_ATTRIBUTE_DAMAGE, AttributeDamageBox.Text);
            SetValue(row, IDX_ATTRIBUTE_RATE, AttributeRateBox.Text);
            SetValue(row, IDX_ATTRIBUTE_RESIST, AttributeResistBox.Text);

            SyncKnownEditorsToAllColumns();
            ApplyAllColumnsToRow(row);

            _selected.Name = NameBox.Text?.Trim() ?? string.Empty;
            _selected.TranslatedName = TranslatedNameBox.Text?.Trim() ?? string.Empty;
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

        private static List<string> ParseColumnNames(string header, int expectedCount)
        {
            var names = MONSTER_COLUMN_NAMES.ToList();

            if (names.Count < expectedCount)
            {
                for (int i = names.Count; i < expectedCount; i++)
                    names.Add($"Column{i}");
            }

            if (names.Count > expectedCount)
                names = names.Take(expectedCount).ToList();

            return names;
        }

        private void LoadAllColumnsFromRow(List<string> row)
        {
            AllColumnItems.Clear();

            int total = Math.Max(db?.Schema?.Columns ?? 0, row?.Count ?? 0);
            if (total <= 0)
                return;

            for (int i = 0; i < total; i++)
            {
                string name = i < _columnNames.Count ? _columnNames[i] : $"Column{i}";
                AllColumnItems.Add(new MonsterColumnItem
                {
                    Index = i,
                    Name = name,
                    Value = GetValue(row, i)
                });
            }

            BuildGroupedColumns();
        }

        private void BuildGroupedColumns()
        {
            FillGroupByIndices(SpellsColumnItems, new[]
            {
                IDX_TYPE, IDX_SPECIAL_FLAG, IDX_EXP,
                35, 36, 37,
                46, 47, 48, 49, 50, 51, 52, 53, 54, 55,
                57, 58, 59, 60, 61, 62, 63, 64, 65,
                67, 68, 69, 70, 71
            });

            FillGroupByIndices(OtherColumnItems, new[]
            {
                IDX_CASTING_EFFECT_ID, IDX_ROAM_SPEED, 40, 41, 42, 43, IDX_MAX_CALL_HELP
            });

            FillGroupByIndices(LimitsColumnItems, new[]
            {
                IDX_MONSTER_ALIGNMENT, IDX_LOCATE_LIMIT,
                72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83,
                84, 85
            });
        }

        private void FillGroupByIndices(ObservableCollection<MonsterColumnItem> target, IEnumerable<int> indexes)
        {
            target.Clear();

            foreach (var idx in indexes)
            {
                var item = AllColumnItems.FirstOrDefault(x => x.Index == idx);
                if (item != null)
                    target.Add(item);
            }
        }

        private void ApplyAllColumnsToRow(List<string> row)
        {
            foreach (var col in AllColumnItems)
                SetValue(row, col.Index, col.Value ?? string.Empty);
        }

        private void SetColumnItemValue(int index, string value)
        {
            var item = AllColumnItems.FirstOrDefault(x => x.Index == index);
            if (item != null)
                item.Value = value ?? string.Empty;
        }

        private void SyncKnownEditorsToAllColumns()
        {
            SetColumnItemValue(IDX_MODEL_IDS, ModelIdsBox.Text);
            SetColumnItemValue(IDX_NAME, NameBox.Text);
            SetColumnItemValue(IDX_LEVEL, LevelBox.Text);
            SetColumnItemValue(IDX_RANK, GetComboValueOrText(RankCombo, null));
            SetColumnItemValue(IDX_ZONE_ICON, ZoneIconBox.Text);
            SetColumnItemValue(IDX_TYPE, GetComboValueOrText(TypeCombo, null));
            SetColumnItemValue(IDX_SPECIAL_FLAG, SpecialFlagBox.Text);
            SetColumnItemValue(IDX_MAX_HP, MaxHpBox.Text);
            SetColumnItemValue(IDX_MAX_MP, MaxMpBox.Text);
            SetColumnItemValue(IDX_FEAR_TYPE, GetComboValueOrText(FearTypeCombo, null));
            SetColumnItemValue(IDX_PART_HP, PartHpBox.Text);
            SetColumnItemValue(IDX_PART_BREAKING_ACTION, PartBreakingActionBox.Text);
            SetColumnItemValue(IDX_MONSTER_ALIGNMENT, GetComboValueOrText(MonsterAlignmentCombo, null));
            SetColumnItemValue(IDX_SUMMON_TYPE, GetComboValueOrText(SummonTypeCombo, null));
            SetColumnItemValue(IDX_AVG_PHYSICO_DAMAGE, AvgPhysicoDamageBox.Text);
            SetColumnItemValue(IDX_RAND_PHYSICO_DAMAGE, RandPhysicoDamageBox.Text);
            SetColumnItemValue(IDX_ATTACK_RANGE, AttackRangeBox.Text);
            SetColumnItemValue(IDX_ATTACK_SPEED, AttackSpeedBox.Text);
            SetColumnItemValue(IDX_ATTACK, AttackBox.Text);
            SetColumnItemValue(IDX_PHYSICO_DEFENCE, PhysicoDefenceBox.Text);
            SetColumnItemValue(IDX_MAGIC_DAMAGE, MagicDamageBox.Text);
            SetColumnItemValue(IDX_MAGIC_DEFENCE, MagicDefenceBox.Text);
            SetColumnItemValue(IDX_HIT_RATE, HitRateBox.Text);
            SetColumnItemValue(IDX_DODGE_RATE, DodgeRateBox.Text);
            SetColumnItemValue(IDX_PHYSICO_CRITICAL_RATE, PhysicoCriticalRateBox.Text);
            SetColumnItemValue(IDX_PHYSICO_CRITICAL_DAMAGE, PhysicoCriticalDamageBox.Text);
            SetColumnItemValue(IDX_MAGIC_CRITICAL_RATE, MagicCriticalRateBox.Text);
            SetColumnItemValue(IDX_MAGIC_CRITICAL_DAMAGE, MagicCriticalDamageBox.Text);
            SetColumnItemValue(IDX_PHYSICAL_PENETRATION, PhysicalPenetrationBox.Text);
            SetColumnItemValue(IDX_MAGICAL_PENETRATION, MagicalPenetrationBox.Text);
            SetColumnItemValue(IDX_PHYSICAL_PENETRATION_DEFENCE, PhysicalPenetrationDefenceBox.Text);
            SetColumnItemValue(IDX_MAGICAL_PENETRATION_DEFENCE, MagicalPenetrationDefenceBox.Text);
            SetColumnItemValue(IDX_ATTRIBUTE, GetComboValueOrText(AttributeCombo, null));
            SetColumnItemValue(IDX_ATTRIBUTE_DAMAGE, AttributeDamageBox.Text);
            SetColumnItemValue(IDX_ATTRIBUTE_RATE, AttributeRateBox.Text);
            SetColumnItemValue(IDX_ATTRIBUTE_RESIST, AttributeResistBox.Text);
        }

        private void WriteMonsterFile()
        {
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(db.VersionLine))
                lines.Add(db.VersionLine);

            lines.Add(string.IsNullOrWhiteSpace(db.ColumnHeader)
                ? string.Join("|", MONSTER_COLUMN_NAMES) + "|"
                : db.ColumnHeader);

            foreach (var entry in Entries.OrderBy(x => ParseIntOrMax(x.Id)))
            {
                var row = entry.Row.ToList();

                while (row.Count < db.Schema.Columns)
                    row.Add(string.Empty);

                if (row.Count > db.Schema.Columns)
                    row = row.Take(db.Schema.Columns).ToList();

                lines.Add(string.Join("|", row) + "|");
                db.Rows[entry.Id] = row;
            }

            File.WriteAllLines(db.FilePath, lines, Encoding.GetEncoding(950));
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            LoadReferences();
            LoadMonsters();
            entriesView.Refresh();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            entriesView.Refresh();
        }
    }
}
