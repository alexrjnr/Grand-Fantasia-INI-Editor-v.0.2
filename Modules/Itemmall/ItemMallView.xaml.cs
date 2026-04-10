using GrandFantasiaINIEditor.Core;
using Pfim;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace GrandFantasiaINIEditor.Modules.ItemMall
{
    public partial class ItemMallView : UserControl
    {
        private readonly string clientPath;
        private readonly string schemasPath;
        private GenericIniDb db;
        private readonly ObservableCollection<ItemEntry> Items = new();
        private readonly Dictionary<string, BitmapSource> iconCache = new();
        private CancellationTokenSource searchCts;

        private readonly List<(ulong value, CheckBox cb)> _opflagsChecks = new();
        private readonly List<(ulong value, CheckBox cb)> _opflagsPlusChecks = new();
        private readonly List<(ulong value, CheckBox cb)> _classChecks = new();

        private bool _loading;
        private bool _suppressSelectionChanged;
        private List<string> _currentRow;
        private string _lastSelectedItemId;
        private List<string> _originalRowSnapshot;
        private string _originalNameSnapshot;
        private string _originalDescSnapshot;


        private const int IDX_RESTRICT_GENDER = 15;
        private const int IDX_REBIRTH_SCORE = 19;
        private const int IDX_RESTRICT_ALIGN = 21;
        private const int IDX_RESTRICT_PRESTIGE = 22;
        private const int IDX_ENCHANT_TIME_TYPE = 73;
        private const int IDX_ENCHANT_DURATION = 74;
        private const int IDX_BACKPACK_SIZE = 77;   
        private const int IDX_MAX_DURABILITY = 80;
        private const int IDX_RESTRICT_EVENT_POS_ID = 84;
        private const int IDX_ID = 0;
        private const int IDX_ICON = 1;
        private const int IDX_MODEL_ID = 2;
        private const int IDX_MODEL_FILE = 3;
        private const int IDX_WEAPON_EFFECT_ID = 4;
        private const int IDX_FLY_EFFECT_ID = 5;
        private const int IDX_USED_EFFECT_ID = 6;
        private const int IDX_USED_SOUND_NAME = 7;
        private const int IDX_ENHANCE_EFFECT_ID = 8;

        private const int IDX_ITEM_TYPE = 10;
        private const int IDX_EQUIP_TYPE = 11;
        private const int IDX_OP_FLAGS = 12;
        private const int IDX_OP_FLAGS_PLUS = 13;
        private const int IDX_TARGET = 14;

        private const int IDX_RESTRICT_LEVEL = 16;
        private const int IDX_RESTRICT_MAX_LEVEL = 17;
        private const int IDX_REBIRTH_COUNT = 18;
        private const int IDX_REBIRTH_MAX_SCORE = 20;
        private const int IDX_RESTRICT_CLASS = 23;
        private const int IDX_ITEM_QUALITY = 24;
        private const int IDX_ITEM_GROUP = 25;
        private const int IDX_CASTING_TIME = 26;
        private const int IDX_COOLDOWN_TIME = 27;
        private const int IDX_COOLDOWN_GROUP = 28;

        private const int IDX_MAX_HP = 29;
        private const int IDX_MAX_MP = 30;
        private const int IDX_STR = 31;
        private const int IDX_CON = 32;
        private const int IDX_INT = 33;
        private const int IDX_VOL = 34;
        private const int IDX_DEX = 35;
        private const int IDX_AVG_PHYSICO_DAMAGE = 36;
        private const int IDX_RAND_PHYSICO_DAMAGE = 37;
        private const int IDX_ATTACK_RANGE = 38;
        private const int IDX_ATTACK_SPEED = 39;
        private const int IDX_ATTACK = 40;
        private const int IDX_RANGE_ATTACK = 41;
        private const int IDX_PHYSICO_DEFENCE = 42;
        private const int IDX_MAGIC_DAMAGE = 43;
        private const int IDX_MAGIC_DEFENCE = 44;
        private const int IDX_HIT_RATE = 45;
        private const int IDX_DODGE_RATE = 46;
        private const int IDX_PHYSICO_CRITICAL_RATE = 47;
        private const int IDX_PHYSICO_CRITICAL_DAMAGE = 48;
        private const int IDX_MAGIC_CRITICAL_RATE = 49;
        private const int IDX_MAGIC_CRITICAL_DAMAGE = 50;
        private const int IDX_PHYSICAL_PENETRATION = 51;
        private const int IDX_MAGICAL_PENETRATION = 52;
        private const int IDX_PHYSICAL_PENETRATION_DEFENCE = 53;
        private const int IDX_MAGICAL_PENETRATION_DEFENCE = 54;

        private const int IDX_ATTRIBUTE = 55;
        private const int IDX_ATTRIBUTE_RATE = 56;
        private const int IDX_ATTRIBUTE_DAMAGE = 57;
        private const int IDX_ATTRIBUTE_RESIST = 58;
        private const int IDX_SPECIAL_TYPE = 59;
        private const int IDX_SPECIAL_RATE = 60;
        private const int IDX_SPECIAL_DAMAGE = 61;

        private const int IDX_DROP_RATE = 62;
        private const int IDX_DROP_INDEX = 63;
        private const int IDX_TREASURE_BUFF_1 = 64;
        private const int IDX_TREASURE_BUFF_2 = 65;
        private const int IDX_TREASURE_BUFF_3 = 66;
        private const int IDX_TREASURE_BUFF_4 = 67;

        private const int IDX_ENCHANT_TYPE = 68;
        private const int IDX_ENCHANT_ID = 69;
        private const int IDX_EXPERT_LEVEL = 70;
        private const int IDX_EXPERT_ENCHANT_ID = 71;
        private const int IDX_ELF_SKILL_ID = 72;
        private const int IDX_LIMIT_TYPE = 75;
        private const int IDX_DUE_DATE_TIME = 76;

        private const int IDX_MAX_SOCKET = 78;
        private const int IDX_SOCKET_RATE = 79;
        private const int IDX_MAX_STACK = 81;
        private const int IDX_SHOP_PRICE_TYPE = 82;
        private const int IDX_SYS_PRICE = 83;

        private const int IDX_MISSION_POS_ID = 85;
        private const int IDX_BLOCK_RATE = 86;
        private const int IDX_LOG_LEVEL = 87;
        private const int IDX_AUCTION_TYPE = 88;
        private const int IDX_EXTRA_DATA_1 = 89;
        private const int IDX_EXTRA_DATA_2 = 90;
        private const int IDX_EXTRA_DATA_3 = 91;

        public ItemMallView(string clientPath, string schemasPath)
        {
            InitializeComponent();

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            this.clientPath = clientPath;
            this.schemasPath = schemasPath;
            ItemList.ItemsSource = Items;

            PopulateCombos();
            InitFlagsUi();
            HookFlagEvents();
            LoadDatabase();
        }

        private sealed class ItemEntry
        {
            public string Id { get; set; }
            public string Name { get; set; }

            public override string ToString() => $"{Id} - {Name}";
        }

        private sealed class ComboOption
        {
            public int Value { get; set; }
            public string Label { get; set; }

            public override string ToString() => $"{Label} ({Value})";
        }

        private sealed class FlagDef
        {
            public string Label { get; set; }
            public ulong Value { get; set; }
        }

        private static string GetBoxText(TextBox box)
        {
            return box?.Text?.Trim() ?? string.Empty;
        }

        private static int CompareItemIds(string a, string b)
        {
            if (int.TryParse(a, out var ai) && int.TryParse(b, out var bi))
                return ai.CompareTo(bi);

            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private void PopulateCombos()
        {
            if (RestrictGenderCombo != null)
            {
                RestrictGenderCombo.ItemsSource = RESTRICT_GENDER
                    .Select(kv => new ComboOption { Value = kv.Key, Label = kv.Value })
                    .OrderBy(x => x.Value)
                    .ToList();
            }

            if (RestrictAlignCombo != null)
            {
                RestrictAlignCombo.ItemsSource = RESTRICT_ALIGN
                    .Select(kv => new ComboOption { Value = kv.Key, Label = kv.Value })
                    .OrderBy(x => x.Value)
                    .ToList();
            }

            if (ItemTypeCombo != null)
            {
                ItemTypeCombo.ItemsSource = ITEM_TYPE
                    .Select(kv => new ComboOption { Value = kv.Key, Label = kv.Value })
                    .OrderBy(x => x.Value)
                    .ToList();
            }

            if (QualityCombo != null)
            {
                QualityCombo.ItemsSource = ITEM_QUALITY
                    .Select(kv => new ComboOption { Value = kv.Key, Label = kv.Value })
                    .OrderBy(x => x.Value)
                    .ToList();
            }

            if (EquipTypeCombo != null)
            {
                EquipTypeCombo.ItemsSource = EQUIP_TYPE
                    .Select(kv => new ComboOption { Value = kv.Key, Label = kv.Value })
                    .OrderBy(x => x.Value)
                    .ToList();
            }

            if (TargetCombo != null)
            {
                TargetCombo.ItemsSource = ITEM_TARGET
                    .Select(kv => new ComboOption { Value = kv.Key, Label = kv.Value })
                    .OrderBy(x => x.Value)
                    .ToList();
            }

            if (ShopPriceTypeCombo != null)
            {
                ShopPriceTypeCombo.ItemsSource = SHOP_PRICE_TYPE
                    .Select(kv => new ComboOption { Value = kv.Key, Label = kv.Value })
                    .OrderBy(x => x.Value)
                    .ToList();
            }

            if (AttributeBox != null)
            {
                AttributeBox.ItemsSource = ITEM_ATTRIBUTE
                    .Select(kv => new ComboOption { Value = kv.Key, Label = kv.Value })
                    .OrderBy(x => x.Value)
                    .ToList();
            }
        }

        private void InitFlagsUi()
        {
            _opflagsChecks.Clear();
            _opflagsPlusChecks.Clear();
            _classChecks.Clear();

            if (op_grid != null)
            {
                op_grid.Children.Clear();

                foreach (var def in OP_FLAGS)
                {
                    var cb = new CheckBox
                    {
                        Content = $"{def.Label} ({def.Value})",
                        Foreground = Brushes.AliceBlue,
                        Margin = new Thickness(0, 0, 14, 10)
                    };

                    _opflagsChecks.Add((def.Value, cb));
                    op_grid.Children.Add(cb);
                }
            }

            if (opplus_grid != null)
            {
                opplus_grid.Children.Clear();

                foreach (var def in OP_FLAGS_PLUS)
                {
                    var cb = new CheckBox
                    {
                        Content = $"{def.Label} ({def.Value})",
                        Foreground = Brushes.AliceBlue,
                        Margin = new Thickness(0, 0, 14, 10)
                    };

                    _opflagsPlusChecks.Add((def.Value, cb));
                    opplus_grid.Children.Add(cb);
                }
            }

            if (class_grid != null)
            {
                class_grid.Children.Clear();

                foreach (var def in RESTRICT_CLASSES)
                {
                    var cb = new CheckBox
                    {
                        Content = $"{def.Label} (0x{def.Value:X})",
                        Foreground = Brushes.AliceBlue,
                        Margin = new Thickness(0, 0, 14, 10)
                    };

                    _classChecks.Add((def.Value, cb));
                    class_grid.Children.Add(cb);
                }
            }
        }

        private void HookFlagEvents()
        {
            foreach (var item in _opflagsChecks)
            {
                item.cb.Checked += FlagCheckChanged;
                item.cb.Unchecked += FlagCheckChanged;
            }

            foreach (var item in _opflagsPlusChecks)
            {
                item.cb.Checked += FlagPlusCheckChanged;
                item.cb.Unchecked += FlagPlusCheckChanged;
            }

            foreach (var item in _classChecks)
            {
                item.cb.Checked += ClassCheckChanged;
                item.cb.Unchecked += ClassCheckChanged;
            }
        }

        private void FlagCheckChanged(object sender, RoutedEventArgs e)
        {
            RecomputeOpFlags();
        }

        private void FlagPlusCheckChanged(object sender, RoutedEventArgs e)
        {
            RecomputeOpFlagsPlus();
        }

        private void ClassCheckChanged(object sender, RoutedEventArgs e)
        {
            RecomputeRestrictClass();
        }

        private void RecomputeOpFlags()
        {
            if (_loading || _currentRow == null)
                return;

            ulong total = 0;

            foreach (var (value, cb) in _opflagsChecks)
            {
                if (cb.IsChecked == true)
                    total |= value;
            }

            total = PreserveUnknownBits(IDX_OP_FLAGS, total, OP_FLAGS);

            SetRowValue(IDX_OP_FLAGS, FormatBitmaskLikeOriginal(IDX_OP_FLAGS, total));

            ulong classVal = GetULongFromCurrentRow(IDX_RESTRICT_CLASS);
            RefreshFlagDisplays(total, classVal);
        }

        private void RecomputeOpFlagsPlus()
        {
            if (_loading || _currentRow == null)
                return;

            ulong total = 0;

            foreach (var (value, cb) in _opflagsPlusChecks)
            {
                if (cb.IsChecked == true)
                    total |= value;
            }

            total = PreserveUnknownBits(IDX_OP_FLAGS_PLUS, total, OP_FLAGS_PLUS);

            SetRowValue(IDX_OP_FLAGS_PLUS, FormatBitmaskLikeOriginal(IDX_OP_FLAGS_PLUS, total));

            if (opflagsplus_value != null)
                opflagsplus_value.Text = $"{total}  |  0x{total:X}";
        }

        private void RecomputeRestrictClass()
        {
            if (_loading || _currentRow == null)
                return;

            ulong total = 0;

            foreach (var (value, cb) in _classChecks)
            {
                if (cb.IsChecked == true)
                    total |= value;
            }

            total = PreserveUnknownBits(IDX_RESTRICT_CLASS, total, RESTRICT_CLASSES);

            SetRowValue(IDX_RESTRICT_CLASS, FormatBitmaskLikeOriginal(IDX_RESTRICT_CLASS, total));

            ulong opVal = GetULongFromCurrentRow(IDX_OP_FLAGS);
            RefreshFlagDisplays(opVal, total);

            SetText(ClassBox, $"{total} | 0x{total:X}");
            SetToolTip(
                ClassBox,
                $"Valor decimal: {total}\n" +
                $"Valor hexadecimal: 0x{total:X}\n" +
                $"Composição: {BuildClassDecomposition(total)}\n\n" +
                $"{BuildClassSummary(total)}");
        }

        private void SetChecksFromValue(
            List<(ulong value, CheckBox cb)> checks,
            ulong value,
            RoutedEventHandler handlerToAttach)
        {
            foreach (var (flag, cb) in checks)
            {
                cb.Checked -= FlagCheckChanged;
                cb.Unchecked -= FlagCheckChanged;
                cb.Checked -= FlagPlusCheckChanged;
                cb.Unchecked -= FlagPlusCheckChanged;
                cb.Checked -= ClassCheckChanged;
                cb.Unchecked -= ClassCheckChanged;

                cb.IsChecked = (value & flag) == flag;

                cb.Checked += handlerToAttach;
                cb.Unchecked += handlerToAttach;
            }
        }

        private void RefreshFlagDisplays(ulong opVal, ulong classVal)
        {
            if (opflags_value != null)
                opflags_value.Text = $"{opVal}  |  0x{opVal:X}";

            if (restrictclass_value != null)
                restrictclass_value.Text = $"{classVal}  |  0x{classVal:X}";
        }

        private ulong GetKnownMask(List<FlagDef> defs)
        {
            ulong mask = 0;
            foreach (var def in defs)
                mask |= def.Value;
            return mask;
        }

        private ulong PreserveUnknownBits(int idx, ulong rebuiltValue, List<FlagDef> defs)
        {
            ulong originalValue = ParseULongSafe(GetOriginalFieldText(idx));
            ulong knownMask = GetKnownMask(defs);
            ulong unknownBits = originalValue & ~knownMask;
            return rebuiltValue | unknownBits;
        }
        private ulong GetULongFromCurrentRow(int idx)
        {
            if (_currentRow == null || idx < 0 || idx >= _currentRow.Count)
                return 0;

            return ParseULongSafe(_currentRow[idx]);
        }

        private string GetOriginalFieldText(int idx)
        {
            if (_originalRowSnapshot != null && idx >= 0 && idx < _originalRowSnapshot.Count)
                return _originalRowSnapshot[idx] ?? string.Empty;

            if (_currentRow != null && idx >= 0 && idx < _currentRow.Count)
                return _currentRow[idx] ?? string.Empty;

            return string.Empty;
        }

        private static bool LooksLikeHexWithoutPrefix(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            value = value.Trim();

            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return false;

            return value.Any(c =>
                (c >= 'A' && c <= 'F') ||
                (c >= 'a' && c <= 'f'));
        }

        private string FormatBitmaskLikeOriginal(int idx, ulong value)
        {
            string original = GetOriginalFieldText(idx)?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(original))
                return value == 0 ? string.Empty : value.ToString(CultureInfo.InvariantCulture);

            if (original.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return "0x" + value.ToString("X", CultureInfo.InvariantCulture);

            if (LooksLikeHexWithoutPrefix(original))
                return value.ToString("X", CultureInfo.InvariantCulture);

            return value.ToString(CultureInfo.InvariantCulture);
        }

        private string GetComboValueOrKeepOriginal(ComboBox combo, int idx)
        {
            if (combo?.SelectedItem is ComboOption option)
                return option.Value.ToString(CultureInfo.InvariantCulture);

            return GetOriginalFieldText(idx).Trim();
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

        private void SelectComboByValue(ComboBox combo, int value)
        {
            if (combo == null)
                return;

            foreach (var item in combo.Items)
            {
                if (item is ComboOption option && option.Value == value)
                {
                    combo.SelectedItem = item;
                    return;
                }
            }

            combo.SelectedIndex = -1;
        }

        private void LoadDatabase()
        {
            db = GenericIniLoader.Load(clientPath, schemasPath, "S_ItemMall.ini", "T_ItemMall.ini");
            Items.Clear();

            foreach (var r in db.Rows.OrderBy(x => x.Key, Comparer<string>.Create(CompareItemIds)))
            {
                var name = db.Translations.TryGetValue(r.Key, out var t) ? t.Name : "Unknown";

                Items.Add(new ItemEntry
                {
                    Id = r.Key,
                    Name = name
                });
            }
        }

        private List<string> BuildEditedRowFromControls()
        {
            var row = _currentRow != null
                ? new List<string>(_currentRow)
                : new List<string>();


            SetListValue(row, IDX_RESTRICT_GENDER, GetComboValueOrKeepOriginal(RestrictGenderCombo, IDX_RESTRICT_GENDER));
            SetListValue(row, IDX_REBIRTH_SCORE, GetBoxText(RebirthScoreBox));


            SetListValue(row, IDX_RESTRICT_ALIGN, GetComboValueOrKeepOriginal(RestrictAlignCombo, IDX_RESTRICT_ALIGN));


            SetListValue(row, IDX_RESTRICT_PRESTIGE, GetBoxText(RestrictPrestigeBox));
            SetListValue(row, IDX_BACKPACK_SIZE, GetBoxText(BackpackSizeBox));
            SetListValue(row, IDX_MAX_DURABILITY, GetBoxText(MaxDurabilityBox));
            SetListValue(row, IDX_RESTRICT_EVENT_POS_ID, GetBoxText(RestrictEventPosIdBox));
            SetListValue(row, IDX_ENCHANT_TIME_TYPE, GetBoxText(EnchantTimeTypeBox));
            SetListValue(row, IDX_ENCHANT_DURATION, GetBoxText(EnchantDurationBox));

            SetListValue(row, IDX_ICON, GetBoxText(IconBox));
            SetListValue(row, IDX_MODEL_ID, GetBoxText(ModelIdBox));
            SetListValue(row, IDX_MODEL_FILE, GetBoxText(ModelFileBox));

            SetListValue(row, IDX_ITEM_TYPE, GetComboValueOrKeepOriginal(ItemTypeCombo, IDX_ITEM_TYPE));
            SetListValue(row, IDX_EQUIP_TYPE, GetComboValueOrKeepOriginal(EquipTypeCombo, IDX_EQUIP_TYPE));
            SetListValue(row, IDX_TARGET, GetComboValueOrKeepOriginal(TargetCombo, IDX_TARGET));
            SetListValue(row, IDX_RESTRICT_LEVEL, GetBoxText(LevelBox));
            SetListValue(row, IDX_RESTRICT_MAX_LEVEL, GetBoxText(RestrictMaxLevelBox));
            SetListValue(row, IDX_REBIRTH_COUNT, GetBoxText(RebirthCountBox));
            SetListValue(row, IDX_REBIRTH_MAX_SCORE, GetBoxText(RebirthMaxScoreBox));
            SetListValue(row, IDX_ITEM_QUALITY, GetComboValueOrKeepOriginal(QualityCombo, IDX_ITEM_QUALITY));
            SetListValue(row, IDX_ITEM_GROUP, GetBoxText(ItemGroupBox));
            SetListValue(row, IDX_CASTING_TIME, GetBoxText(CastingTimeBox));
            SetListValue(row, IDX_COOLDOWN_TIME, GetBoxText(CoolDownTimeBox));
            SetListValue(row, IDX_COOLDOWN_GROUP, GetBoxText(CoolDownGroupBox));

            SetListValue(row, IDX_MAX_HP, GetBoxText(HpBox));
            SetListValue(row, IDX_MAX_MP, GetBoxText(MpBox));
            SetListValue(row, IDX_STR, GetBoxText(StrBox));
            SetListValue(row, IDX_CON, GetBoxText(ConBox));
            SetListValue(row, IDX_INT, GetBoxText(IntBox));
            SetListValue(row, IDX_VOL, GetBoxText(VolBox));
            SetListValue(row, IDX_DEX, GetBoxText(DexBox));
            SetListValue(row, IDX_AVG_PHYSICO_DAMAGE, GetBoxText(AvgPhysicoDamageBox));
            SetListValue(row, IDX_RAND_PHYSICO_DAMAGE, GetBoxText(RandPhysicoDamageBox));
            SetListValue(row, IDX_ATTACK_RANGE, GetBoxText(AttackRangeBox));
            SetListValue(row, IDX_ATTACK_SPEED, GetBoxText(AttackSpeedBox));
            SetListValue(row, IDX_ATTACK, GetBoxText(AtkBox));
            SetListValue(row, IDX_RANGE_ATTACK, GetBoxText(RangeAttackBox));
            SetListValue(row, IDX_PHYSICO_DEFENCE, GetBoxText(DefBox));
            SetListValue(row, IDX_MAGIC_DAMAGE, GetBoxText(MatkBox));
            SetListValue(row, IDX_MAGIC_DEFENCE, GetBoxText(MdefBox));
            SetListValue(row, IDX_HIT_RATE, GetBoxText(HitRateBox));
            SetListValue(row, IDX_DODGE_RATE, GetBoxText(DodgeRateBox));
            SetListValue(row, IDX_PHYSICO_CRITICAL_RATE, GetBoxText(PhysicoCriticalRateBox));
            SetListValue(row, IDX_PHYSICO_CRITICAL_DAMAGE, GetBoxText(PhysicoCriticalDamageBox));
            SetListValue(row, IDX_MAGIC_CRITICAL_RATE, GetBoxText(MagicCriticalRateBox));
            SetListValue(row, IDX_MAGIC_CRITICAL_DAMAGE, GetBoxText(MagicCriticalDamageBox));
            SetListValue(row, IDX_PHYSICAL_PENETRATION, GetBoxText(PhysicalPenetrationBox));
            SetListValue(row, IDX_MAGICAL_PENETRATION, GetBoxText(MagicalPenetrationBox));
            SetListValue(row, IDX_PHYSICAL_PENETRATION_DEFENCE, GetBoxText(PhysicalPenetrationDefenceBox));
            SetListValue(row, IDX_MAGICAL_PENETRATION_DEFENCE, GetBoxText(MagicalPenetrationDefenceBox));

            SetListValue(row, IDX_ATTRIBUTE, GetComboValueOrKeepOriginal(AttributeBox, IDX_ATTRIBUTE));
            SetListValue(row, IDX_ATTRIBUTE_RATE, GetBoxText(AttributeRateBox));
            SetListValue(row, IDX_ATTRIBUTE_DAMAGE, GetBoxText(AttributeDamageBox));
            SetListValue(row, IDX_ATTRIBUTE_RESIST, GetBoxText(AttributeResistBox));
            SetListValue(row, IDX_SPECIAL_TYPE, GetBoxText(SpecialTypeBox));
            SetListValue(row, IDX_SPECIAL_RATE, GetBoxText(SpecialRateBox));
            SetListValue(row, IDX_SPECIAL_DAMAGE, GetBoxText(SpecialDamageBox));
            SetListValue(row, IDX_ENCHANT_TYPE, GetBoxText(EnchantTypeBox));
            SetListValue(row, IDX_ENCHANT_ID, GetBoxText(EnchantIdBox));

            SetListValue(row, IDX_WEAPON_EFFECT_ID, GetBoxText(WeaponEffectIdBox));
            SetListValue(row, IDX_FLY_EFFECT_ID, GetBoxText(FlyEffectIdBox));
            SetListValue(row, IDX_USED_EFFECT_ID, GetBoxText(UsedEffectIdBox));
            SetListValue(row, IDX_USED_SOUND_NAME, GetBoxText(UsedSoundNameBox));
            SetListValue(row, IDX_ENHANCE_EFFECT_ID, GetBoxText(EnhanceEffectIdBox));

            SetListValue(row, IDX_DROP_RATE, GetBoxText(DropRateBox));
            SetListValue(row, IDX_DROP_INDEX, GetBoxText(DropIndexBox));
            SetListValue(row, IDX_TREASURE_BUFF_1, GetBoxText(TreasureBuff1Box));
            SetListValue(row, IDX_TREASURE_BUFF_2, GetBoxText(TreasureBuff2Box));
            SetListValue(row, IDX_TREASURE_BUFF_3, GetBoxText(TreasureBuff3Box));
            SetListValue(row, IDX_TREASURE_BUFF_4, GetBoxText(TreasureBuff4Box));

            SetListValue(row, IDX_EXPERT_LEVEL, GetBoxText(ExpertLevelBox));
            SetListValue(row, IDX_EXPERT_ENCHANT_ID, GetBoxText(ExpertEnchantIdBox));
            SetListValue(row, IDX_ELF_SKILL_ID, GetBoxText(ElfSkillIdBox));

            SetListValue(row, IDX_LIMIT_TYPE, GetBoxText(LimitTypeBox));
            SetListValue(row, IDX_DUE_DATE_TIME, GetBoxText(DueDateTimeBox));

            SetListValue(row, IDX_MAX_SOCKET, GetBoxText(MaxSocketBox));
            SetListValue(row, IDX_SOCKET_RATE, GetBoxText(SocketRateBox));
            SetListValue(row, IDX_MAX_STACK, GetBoxText(MaxStackBox));
            SetListValue(row, IDX_SHOP_PRICE_TYPE, GetComboValueOrKeepOriginal(ShopPriceTypeCombo, IDX_SHOP_PRICE_TYPE));
            SetListValue(row, IDX_SYS_PRICE, GetBoxText(SysPriceBox));

            SetListValue(row, IDX_MISSION_POS_ID, GetBoxText(MissionPosIdBox));
            SetListValue(row, IDX_BLOCK_RATE, GetBoxText(BlockRateBox));
            SetListValue(row, IDX_LOG_LEVEL, GetBoxText(LogLevelBox));
            SetListValue(row, IDX_AUCTION_TYPE, GetBoxText(AuctionTypeBox));
            SetListValue(row, IDX_EXTRA_DATA_1, GetBoxText(ExtraData1Box));
            SetListValue(row, IDX_EXTRA_DATA_2, GetBoxText(ExtraData2Box));
            SetListValue(row, IDX_EXTRA_DATA_3, GetBoxText(ExtraData3Box));

            ulong opFlagsValue = 0;
            foreach (var (value, cb) in _opflagsChecks)
            {
                if (cb.IsChecked == true)
                    opFlagsValue |= value;
            }
            opFlagsValue = PreserveUnknownBits(IDX_OP_FLAGS, opFlagsValue, OP_FLAGS);
            SetListValue(row, IDX_OP_FLAGS, FormatBitmaskLikeOriginal(IDX_OP_FLAGS, opFlagsValue));

            ulong opFlagsPlusValue = 0;
            foreach (var (value, cb) in _opflagsPlusChecks)
            {
                if (cb.IsChecked == true)
                    opFlagsPlusValue |= value;
            }
            opFlagsPlusValue = PreserveUnknownBits(IDX_OP_FLAGS_PLUS, opFlagsPlusValue, OP_FLAGS_PLUS);
            SetListValue(row, IDX_OP_FLAGS_PLUS, FormatBitmaskLikeOriginal(IDX_OP_FLAGS_PLUS, opFlagsPlusValue));

            ulong classValue = 0;
            foreach (var (value, cb) in _classChecks)
            {
                if (cb.IsChecked == true)
                    classValue |= value;
            }
            classValue = PreserveUnknownBits(IDX_RESTRICT_CLASS, classValue, RESTRICT_CLASSES);
            SetListValue(row, IDX_RESTRICT_CLASS, FormatBitmaskLikeOriginal(IDX_RESTRICT_CLASS, classValue));
            
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

        private void PatchIniRowInPlace(string path, Encoding encoding, string itemId, HashSet<int> changedColumns, List<string> editedRow)
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
                if (!string.Equals(lineId, itemId, StringComparison.Ordinal))
                    continue;

                int needed = Math.Max(parts.Count, editedRow.Count);
                while (parts.Count < needed)
                    parts.Add(string.Empty);

                foreach (int idx in changedColumns)
                {
                    string newValue = idx < editedRow.Count ? (editedRow[idx] ?? string.Empty) : string.Empty;
                    parts[idx] = newValue;
                }

                lines[i] = string.Join("|", parts);
                File.WriteAllLines(path, lines, encoding);
                return;
            }

            throw new InvalidOperationException($"Item {itemId} não foi encontrado em {Path.GetFileName(path)}.");
        }

        private void PatchTranslateRowInPlace(string path, Encoding encoding, string itemId, string newName, string newDesc)
        {
            if (!File.Exists(path))
                return;

            var lines = File.ReadAllLines(path, encoding);

            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split(new[] { '|' }, StringSplitOptions.None).ToList();

                if (parts.Count == 0)
                    continue;

                string lineId = (parts[0] ?? string.Empty).Trim();
                if (!string.Equals(lineId, itemId, StringComparison.Ordinal))
                    continue;

                while (parts.Count < 3)
                    parts.Add(string.Empty);

                parts[1] = newName ?? string.Empty;
                parts[2] = newDesc ?? string.Empty;

                lines[i] = string.Join("|", parts);
                File.WriteAllLines(path, lines, encoding);
                return;
            }

            var newLine = string.Join("|", new[]
            {
        itemId ?? string.Empty,
        newName ?? string.Empty,
        newDesc ?? string.Empty
    });

            var output = lines.ToList();
            output.Add(newLine);
            File.WriteAllLines(path, output, encoding);
        }
        private void CopyControlsToCurrentRow()
        {
            if (_currentRow == null)
                return;

            var editedRow = BuildEditedRowFromControls();
            string currentId = GetValue(editedRow, IDX_ID);

            _currentRow = editedRow;

            if (!string.IsNullOrWhiteSpace(currentId))
                db.Rows[currentId] = _currentRow;
        }

        private bool HasCurrentItemChanges()
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

            string currentName = GetBoxText(NameBox);
            string currentDesc = GetBoxText(DescBox);

            if (!string.Equals(currentName, _originalNameSnapshot ?? string.Empty, StringComparison.Ordinal))
                return true;

            if (!string.Equals(currentDesc, _originalDescSnapshot ?? string.Empty, StringComparison.Ordinal))
                return true;

            return false;
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

            foreach (var kv in db.Rows.OrderBy(x => x.Key, Comparer<string>.Create(CompareItemIds)))
                lines.Add(string.Join("|", kv.Value));

            using var sw = new StreamWriter(path, false, encoding);
            foreach (var line in lines)
                sw.WriteLine(line);
        }

        private void SaveTranslateIni(string path, Encoding encoding)
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

            foreach (var kv in db.Translations.OrderBy(x => x.Key, Comparer<string>.Create(CompareItemIds)))
            {
                string id = kv.Key ?? string.Empty;
                string name = kv.Value?.Name ?? string.Empty;
                string desc = kv.Value?.Description ?? string.Empty;
                lines.Add($"{id}|{name}|{desc}");
            }

            using var sw = new StreamWriter(path, false, encoding);
            foreach (var line in lines)
                sw.WriteLine(line);
        }

        private void SaveCurrentItemFiles()
        {
            if (_currentRow == null)
                return;

            string currentId = GetValue(_currentRow, IDX_ID);
            if (string.IsNullOrWhiteSpace(currentId))
                return;

            var editedRow = BuildEditedRowFromControls();
            var changedColumns = GetChangedColumnIndices(_originalRowSnapshot, editedRow);

            string currentName = GetBoxText(NameBox);
            string currentDesc = GetBoxText(DescBox);

            bool translateChanged =
                !string.Equals(currentName, _originalNameSnapshot ?? string.Empty, StringComparison.Ordinal) ||
                !string.Equals(currentDesc, _originalDescSnapshot ?? string.Empty, StringComparison.Ordinal);

            string sItemPath = Path.Combine(clientPath, "data", "db", "S_ItemMall.ini");
            string cItemPath = Path.Combine(clientPath, "data", "db", "C_ItemMall.ini");
            string tItemPath = Path.Combine(clientPath, "data", "translate", "T_ItemMall.ini");

            PatchIniRowInPlace(sItemPath, Encoding.GetEncoding(950), currentId, changedColumns, editedRow);

            if (File.Exists(cItemPath))
                PatchIniRowInPlace(cItemPath, Encoding.GetEncoding(950), currentId, changedColumns, editedRow);

            if (translateChanged || !db.Translations.ContainsKey(currentId))
                PatchTranslateRowInPlace(tItemPath, Encoding.GetEncoding(1252), currentId, currentName, currentDesc);

            _currentRow = editedRow;
            db.Rows[currentId] = new List<string>(editedRow);

            db.Translations[currentId] = new GenericTranslation
            {
                Name = currentName,
                Description = currentDesc
            };

            _originalRowSnapshot = editedRow.Select(x => x ?? string.Empty).ToList();
            _originalNameSnapshot = currentName;
            _originalDescSnapshot = currentDesc;
        }

        private bool ConfirmSaveIfNeeded()
        {
            if (!HasCurrentItemChanges())
                return true;

            var result = MessageBox.Show(
                "Este item foi modificado. Deseja salvar antes de trocar para outro item?",
                "Salvar alterações",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
                return false;

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    SaveCurrentItemFiles();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Erro ao salvar arquivos:\n\n" + ex.Message,
                        "Erro",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return false;
                }
            }

            return true;
        }

        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (db == null)
                return;

            searchCts?.Cancel();
            searchCts = new CancellationTokenSource();
            var token = searchCts.Token;

            string filter = (SearchBox.Text ?? string.Empty).Trim().ToLowerInvariant();

            try
            {
                var result = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();

                    return db.Rows
                        .Where(x =>
                            string.IsNullOrEmpty(filter) ||
                            x.Key.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                            (db.Translations.ContainsKey(x.Key) &&
                             (db.Translations[x.Key].Name?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)))
                        .OrderBy(x => x.Key, Comparer<string>.Create(CompareItemIds))
                        .Select(x => new ItemEntry
                        {
                            Id = x.Key,
                            Name = db.Translations.TryGetValue(x.Key, out var t) ? t.Name : "Unknown"
                        })
                        .ToList();
                }, token);

                Items.Clear();
                foreach (var item in result)
                    Items.Add(item);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void ItemList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionChanged)
                return;

            if (ItemList.SelectedItem is not ItemEntry entry)
                return;

            if (_lastSelectedItemId != null && _lastSelectedItemId != entry.Id)
            {
                if (!ConfirmSaveIfNeeded())
                {
                    _suppressSelectionChanged = true;
                    ItemList.SelectedItem = Items.FirstOrDefault(x => x.Id == _lastSelectedItemId);
                    _suppressSelectionChanged = false;
                    return;
                }
            }

            if (!db.Rows.TryGetValue(entry.Id, out var rowFromDb))
                return;

            _currentRow = rowFromDb.Select(x => x?.Trim() ?? string.Empty).ToList();
            _loading = true;

            try
            {
                SetText(IconBox, GetValue(_currentRow, IDX_ICON));
                SetText(ModelIdBox, GetValue(_currentRow, IDX_MODEL_ID));
                SetText(ModelFileBox, GetValue(_currentRow, IDX_MODEL_FILE));
                SetText(LevelBox, GetValue(_currentRow, IDX_RESTRICT_LEVEL));

                if (db.Translations.TryGetValue(entry.Id, out var translation))
                {
                    SetText(NameBox, translation.Name ?? string.Empty);
                    SetText(DescBox, translation.Description ?? string.Empty);
                }
                else
                {
                    SetText(NameBox, string.Empty);
                    SetText(DescBox, string.Empty);
                }

                if (int.TryParse(GetValue(_currentRow, IDX_ITEM_TYPE), out var itemType))
                    SelectComboByValue(ItemTypeCombo, itemType);
                else if (ItemTypeCombo != null)
                    ItemTypeCombo.SelectedIndex = -1;

                if (int.TryParse(GetValue(_currentRow, IDX_ITEM_QUALITY), out var quality))
                    SelectComboByValue(QualityCombo, quality);
                else if (QualityCombo != null)
                    QualityCombo.SelectedIndex = -1;

                if (int.TryParse(GetValue(_currentRow, IDX_EQUIP_TYPE), out var equipType))
                    SelectComboByValue(EquipTypeCombo, equipType);
                else if (EquipTypeCombo != null)
                    EquipTypeCombo.SelectedIndex = -1;

                if (int.TryParse(GetValue(_currentRow, IDX_TARGET), out var target))
                    SelectComboByValue(TargetCombo, target);
                else if (TargetCombo != null)
                    TargetCombo.SelectedIndex = -1;

                if (int.TryParse(GetValue(_currentRow, IDX_SHOP_PRICE_TYPE), out var shopPriceType))
                    SelectComboByValue(ShopPriceTypeCombo, shopPriceType);
                else if (ShopPriceTypeCombo != null)
                    ShopPriceTypeCombo.SelectedIndex = -1;

                SetText(WeaponEffectIdBox, GetValue(_currentRow, IDX_WEAPON_EFFECT_ID));
                SetText(FlyEffectIdBox, GetValue(_currentRow, IDX_FLY_EFFECT_ID));
                SetText(UsedEffectIdBox, GetValue(_currentRow, IDX_USED_EFFECT_ID));
                SetText(UsedSoundNameBox, GetValue(_currentRow, IDX_USED_SOUND_NAME));
                SetText(EnhanceEffectIdBox, GetValue(_currentRow, IDX_ENHANCE_EFFECT_ID));

                SetText(DropRateBox, GetValue(_currentRow, IDX_DROP_RATE));
                SetText(DropIndexBox, GetValue(_currentRow, IDX_DROP_INDEX));
                SetText(TreasureBuff1Box, GetValue(_currentRow, IDX_TREASURE_BUFF_1));
                SetText(TreasureBuff2Box, GetValue(_currentRow, IDX_TREASURE_BUFF_2));
                SetText(TreasureBuff3Box, GetValue(_currentRow, IDX_TREASURE_BUFF_3));
                SetText(TreasureBuff4Box, GetValue(_currentRow, IDX_TREASURE_BUFF_4));


                if (int.TryParse(GetValue(_currentRow, IDX_RESTRICT_GENDER), out var restrictGender))
                    SelectComboByValue(RestrictGenderCombo, restrictGender);
                else if (RestrictGenderCombo != null)
                    RestrictGenderCombo.SelectedIndex = -1;


                SetText(RebirthScoreBox, GetValue(_currentRow, IDX_REBIRTH_SCORE));


                if (int.TryParse(GetValue(_currentRow, IDX_RESTRICT_ALIGN), out var restrictAlign))
                    SelectComboByValue(RestrictAlignCombo, restrictAlign);
                else if (RestrictAlignCombo != null)
                    RestrictAlignCombo.SelectedIndex = -1;


                SetText(RestrictPrestigeBox, GetValue(_currentRow, IDX_RESTRICT_PRESTIGE));
                SetText(BackpackSizeBox, GetValue(_currentRow, IDX_BACKPACK_SIZE));
                SetText(MaxDurabilityBox, GetValue(_currentRow, IDX_MAX_DURABILITY));
                SetText(RestrictEventPosIdBox, GetValue(_currentRow, IDX_RESTRICT_EVENT_POS_ID));
                SetText(EnchantTimeTypeBox, GetValue(_currentRow, IDX_ENCHANT_TIME_TYPE));
                SetText(EnchantDurationBox, GetValue(_currentRow, IDX_ENCHANT_DURATION));


                SetText(ExpertLevelBox, GetValue(_currentRow, IDX_EXPERT_LEVEL));
                SetText(ExpertEnchantIdBox, GetValue(_currentRow, IDX_EXPERT_ENCHANT_ID));
                SetText(ElfSkillIdBox, GetValue(_currentRow, IDX_ELF_SKILL_ID));

                SetText(LimitTypeBox, GetValue(_currentRow, IDX_LIMIT_TYPE));
                SetText(DueDateTimeBox, GetValue(_currentRow, IDX_DUE_DATE_TIME));

                SetText(MissionPosIdBox, GetValue(_currentRow, IDX_MISSION_POS_ID));
                SetText(BlockRateBox, GetValue(_currentRow, IDX_BLOCK_RATE));
                SetText(LogLevelBox, GetValue(_currentRow, IDX_LOG_LEVEL));
                SetText(AuctionTypeBox, GetValue(_currentRow, IDX_AUCTION_TYPE));
                SetText(ExtraData1Box, GetValue(_currentRow, IDX_EXTRA_DATA_1));
                SetText(ExtraData2Box, GetValue(_currentRow, IDX_EXTRA_DATA_2));
                SetText(ExtraData3Box, GetValue(_currentRow, IDX_EXTRA_DATA_3));

                SetText(MaxStackBox, GetValue(_currentRow, IDX_MAX_STACK));
                SetText(MaxSocketBox, GetValue(_currentRow, IDX_MAX_SOCKET));
                SetText(SocketRateBox, GetValue(_currentRow, IDX_SOCKET_RATE));
                SetText(SysPriceBox, GetValue(_currentRow, IDX_SYS_PRICE));
                SetText(RestrictMaxLevelBox, GetValue(_currentRow, IDX_RESTRICT_MAX_LEVEL));
                SetText(CoolDownTimeBox, GetValue(_currentRow, IDX_COOLDOWN_TIME));
                SetText(CoolDownGroupBox, GetValue(_currentRow, IDX_COOLDOWN_GROUP));
                SetText(ItemGroupBox, GetValue(_currentRow, IDX_ITEM_GROUP));
                SetText(CastingTimeBox, GetValue(_currentRow, IDX_CASTING_TIME));
                SetText(RebirthCountBox, GetValue(_currentRow, IDX_REBIRTH_COUNT));
                SetText(RebirthMaxScoreBox, GetValue(_currentRow, IDX_REBIRTH_MAX_SCORE));

                SetText(HpBox, GetValue(_currentRow, IDX_MAX_HP));
                SetText(MpBox, GetValue(_currentRow, IDX_MAX_MP));
                SetText(StrBox, GetValue(_currentRow, IDX_STR));
                SetText(DexBox, GetValue(_currentRow, IDX_DEX));
                SetText(IntBox, GetValue(_currentRow, IDX_INT));
                SetText(AtkBox, GetValue(_currentRow, IDX_ATTACK));
                SetText(DefBox, GetValue(_currentRow, IDX_PHYSICO_DEFENCE));
                SetText(MatkBox, GetValue(_currentRow, IDX_MAGIC_DAMAGE));
                SetText(MdefBox, GetValue(_currentRow, IDX_MAGIC_DEFENCE));

                SetText(ConBox, GetValue(_currentRow, IDX_CON));
                SetText(VolBox, GetValue(_currentRow, IDX_VOL));
                SetText(AvgPhysicoDamageBox, GetValue(_currentRow, IDX_AVG_PHYSICO_DAMAGE));
                SetText(RandPhysicoDamageBox, GetValue(_currentRow, IDX_RAND_PHYSICO_DAMAGE));
                SetText(AttackRangeBox, GetValue(_currentRow, IDX_ATTACK_RANGE));
                SetText(AttackSpeedBox, GetValue(_currentRow, IDX_ATTACK_SPEED));
                SetText(RangeAttackBox, GetValue(_currentRow, IDX_RANGE_ATTACK));
                SetText(HitRateBox, GetValue(_currentRow, IDX_HIT_RATE));
                SetText(DodgeRateBox, GetValue(_currentRow, IDX_DODGE_RATE));
                SetText(PhysicoCriticalRateBox, GetValue(_currentRow, IDX_PHYSICO_CRITICAL_RATE));
                SetText(PhysicoCriticalDamageBox, GetValue(_currentRow, IDX_PHYSICO_CRITICAL_DAMAGE));
                SetText(MagicCriticalRateBox, GetValue(_currentRow, IDX_MAGIC_CRITICAL_RATE));
                SetText(MagicCriticalDamageBox, GetValue(_currentRow, IDX_MAGIC_CRITICAL_DAMAGE));
                SetText(PhysicalPenetrationBox, GetValue(_currentRow, IDX_PHYSICAL_PENETRATION));
                SetText(MagicalPenetrationBox, GetValue(_currentRow, IDX_MAGICAL_PENETRATION));
                SetText(PhysicalPenetrationDefenceBox, GetValue(_currentRow, IDX_PHYSICAL_PENETRATION_DEFENCE));
                SetText(MagicalPenetrationDefenceBox, GetValue(_currentRow, IDX_MAGICAL_PENETRATION_DEFENCE));

                if (int.TryParse(GetValue(_currentRow, IDX_ATTRIBUTE), out var attribute))
                    SelectComboByValue(AttributeBox, attribute);
                else if (AttributeBox != null)
                    AttributeBox.SelectedIndex = -1;

                SetText(AttributeRateBox, GetValue(_currentRow, IDX_ATTRIBUTE_RATE));
                SetText(AttributeDamageBox, GetValue(_currentRow, IDX_ATTRIBUTE_DAMAGE));
                SetText(AttributeResistBox, GetValue(_currentRow, IDX_ATTRIBUTE_RESIST));

                SetText(SpecialTypeBox, GetValue(_currentRow, IDX_SPECIAL_TYPE));
                SetText(SpecialRateBox, GetValue(_currentRow, IDX_SPECIAL_RATE));
                SetText(SpecialDamageBox, GetValue(_currentRow, IDX_SPECIAL_DAMAGE));

                SetText(EnchantTypeBox, GetValue(_currentRow, IDX_ENCHANT_TYPE));
                SetText(EnchantIdBox, GetValue(_currentRow, IDX_ENCHANT_ID));

                ulong opFlagsValue = ParseULongSafe(GetValue(_currentRow, IDX_OP_FLAGS));
                ulong opFlagsPlusValue = ParseULongSafe(GetValue(_currentRow, IDX_OP_FLAGS_PLUS));
                ulong classValue = ParseULongSafe(GetValue(_currentRow, IDX_RESTRICT_CLASS));

                SetChecksFromValue(_opflagsChecks, opFlagsValue, FlagCheckChanged);
                SetChecksFromValue(_opflagsPlusChecks, opFlagsPlusValue, FlagPlusCheckChanged);
                SetChecksFromValue(_classChecks, classValue, ClassCheckChanged);

                RefreshFlagDisplays(opFlagsValue, classValue);

                if (opflagsplus_value != null)
                    opflagsplus_value.Text = $"{opFlagsPlusValue}  |  0x{opFlagsPlusValue:X}";

                SetText(ClassBox, $"{classValue} | 0x{classValue:X}");
                SetToolTip(
                    ClassBox,
                    $"Valor decimal: {classValue}\n" +
                    $"Valor hexadecimal: 0x{classValue:X}\n" +
                    $"Composição: {BuildClassDecomposition(classValue)}\n\n" +
                    $"{BuildClassSummary(classValue)}");

                string opFlagsText = BuildFlagsSummary(opFlagsValue, OP_FLAGS);
                string opFlagsHex = $"0x{opFlagsValue:X}";
                string opTooltip = $"Valor decimal: {opFlagsValue} | {opFlagsHex}\n{opFlagsText}";

                foreach (var (_, cb) in _opflagsChecks)
                    cb.ToolTip = opTooltip;

                string opFlagsPlusText = BuildFlagsSummary(opFlagsPlusValue, OP_FLAGS_PLUS);
                string opFlagsPlusHex = $"0x{opFlagsPlusValue:X}";
                string opPlusTooltip = $"Valor decimal: {opFlagsPlusValue} | {opFlagsPlusHex}\n{opFlagsPlusText}";

                foreach (var (_, cb) in _opflagsPlusChecks)
                    cb.ToolTip = opPlusTooltip;

                LoadIcon(entry.Id);

                _lastSelectedItemId = entry.Id;
                _originalRowSnapshot = _currentRow.Select(s => s ?? string.Empty).ToList();
                _originalNameSnapshot = GetBoxText(NameBox);
                _originalDescSnapshot = GetBoxText(DescBox);
            }
            finally
            {
                _loading = false;
            }
        }

        private List<FlagDef> DecodeFlags(ulong value, List<FlagDef> defs)
        {
            var result = new List<FlagDef>();

            foreach (var def in defs.OrderBy(d => d.Value))
            {
                if ((value & def.Value) == def.Value)
                    result.Add(def);
            }

            return result;
        }

        private string BuildFlagsSummary(ulong value, List<FlagDef> defs)
        {
            var flags = DecodeFlags(value, defs);

            if (flags.Count == 0)
                return "Nenhuma flag marcada";

            return string.Join(", ", flags.Select(f => $"{f.Label} ({f.Value})"));
        }

        private string BuildClassSummary(ulong value)
        {
            var classes = DecodeFlags(value, RESTRICT_CLASSES);

            if (classes.Count == 0)
                return "Nenhuma classe marcada";

            return string.Join(", ", classes.Select(c => $"{c.Label} (0x{c.Value:X})"));
        }

        private string BuildClassDecomposition(ulong value)
        {
            var classes = DecodeFlags(value, RESTRICT_CLASSES);

            if (classes.Count == 0)
                return "0";

            return string.Join(" + ", classes.Select(c => c.Value.ToString()));
        }

        private string GetValue(List<string> row, int index)
        {
            if (row == null || index < 0 || index >= row.Count)
                return string.Empty;

            return row[index]?.Trim() ?? string.Empty;
        }

        private ulong ParseULongSafe(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            value = value.Trim();

            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (ulong.TryParse(
                    value.Substring(2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out var hexValue))
                {
                    return hexValue;
                }
            }

            if (ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var decValue))
                return decValue;

            bool looksHex = value.Any(c =>
                (c >= 'A' && c <= 'F') ||
                (c >= 'a' && c <= 'f'));

            if (looksHex)
            {
                if (ulong.TryParse(
                    value,
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out var hexNoPrefixValue))
                {
                    return hexNoPrefixValue;
                }
            }

            return 0;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveCurrentItemFiles();

                MessageBox.Show(
                    "Alterações salvas com sucesso em S_ItemMall.ini, C_ItemMall.ini e T_ItemMall.ini.",
                    "Salvar",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Erro ao salvar arquivos:\n\n" + ex.Message,
                    "Erro",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333"))
            };

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };

            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var cancelButton = new Button
            {
                Content = "Cancelar",
                Width = 80,
                Height = 30
            };

            okButton.Click += (_, __) => window.DialogResult = true;
            cancelButton.Click += (_, __) => window.DialogResult = false;

            buttons.Children.Add(okButton);
            buttons.Children.Add(cancelButton);

            Grid.SetRow(text, 0);
            Grid.SetRow(input, 1);
            Grid.SetRow(buttons, 2);

            root.Children.Add(text);
            root.Children.Add(input);
            root.Children.Add(buttons);

            window.Content = root;

            return window.ShowDialog() == true ? input.Text?.Trim() ?? string.Empty : null;
        }

        private void Clone_Click(object sender, RoutedEventArgs e)
        {
            if (ItemList.SelectedItem is not ItemEntry selected)
            {
                MessageBox.Show(
                    "Selecione um item para clonar.",
                    "Clonar item",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!db.Rows.TryGetValue(selected.Id, out var row))
                return;

            string newId = PromptNewId("Clonar item", "Informe o novo ID:");

            if (newId == null)
                return;

            if (string.IsNullOrWhiteSpace(newId))
            {
                MessageBox.Show(
                    "O novo ID não pode ficar vazio.",
                    "Clonar item",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (db.Rows.ContainsKey(newId))
            {
                MessageBox.Show(
                    "Esse ID já existe. Escolha outro ID.",
                    "Clonar item",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var clonedRow = new List<string>(row);
            while (clonedRow.Count <= IDX_ID)
                clonedRow.Add(string.Empty);

            clonedRow[IDX_ID] = newId;
            db.Rows[newId] = clonedRow;

            if (db.Translations.TryGetValue(selected.Id, out var translation))
            {
                db.Translations[newId] = new GenericTranslation
                {
                    Name = translation.Name,
                    Description = translation.Description
                };
            }
            else
            {
                db.Translations[newId] = new GenericTranslation
                {
                    Name = selected.Name,
                    Description = string.Empty
                };
            }

            try
            {
                string sItemPath = Path.Combine(clientPath, "data", "db", "S_ItemMall.ini");
                string cItemPath = Path.Combine(clientPath, "data", "db", "C_ItemMall.ini");
                string tItemPath = Path.Combine(clientPath, "data", "translate", "T_ItemMall.ini");

                SaveDataIni(sItemPath, Encoding.GetEncoding(950));
                SaveDataIni(cItemPath, Encoding.GetEncoding(950));
                SaveTranslateIni(tItemPath, Encoding.GetEncoding(1252));
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "O item foi clonado em memória, mas houve erro ao salvar:\n\n" + ex.Message,
                    "Erro",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            LoadDatabase();

            _suppressSelectionChanged = true;
            ItemList.SelectedItem = Items.FirstOrDefault(x => x.Id == newId);
            _suppressSelectionChanged = false;

            ItemList.SelectedItem = Items.FirstOrDefault(x => x.Id == newId);

            MessageBox.Show(
                $"Item clonado com sucesso para o ID {newId}.",
                "Clonar item",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfirmSaveIfNeeded())
                return;

            LoadDatabase();
            _currentRow = null;
            _originalRowSnapshot = null;
            _originalNameSnapshot = null;
            _originalDescSnapshot = null;
            _lastSelectedItemId = null;
        }

        private void LoadIcon(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                if (ItemIcon != null) ItemIcon.Source = null;
                return;
            }

            // Get icon name from db (S_ItemMall.ini row)
            if (db != null && db.Rows.TryGetValue(itemId, out var row) && row.Count > IDX_ICON)
            {
                string iconName = row[IDX_ICON]?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(iconName))
                {
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
                            if (File.Exists(path))
                            {
                                if (ItemIcon != null) ItemIcon.Source = DdsLoader.Load(path);
                                return;
                            }
                        }
                    }
                }
            }

            if (ItemIcon != null) ItemIcon.Source = null;
        }

        private static void SetText(TextBox box, string value)
        {
            if (box != null)
                box.Text = value ?? string.Empty;
        }

        private static void SetToolTip(Control control, string tooltip)
        {
            if (control != null)
                control.ToolTip = tooltip;
        }

        private static void SetToolTip(CheckBox control, string tooltip)
        {
            if (control != null)
                control.ToolTip = tooltip;
        }

        // Dicionario Do tipo de Item

        private readonly Dictionary<int, string> ITEM_TYPE = new()
        {
            {0,"Unknown"},
            {1,"Capacete"},
            {2,"Peito"},
            {3,"Calça"},
            {4,"Luva"},
            {5,"Pés"},
            {6,"Capa"},
            {7,"Espada 1H"},
            {8,"Espada 2H"},
            {9,"Martelo"},
            {10,"Martelo 2H"},
            {11,"Machado"},
            {12,"Machado 2H"},
            {13,"Arco"},
            {14,"Rifle"},
            {15,"Relyc"},
            {16,"Cajado"},
            {17,"Escudo"},
            {18,"Trinket"},
            {19,"Flecha"},
            {20,"Munição"},
            {21,"Mochila"},
            {22,"Item"},
            {23,"Material"},
            {24,"Pedra"},
            {25,"Pergaminho"},
            {26,"Pedr. Habil."},
            {27,"Equip Set"},
            {28,"Tesouro"},
            {29,"LuckyBag"},
            {30,"ElfStone"},
            {31,"ElfEquip"},
            {32,"Chip"},
            {33,"Formula"},
            {34,"Crystal"},
            {35,"Kuso"},
            {36,"Fantasia 1H"},
            {37,"Fantasia 2H"},
            {38,"Fantasia Cajado"},
            {39,"Fantasia Arco"},
            {40,"Fantasia Arma"},
            {41,"Fantasia Escudo"},
            {42,"Fantasia Corpo"},
            {43,"ElfGameItem"},
            {44,"WitchCraft"},
            {45,"Building"},
            {46,"UnBindItem"},
            {47,"Mochila Sprit."},
            {48,"Food"},
            {49,"MatchItem"},
            {50,"KusoTrinket"},
            {51,"ElfBook1"},
            {52,"ElfBook2"},
            {53,"ElfBook3"},
            {54,"Arma Mecha 1H"},
            {55,"Arma Mecha 2H"},
            {56,"Canhão"},
            {57,"Bala de Canhão"},
            {58,"KusoSoulCrystal"},
            {59,"Katana"},
            {60,"Chave"},
            {61,"Cartão Postal"},
            {62,"Souvenir"},
            {63,"OptionalLuckyBag"},
            {8001,"CombineRate"},
            {8002,"StrengthenRate"},
            {8003,"CookRate"},
            {8004,"MatchRate"},
            {8005,"StrengthenTransfer"},
            {8006,"RideCombinePoint"},
            {9001,"OpenUIStart"},
            {9999,"OpenUIEnd"},
            {10000,"Max"},
        };

        // dicionario de status elemental 

        private readonly Dictionary<int, string> ITEM_ATTRIBUTE = new()
        {
            {0, "None"},
            {1, "Luz"},
            {2, "Escuridão"},
            {3, "Relâmpago"},
            {4, "Fogo"},
            {5, "Gelo"},
            {6, "Natureza"},
        };

        // dicionario de tipo de equipamento

        private readonly Dictionary<int, string> EQUIP_TYPE = new()
        {
            {-1,"Unknow"},
            {0,"None"},
            {1,"Capacete"},
            {2,"Peito"},
            {3,"Calça"},
            {4,"Luva"},
            {5,"Pes"},
            {6,"Capa"},
            {7,"Uma mão"},
            {8,"LeftHand"},
            {9,"Duas Mãos"},
            {10,"Shoot"},
            {11,"Trinket"},
            {13,"Munição"},
            {14,"Mochila"},
            {15,"SpellStoneA"},
            {16,"SpellStoneB"},
            {17,"SpellStoneC"},
            {18,"SpellStoneD"},
            {19,"ElfStoneA"},
            {20,"ElfStoneB"},
            {21,"ElfCap"},
            {22,"ElfClothes"},
            {23,"ElfFurnitureA"},
            {24,"ElfFurnitureB"},
            {25,"ElfFurnitureC"},
            {26,"Piso Sprite"},
            {27,"Parede Sprite"},
            {28,"Uma Mão"},
            {29,"TrinketOnly"},
            {30,"SpellStoneE"},
            {31,"SpellStoneF"},
            {32,"SpellStoneG"},
            {33,"SpellStoneH"},
            {34,"IsleEquipA"},
            {35,"IsleEquipB"},
            {36,"IsleEquipC"},
            {37,"IsleEquipD"},
            {38,"IsleEquipE"},
            {39,"IsleEquipF"},
            {40,"IKRide"},
            {41,"GKRide"},
            {42,"SoulCrystal"},
            {43,"Souvenir"},
            {44,"Max"},
        };


        // Dicionario  de alvo do item

        private readonly Dictionary<int, string> ITEM_TARGET = new()
        {
            {0,"Vazio"},
            {1,"None"},
            {2,"ToSelf"},
            {3,"ToElf"},
            {4,"ToChar"},
            {5,"ToItem"},
            {6,"ToCrop"},
            {7,"ToFarm"},
            {8,"ToIsleScene"},
            {9,"ToIsleStatue"},
            {10,"ToHiredFarm"},
            {11,"ToFishGround"},
            {12,"ToFish"},
            {13,"ToPit"},
            {14,"ToMineral"},
            {15,"ToPlant"},
            {16,"ToIsleFarm"},
            {17,"ToCattle"},
            {18,"ToPasturage"},
            {19,"ToESShortcut"},
            {20,"ToMonster"},
            {21,"ToNPC"},
            {22,"ToSpellAA"},
        };

        // dicionario de qualidade do item

        private readonly Dictionary<int, string> ITEM_QUALITY = new()
        {
            {0,"None"},
            {1,"Cinza"},
            {2,"Verde"},
            {3,"Azul"},
            {4,"Laranja"},
            {5,"Amarelo"},
            {6,"Roxo"},
            {7,"Red"},
            {8,"End"},
        };

        // dicionario de moedas 

        private readonly Dictionary<int, string> SHOP_PRICE_TYPE = new()
        {
            {1, "Gold"},
            {2, "Ponto PvP"},
            {3, "Emblema Rosa Cristal"},
            {4, "Ponto Casal"},
            {5, "Ponto GvG"},
            {6, "Selo de Caça"},
            {7, "Moedas Coco"},
            {9, "Reputação de Classe"},
            {10, "Resistência Kaslow"},
            {11, "Vingança de Ilya"},
            {12, "Mercenários Jale"},
        };

        // dicionario de flags de uso

        private readonly List<FlagDef> OP_FLAGS = new()
        {
            new FlagDef { Label = "Usavel", Value = 1 },
            new FlagDef { Label = "Não Gasta", Value = 2 },
            new FlagDef { Label = "Não trocavel", Value = 4 },
            new FlagDef { Label = "Não joga fora", Value = 8 },
            new FlagDef { Label = "NoEnhance (16)", Value = 16 },
            new FlagDef { Label = "NoRepair", Value = 32 },
            new FlagDef { Label = "Combineable", Value = 64 },
            new FlagDef { Label = "BindOnEquip", Value = 128 },
            new FlagDef { Label = "Acumula Tempo", Value = 256 },
            new FlagDef { Label = "NoSameBuff", Value = 512 },
            new FlagDef { Label = "Proibido em combate", Value = 1024 },
            new FlagDef { Label = "NoInTown", Value = 2048 },
            new FlagDef { Label = "NoInCave", Value = 4096 },
            new FlagDef { Label = "NoInInstance", Value = 8192 },
            new FlagDef { Label = "LinkToQuest", Value = 16384 },
            new FlagDef { Label = "Usar Morto", Value = 32768 },
            new FlagDef { Label = "Only1", Value = 65536 },
            new FlagDef { Label = "Only2", Value = 131072 },
            new FlagDef { Label = "Only3", Value = 262144 },
            new FlagDef { Label = "Only4", Value = 524288 },
            new FlagDef { Label = "Only5", Value = 1048576 },
            new FlagDef { Label = "Only", Value = 2031616 },
            new FlagDef { Label = "Replaceable1", Value = 2097152 },
            new FlagDef { Label = "Replaceable2", Value = 4194304 },
            new FlagDef { Label = "Replaceable3", Value = 8388608 },
            new FlagDef { Label = "Replaceable4", Value = 16777216 },
            new FlagDef { Label = "Replaceable5", Value = 33554432 },
            new FlagDef { Label = "Replaceable", Value = 65011712 },
            new FlagDef { Label = "Proibido Arena", Value = 67108864 },
            new FlagDef { Label = "NoInField", Value = 134217728 },
            new FlagDef { Label = "Some se mudar de mapa", Value = 268435456 },
            new FlagDef { Label = "UnBindItem", Value = 536870912 },
        };

        // dicionario de flags de uso 2

        private readonly List<FlagDef> OP_FLAGS_PLUS = new()
        {
            new FlagDef { Label = "IKCombine", Value = 1 },
            new FlagDef { Label = "GKCombine", Value = 2 },
            new FlagDef { Label = "EquipShow", Value = 4 },
            new FlagDef { Label = "PurpleWLimit", Value = 8 },
            new FlagDef { Label = "PurpleALimit", Value = 16 },
            new FlagDef { Label = "UseBind", Value = 32 },
            new FlagDef { Label = "OneStack", Value = 64 },
            new FlagDef { Label = "RideCombineIK", Value = 128 },
            new FlagDef { Label = "RideCombineGK", Value = 256 },
            new FlagDef { Label = "VIP", Value = 512 },
            new FlagDef { Label = "ChairCombineIK", Value = 2048 },
            new FlagDef { Label = "ChairCombineGK", Value = 4096 },
            new FlagDef { Label = "RedWLimit", Value = 8192 },
            new FlagDef { Label = "RedALimit", Value = 16384 },
            new FlagDef { Label = "CrystalCombo", Value = 32768 },
            new FlagDef { Label = "SouvenirCombo", Value = 65536 },
            new FlagDef { Label = "GodAreaCombo", Value = 131072 },
            new FlagDef { Label = "ElftabletEquip", Value = 262144 },
            new FlagDef { Label = "ElftabletExp", Value = 524288 },
            new FlagDef { Label = "ShowProbability", Value = 1048576 },
            new FlagDef { Label = "StorageForbidden", Value = 2097152 },
            new FlagDef { Label = "FamilyStorageForbidden", Value = 4194304 },
        };


        // dicionario de classe 

        private readonly List<FlagDef> RESTRICT_CLASSES = new()
        {
            new FlagDef { Label = "Novato", Value = 0x0001 },
            new FlagDef { Label = "Guerreiro (0002)", Value = 0x0002 },
            new FlagDef { Label = "Guerreiro (0004)", Value = 0x0004 },
            new FlagDef { Label = "Berzeker", Value = 0x0008 },
            new FlagDef { Label = "Paladino", Value = 0x0010 },
            new FlagDef { Label = "Arqueiro (0020)", Value = 0x0020 },
            new FlagDef { Label = "Arqueiro (0040)", Value = 0x0040 },
            new FlagDef { Label = "Ranger", Value = 0x0080 },
            new FlagDef { Label = "Assassino", Value = 0x0100 },
            new FlagDef { Label = "Sacerdote (0200)", Value = 0x0200 },
            new FlagDef { Label = "Sacerdote (0400)", Value = 0x0400 },
            new FlagDef { Label = "Clerigo", Value = 0x0800 },
            new FlagDef { Label = "Sabio", Value = 0x1000 },
            new FlagDef { Label = "Mago (2000)", Value = 0x2000 },
            new FlagDef { Label = "Mago (4000)", Value = 0x4000 },
            new FlagDef { Label = "Feiticeiro", Value = 0x8000 },
            new FlagDef { Label = "Necromante", Value = 0x10000 },
            new FlagDef { Label = "Tita", Value = 0x20000 },
            new FlagDef { Label = "Templa", Value = 0x40000 },
            new FlagDef { Label = "F.A.", Value = 0x80000 },
            new FlagDef { Label = "S. S.", Value = 0x100000 },
            new FlagDef { Label = "Profeta", Value = 0x200000 },
            new FlagDef { Label = "Mistico", Value = 0x400000 },
            new FlagDef { Label = "A.M.", Value = 0x800000 },
            new FlagDef { Label = "D.M.", Value = 0x1000000 },
            new FlagDef { Label = "Maquinista (200 0000)", Value = 0x2000000 },
            new FlagDef { Label = "Maquinista (400 0000)", Value = 0x4000000 },
            new FlagDef { Label = "Agressor", Value = 0x8000000 },
            new FlagDef { Label = "Demolidor", Value = 0x10000000 },
            new FlagDef { Label = "Prime", Value = 0x20000000 },
            new FlagDef { Label = "Optimus", Value = 0x40000000 },
            new FlagDef { Label = "Cavaleiro da Morte", Value = 0x100000000 },
            new FlagDef { Label = "Cavaleiro Real", Value = 0x200000000 },
            new FlagDef { Label = "Mercenario", Value = 0x400000000 },
            new FlagDef { Label = "Ninja", Value = 0x800000000 },
            new FlagDef { Label = "Mensajeiro Divino", Value = 0x1000000000 },
            new FlagDef { Label = "Xama", Value = 0x2000000000 },
            new FlagDef { Label = "Arcano", Value = 0x4000000000 },
            new FlagDef { Label = "Emissario do Mortos", Value = 0x8000000000 },
            new FlagDef { Label = "Destruidor", Value = 0x10000000000 },
            new FlagDef { Label = "Cavaleiro Sagrado", Value = 0x20000000000 },
            new FlagDef { Label = "Predador", Value = 0x40000000000 },
            new FlagDef { Label = "Shinobi", Value = 0x80000000000 },
            new FlagDef { Label = "Arcanjo", Value = 0x100000000000 },
            new FlagDef { Label = "Druida", Value = 0x200000000000 },
            new FlagDef { Label = "Bruxo", Value = 0x400000000000 },
            new FlagDef { Label = "Shinigami", Value = 0x800000000000 },
            new FlagDef { Label = "Megatron", Value = 0x1000000000000 },
            new FlagDef { Label = "Galvatron", Value = 0x2000000000000 },
            new FlagDef { Label = "Omega", Value = 0x4000000000000 },
            new FlagDef { Label = "Tita Celeste", Value = 0x8000000000000 },
            new FlagDef { Label = "Nomade (10 0000 0000 0000)", Value = 0x10000000000000 },
            new FlagDef { Label = "Nomade (20 0000 0000 0000)", Value = 0x20000000000000 },
            new FlagDef { Label = "Espadachim", Value = 0x40000000000000 },
            new FlagDef { Label = "Ilusionista", Value = 0x80000000000000 },
            new FlagDef { Label = "Samurai", Value = 0x100000000000000 },
            new FlagDef { Label = "Augure", Value = 0x200000000000000 },
            new FlagDef { Label = "Ronin", Value = 0x400000000000000 },
            new FlagDef { Label = "Oraculo", Value = 0x800000000000000 },
            new FlagDef { Label = "Mestre Dimensional", Value = 0x1000000000000000 },
            new FlagDef { Label = "Cronos", Value = 0x2000000000000000 },
        };

        // dicionario de famas

        private readonly Dictionary<int, string> RESTRICT_ALIGN = new()
        {
  
             { 1, "KASLOW" },
             { 2, "JALE" },
             { 3, "ILYA" },
             { 4, "ELSALAND" },
             { 200, "SANTA KASLOW" },
             { 201, "VINGANÇA DE ILYA" },
             { 202, "MERCENÁRIOS DE JALE" },
             { 203, "ASSOCIAÇÃO DE GAS KASLOW" },
             { 204, "ASSOCIAÇÃO DE ARTE JALE" },
             { 5, "QUATRO MARES" },
             { 6, "COCO VERMELHO" },
             { 7, "ANGONIELA" },
             { 11, "LIVROS DE QUILL" },
             { 20, "GUARDIÃO DE SHAPAEL" },
             { 12, "MINERAÇÃO JALE" },
             { 13, "COLETA ILYA" },
             { 14, "CAÇA KASLOW" },
             { 15, "CAÇADORES DE DEMÔNIOS" },
             { 17, "SPRITE SOMBRIO" },
             { 21, "MENSAGEIRO SPRITE" },
             { 16, "PVP" },
             { 18, "GVG" },
             { 19, "CLUBE PK (CHANNEL PVP)" },
             { 100, "CLASSE" },
             { 22, "BODOR" },
             { 23, "ALICE" },
             { 24, "RONTO" },
             { 25, "SMULCA" },
             { 26, "EWAN" },
             { 27, "BAHADO" },
             { 28, "QUILL" },
             { 29, "MOSUNK" },
             { 30, "JUNDO" },
             { 31, "SIROPAS" },
             { 32, "CONGELADO = ILYANA" },
             { 33, "GINNY" },
        };

        //dicionario de sexo 

        private readonly Dictionary<int, string> RESTRICT_GENDER = new()
        {
            { 1, "Masculino" },
            { 2, "Feminino" },
        };
    }
}