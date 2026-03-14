using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.ComponentModel;
using System.Collections;

namespace GrandFantasiaINIEditor.Modules.Mission
{
    public partial class MissionView : UserControl
    {


        private readonly string clientPath;
        private MissionDb db;
        private readonly ObservableCollection<MissionEntry> Missions = new();
        private readonly ObservableCollection<MissionConditionView> AcceptConditionItems = new();

        private readonly ObservableCollection<MissionConditionView> FinishConditionItems = new();

        private readonly ObservableCollection<MissionConditionView> RewardItems = new();

        private readonly Dictionary<string, string> _npcNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _itemNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _monsterNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _nodeNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _titleNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _enchantNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _spellNames = new(StringComparer.OrdinalIgnoreCase);

        private readonly List<(ulong value, CheckBox cb)> _missionFlagChecks = new();

        private bool _loading;
        private bool _suppressSelectionChanged;

        private List<string> _currentRow;
        private string _lastSelectedMissionId;
        private List<string> _originalRowSnapshot;
        private string _originalNameSnapshot;
        private string _originalClassificationSnapshot;

        private const int IDX_MISSION_ID = 0;
        private const int IDX_NAME = 1;
        private const int IDX_DECLINE_LEVEL = 2;
        private const int IDX_TYPE = 3;
        private const int IDX_FLAG = 4;
        private const int IDX_TRIGGER_LEVEL = 5;
        private const int IDX_TRIGGER_NODE_ID = 6;
        private const int IDX_CLASSIFICATION = 7;
        private const int IDX_ACCEPT_NPC = 8;
        private const int IDX_ACCEPT_ITEM = 9;
        private const int IDX_ACCEPT_CONDITIONS = 10;
        private const int IDX_ACCEPT_DIALOG_ID = 11;
        private const int IDX_ACCEPT_RAW_CMDS = 12;
        private const int IDX_FINISH_CONDITIONS = 13;
        private const int IDX_FINISH_NPC = 14;
        private const int IDX_FINISH_ITEM = 15;
        private const int IDX_FINISH_DIALOG_ID = 16;
        private const int IDX_REWARDS = 17;
        private const int IDX_COMPLETE_DIALOG_ID = 18;
        private const int IDX_RECYCLE_ITEMS = 19;
        private const int IDX_NOTE = 20;


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
            LoadSimpleNameLookup(Path.Combine(clientPath, "data", "translate", "T_Title.ini"), _titleNames, 0, 1, Encoding.GetEncoding(1252));
            LoadSimpleNameLookup(Path.Combine(clientPath, "data", "translate", "T_Enchant.ini"), _enchantNames, 0, 1, Encoding.GetEncoding(1252));
            LoadSimpleNameLookup(Path.Combine(clientPath, "data", "translate", "T_Spell.ini"), _spellNames, 0, 1, Encoding.GetEncoding(1252));
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


        private string GetTitleName(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return $"Título {id}";

            return _titleNames.TryGetValue(id.Trim(), out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : $"Título {id}";
        }

        private string GetEnchantName(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return $"Efeito {id}";

            return _enchantNames.TryGetValue(id.Trim(), out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : $"Efeito {id}";
        }

        private string GetSpellName(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return $"Habilidade {id}";

            return _spellNames.TryGetValue(id.Trim(), out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : $"Habilidade {id}";
        }

        private string FormatGoldReward(string value)
        {
            if (!long.TryParse(value, out var amount) || amount < 0)
                return value;

            string s = amount.ToString(CultureInfo.InvariantCulture).PadLeft(5, '0');

            string bronze = s[^2..];
            string silver = s.Length >= 4 ? s[^4..^2] : "00";
            string gold = s.Length > 4 ? s[..^4] : "0";

            if (!int.TryParse(gold, out var g)) g = 0;
            if (!int.TryParse(silver, out var p)) p = 0;
            if (!int.TryParse(bronze, out var b)) b = 0;

            return $"G:{g} P:{p:D2} B:{b:D2}";
        }

        private List<MissionConditionView> ParseRewards(string raw)
        {
            var result = new List<MissionConditionView>();

            if (string.IsNullOrEmpty(raw))
                return result;

            string cleaned = raw.Trim();

            var parts = cleaned.Split(new[] { ':' }, StringSplitOptions.None);

            foreach (var part in parts)
            {
                string entry = (part ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(entry))
                {
                    result.Add(new MissionConditionView
                    {
                        Raw = string.Empty,
                        EditableText = string.Empty,
                        Description = string.Empty
                    });
                    continue;
                }

                result.Add(ParseSingleReward(entry));
            }

            return result;
        }

        private MissionConditionView ParseRewardExp(string reward)
        {
            var match = Regex.Match(reward, @"^RewardExp\s+(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string exp = match.Groups[1].Value;

                return new MissionConditionView
                {
                    Raw = reward,
                    EditableText = $"RewardExp {exp}",
                    Description = $"Receba {exp} de experiência"
                };
            }

            return new MissionConditionView
            {
                Raw = reward,
                EditableText = reward,
                Description = "Recompensa de experiência"
            };
        }

        private MissionConditionView ParseRewardGold(string reward)
        {
            var match = Regex.Match(reward, @"^RewardGold\s+(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string rawGold = match.Groups[1].Value;
                string formatted = FormatGoldReward(rawGold);

                return new MissionConditionView
                {
                    Raw = reward,
                    EditableText = $"RewardGold {rawGold}",
                    Description = $"Receba {formatted}"
                };
            }

            return new MissionConditionView
            {
                Raw = reward,
                EditableText = reward,
                Description = "Recompensa de gold"
            };
        }

        private MissionConditionView ParseRewardReputation(string reward)
        {
            var match = Regex.Match(reward, @"^RewardReputation\s+(\d+)\s*\+\s*(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var repId))
            {
                string amount = match.Groups[2].Value;
                string repName = REPUTATION_DICT.TryGetValue(repId, out var label)
                    ? label
                    : $"Fama {repId}";

                return new MissionConditionView
                {
                    Raw = reward,
                    EditableText = $"RewardReputation {repId} + {amount}",
                    Description = $"Receba {amount} de reputação {repName} ({repId})"
                };
            }

            return new MissionConditionView
            {
                Raw = reward,
                EditableText = reward,
                Description = "Recompensa de reputação"
            };
        }

        private MissionConditionView ParseRewardItem(string reward)
        {
            var match = Regex.Match(reward, @"^RewardItem\s+(\d+)\s+(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string itemId = match.Groups[1].Value;
                string amount = match.Groups[2].Value;
                string itemName = GetItemName(itemId);

                return new MissionConditionView
                {
                    Raw = reward,
                    EditableText = $"RewardItem {itemId} {amount}",
                    Description = $"Receba o item {itemName} ({itemId}) x{amount}"
                };
            }

            return new MissionConditionView
            {
                Raw = reward,
                EditableText = reward,
                Description = "Recompensa de item"
            };
        }

        private MissionConditionView ParseRewardChooseItem(string reward)
        {
            var match = Regex.Match(reward, @"^RewardChooseItem\s+(\d+)\s+(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string itemId = match.Groups[1].Value;
                string amount = match.Groups[2].Value;
                string itemName = GetItemName(itemId);

                return new MissionConditionView
                {
                    Raw = reward,
                    EditableText = $"RewardChooseItem {itemId} {amount}",
                    Description = $"Escolha receber o item {itemName} ({itemId}) x{amount}"
                };
            }

            return new MissionConditionView
            {
                Raw = reward,
                EditableText = reward,
                Description = "Escolha de item"
            };
        }

        private MissionConditionView ParseRewardTitle(string reward)
        {
            var match = Regex.Match(reward, @"^MC\s+add_appellation\s+(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string titleId = match.Groups[1].Value;
                string titleName = GetTitleName(titleId);

                return new MissionConditionView
                {
                    Raw = reward,
                    EditableText = $"MC add_appellation {titleId}",
                    Description = $"Receba o título {titleName} ({titleId})"
                };
            }

            return new MissionConditionView
            {
                Raw = reward,
                EditableText = reward,
                Description = "Receber título"
            };
        }

        private MissionConditionView ParseRewardBuff(string reward)
        {
            var match = Regex.Match(reward, @"^RewardBuff\s+(\d+)\s+(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string buffId = match.Groups[1].Value;
                string amount = match.Groups[2].Value;
                string buffName = GetEnchantName(buffId);

                return new MissionConditionView
                {
                    Raw = reward,
                    EditableText = $"RewardBuff {buffId} {amount}",
                    Description = $"Receba o efeito {buffName} ({buffId}) x{amount}"
                };
            }

            return new MissionConditionView
            {
                Raw = reward,
                EditableText = reward,
                Description = "Receber efeito"
            };
        }

        private MissionConditionView ParseRewardTalentSlot(string reward)
        {
            var match = Regex.Match(reward, @"^MC\s+set_spell_card_attr\b.*$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return new MissionConditionView
                {
                    Raw = reward,
                    EditableText = reward,
                    Description = "Desbloquear slot de talento"
                };
            }

            return new MissionConditionView
            {
                Raw = reward,
                EditableText = reward,
                Description = "Alteração de slot de talento"
            };
        }

        private MissionConditionView ParseRewardChangeClass(string reward)
        {
            var match = Regex.Match(reward, @"^MC\s+change_class\s+(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var classId))
            {
                string className = MISSION_CLASS.TryGetValue(classId, out var label)
                    ? label
                    : $"Classe {classId}";

                return new MissionConditionView
                {
                    Raw = reward,
                    EditableText = $"MC change_class {classId}",
                    Description = $"Mude de classe para {className} ({classId})"
                };
            }

            return new MissionConditionView
            {
                Raw = reward,
                EditableText = reward,
                Description = "Mudança de classe"
            };
        }

        private MissionConditionView ParseRewardElfLevel(string reward)
        {
            var match = Regex.Match(reward, @"^MC\s+set_elf_level\s+(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string level = match.Groups[1].Value;

                return new MissionConditionView
                {
                    Raw = reward,
                    EditableText = $"MC set_elf_level {level}",
                    Description = $"Evolua o sprite para {level}"
                };
            }

            return new MissionConditionView
            {
                Raw = reward,
                EditableText = reward,
                Description = "Evolução de sprite"
            };
        }

        private MissionConditionView ParseRewardGetSpell(string reward)
        {
            var match = Regex.Match(reward, @"^MC\s+get_spell\s+(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string spellId = match.Groups[1].Value;
                string spellName = GetSpellName(spellId);

                return new MissionConditionView
                {
                    Raw = reward,
                    EditableText = $"MC get_spell {spellId}",
                    Description = $"Aprenda a habilidade {spellName} ({spellId})"
                };
            }

            return new MissionConditionView
            {
                Raw = reward,
                EditableText = reward,
                Description = "Aprender habilidade"
            };
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
                bool startsWithId =
                    firstPipe > 0 &&
                    int.TryParse(line.Substring(0, firstPipe).Trim(), out _);

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
        private MissionConditionView ParseRewardSetBot(string reward)
        {
            var match = Regex.Match(reward, @"^MC\s+set_bot\s+([01])$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string value = match.Groups[1].Value;
                string state = value == "1" ? "ligar" : "desligar";

                return new MissionConditionView
                {
                    Raw = reward,
                    EditableText = $"MC set_bot {value}",
                    Description = $"Assistente sprite (bot): {state}"
                };
            }

            return new MissionConditionView
            {
                Raw = reward,
                EditableText = reward,
                Description = "Assistente sprite"
            };
        }

        private MissionConditionView ParseRewardBonus(string reward)
        {
            var match = Regex.Match(reward, @"^RewardBonus\s+(\d+)\s+(\d+)\s+(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string goldRaw = match.Groups[1].Value;
                string formatted = FormatGoldReward(goldRaw);

                return new MissionConditionView
                {
                    Raw = reward,
                    EditableText = $"RewardBonus {match.Groups[1].Value} {match.Groups[2].Value} {match.Groups[3].Value}",
                    Description = $"Receba bônus {formatted}"
                };
            }

            return new MissionConditionView
            {
                Raw = reward,
                EditableText = reward,
                Description = "Recompensa bônus"
            };
        }

        private MissionConditionView ParseRewardFamilyExp(string reward)
        {
            var match = Regex.Match(reward, @"^RewardFamilyExp\s+(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string amount = match.Groups[1].Value;

                return new MissionConditionView
                {
                    Raw = reward,
                    EditableText = $"RewardFamilyExp {amount}",
                    Description = $"Ganhe experiência para a guild {amount}"
                };
            }

            return new MissionConditionView
            {
                Raw = reward,
                EditableText = reward,
                Description = "Experiência de guild"
            };
        }

        private MissionConditionView ParseSingleReward(string reward)
        {
            if (reward.StartsWith("RewardExp", StringComparison.OrdinalIgnoreCase))
                return ParseRewardExp(reward);

            if (reward.StartsWith("RewardGold", StringComparison.OrdinalIgnoreCase))
                return ParseRewardGold(reward);

            if (reward.StartsWith("RewardReputation", StringComparison.OrdinalIgnoreCase))
                return ParseRewardReputation(reward);

            if (reward.StartsWith("RewardItem", StringComparison.OrdinalIgnoreCase))
                return ParseRewardItem(reward);

            if (reward.StartsWith("RewardChooseItem", StringComparison.OrdinalIgnoreCase))
                return ParseRewardChooseItem(reward);

            if (Regex.IsMatch(reward, @"^MC\s+add_appellation\b", RegexOptions.IgnoreCase))
                return ParseRewardTitle(reward);

            if (reward.StartsWith("RewardBuff", StringComparison.OrdinalIgnoreCase))
                return ParseRewardBuff(reward);

            if (Regex.IsMatch(reward, @"^MC\s+set_spell_card_attr\b", RegexOptions.IgnoreCase))
                return ParseRewardTalentSlot(reward);

            if (Regex.IsMatch(reward, @"^MC\s+change_class\b", RegexOptions.IgnoreCase))
                return ParseRewardChangeClass(reward);

            if (Regex.IsMatch(reward, @"^MC\s+set_elf_level\b", RegexOptions.IgnoreCase))
                return ParseRewardElfLevel(reward);

            if (Regex.IsMatch(reward, @"^MC\s+get_spell\b", RegexOptions.IgnoreCase))
                return ParseRewardGetSpell(reward);

            if (Regex.IsMatch(reward, @"^MC\s+set_bot\b", RegexOptions.IgnoreCase))
                return ParseRewardSetBot(reward);

            if (reward.StartsWith("RewardBonus", StringComparison.OrdinalIgnoreCase))
                return ParseRewardBonus(reward);

            if (reward.StartsWith("RewardFamilyExp", StringComparison.OrdinalIgnoreCase))
                return ParseRewardFamilyExp(reward);

            return new MissionConditionView
            {
                Raw = reward,
                EditableText = reward,
                Description = "Reward não mapeado"
            };
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
                return $"Nó {id}";

            return _nodeNames.TryGetValue(id.Trim(), out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : $"Nó {id}";
        }

        public MissionView(string clientPath)
        {
            InitializeComponent();

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            this.clientPath = clientPath;

            MissionList.ItemsSource = Missions;
            AcceptConditionsList.ItemsSource = AcceptConditionItems;
            FinishConditionsList.ItemsSource = FinishConditionItems;
            RewardsList.ItemsSource = RewardItems;

            LoadTranslationLookups();

            PopulateCombos();
            InitFlagsUi();
            HookFlagEvents();
            LoadDatabase();
        }

        private sealed class MissionEntry
        {
            public string Id { get; set; }
            public string Name { get; set; }

            public override string ToString() => $"{Id} - {Name}";
        }

        private sealed class MissionTranslation
        {
            public string Name;
            public string Description;
            public string Classification;

        }

        private sealed class MissionDb
        {
            public Dictionary<string, List<string>> SRows = new();
            public Dictionary<string, MissionTranslation> TRows = new();
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

        private void LoadRewardsFromRaw(string raw)
        {
            RewardItems.Clear();

            foreach (var item in ParseRewards(raw))
                RewardItems.Add(item);
        }

        private string BuildRewardsRaw()
        {
            if (RewardItems.Count == 0)
                return string.Empty;

            var parts = RewardItems
                .Select(x => (x.EditableText ?? string.Empty).Trim())
                .ToList();

            return string.Join(":", parts);
        }

        private void RewardEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading)
                return;

            if (sender is not TextBox tb || tb.Tag is not MissionConditionView item)
                return;

            item.EditableText = tb.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(item.EditableText))
            {
                item.Description = string.Empty;
                return;
            }

            var parsed = ParseSingleReward(item.EditableText.Trim());
            item.Description = parsed.Description;
        }

        private void LoadFinishConditionsFromRaw(string raw)
        {
            FinishConditionItems.Clear();

            foreach (var item in ParseFinishConditions(raw))
                FinishConditionItems.Add(item);
        }

        private string BuildFinishConditionsRaw()
        {
            if (FinishConditionItems.Count == 0)
                return string.Empty;

            var parts = FinishConditionItems
                .Select(x => (x.EditableText ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            return parts.Count == 0
                ? string.Empty
                : "#:" + string.Join(":", parts);
        }

        private void FinishConditionEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading)
                return;

            if (sender is not TextBox tb || tb.Tag is not MissionConditionView item)
                return;

            item.EditableText = tb.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(item.EditableText))
            {
                item.Description = string.Empty;
                return;
            }

            var parsed = ParseSingleFinishCondition(item.EditableText.Trim());
            item.Description = parsed.Description;
        }

        private List<MissionConditionView> ParseFinishConditions(string raw)
        {
            var result = new List<MissionConditionView>();

            if (string.IsNullOrWhiteSpace(raw))
                return result;

            string cleaned = raw.Trim();

            if (cleaned.StartsWith("#:"))
                cleaned = cleaned.Substring(2);

            var parts = cleaned.Split(new[] { ':' }, StringSplitOptions.None);

            foreach (var part in parts)
            {
                string entry = (part ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(entry))
                {
                    result.Add(new MissionConditionView
                    {
                        Raw = string.Empty,
                        EditableText = string.Empty,
                        Description = string.Empty
                    });
                    continue;
                }

                result.Add(ParseSingleFinishCondition(entry));
            }

            return result;
        }


        private MissionConditionView ParseSingleFinishCondition(string condition)
        {
            if (condition.StartsWith("NPCItem", StringComparison.OrdinalIgnoreCase))
                return ParseFinishNpcItemCondition(condition);

            if (condition.StartsWith("NPCTalk", StringComparison.OrdinalIgnoreCase))
                return ParseFinishNpcTalkCondition(condition);

            if (condition.StartsWith("ItemCollect", StringComparison.OrdinalIgnoreCase))
                return ParseFinishItemCollectCondition(condition);

            if (condition.StartsWith("MonsterKilled", StringComparison.OrdinalIgnoreCase))
                return ParseFinishMonsterKilledCondition(condition);

            if (condition.StartsWith("ItemGiven", StringComparison.OrdinalIgnoreCase))
                return ParseFinishItemGivenCondition(condition);

            if (condition.StartsWith("ItemGot", StringComparison.OrdinalIgnoreCase))
                return ParseFinishItemGotCondition(condition);

            if (condition.StartsWith("MonsterTreasureGot", StringComparison.OrdinalIgnoreCase))
                return ParseFinishMonsterTreasureGotCondition(condition);

            if (condition.StartsWith("CheckBuff", StringComparison.OrdinalIgnoreCase))
                return ParseFinishCheckBuffCondition(condition);

            if (condition.StartsWith("AreaTravel", StringComparison.OrdinalIgnoreCase))
                return ParseFinishAreaTravelCondition(condition);

            return new MissionConditionView
            {
                Raw = condition,
                EditableText = condition,
                Description = "Condição de conclusão não mapeada"
            };
        }

        private MissionConditionView ParseFinishNpcItemCondition(string condition)
        {
            var match = Regex.Match(condition, @"^NPCItem\s+(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string itemId = match.Groups[1].Value;
                string itemName = GetItemName(itemId);

                return new MissionConditionView
                {
                    Raw = condition,
                    EditableText = $"NPCItem {itemId}",
                    Description = $"Entregue ao NPC de conclusão o item {itemName} ({itemId})"
                };
            }

            return new MissionConditionView
            {
                Raw = condition,
                EditableText = condition,
                Description = "Entregar item ao NPC de conclusão"
            };
        }

        private MissionConditionView ParseFinishItemGivenCondition(string condition)
        {
            var match = Regex.Match(condition, @"^ItemGiven\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string npcId = match.Groups[1].Value;
                string dialogId = match.Groups[2].Value;
                string itemId = match.Groups[3].Value;
                string amount = match.Groups[4].Value;

                string npcName = GetNpcName(npcId);
                string itemName = GetItemName(itemId);

                return new MissionConditionView
                {
                    Raw = condition,
                    EditableText = $"ItemGiven {npcId} {dialogId} {itemId} {amount}",
                    Description = $"Entregue o item {itemName} ({itemId}) x{amount} ao NPC {npcName} ({npcId}) no diálogo ({dialogId})"
                };
            }

            return new MissionConditionView
            {
                Raw = condition,
                EditableText = condition,
                Description = "Entregar item ao NPC"
            };
        }
        private MissionConditionView ParseFinishNpcTalkCondition(string condition)
        {
            var emptyMatch = Regex.Match(condition, @"^NPCTalk\s*$", RegexOptions.IgnoreCase);
            if (emptyMatch.Success)
            {
                return new MissionConditionView
                {
                    Raw = condition,
                    EditableText = "NPCTalk",
                    Description = "Fale com o NPC de conclusão"
                };
            }

            var match = Regex.Match(condition, @"^NPCTalk\s+(\d+)\s+(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string npcId = match.Groups[1].Value;
                string dialogId = match.Groups[2].Value;
                string npcName = GetNpcName(npcId);

                return new MissionConditionView
                {
                    Raw = condition,
                    EditableText = $"NPCTalk {npcId} {dialogId}",
                    Description = $"Fale com o NPC {npcName} ({npcId}) e conclua o diálogo ({dialogId})"
                };
            }

            return new MissionConditionView
            {
                Raw = condition,
                EditableText = condition,
                Description = "Falar com NPC"
            };
        }

        private MissionConditionView ParseFinishItemCollectCondition(string condition)
        {
            var match = Regex.Match(condition, @"^ItemCollect\s+(\d+)\s+(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string itemId = match.Groups[1].Value;
                string amount = match.Groups[2].Value;
                string itemName = GetItemName(itemId);

                return new MissionConditionView
                {
                    Raw = condition,
                    EditableText = $"ItemCollect {itemId} {amount}",
                    Description = $"Colete o item {itemName} ({itemId}) x{amount}"
                };
            }

            return new MissionConditionView
            {
                Raw = condition,
                EditableText = condition,
                Description = "Coleta de item"
            };
        }

        private MissionConditionView ParseFinishMonsterKilledCondition(string condition)
        {
            var match = Regex.Match(condition, @"^MonsterKilled\s+(\d+)\s+(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string monsterId = match.Groups[1].Value;
                string amount = match.Groups[2].Value;
                string monsterName = GetMonsterName(monsterId);

                return new MissionConditionView
                {
                    Raw = condition,
                    EditableText = $"MonsterKilled {monsterId} {amount}",
                    Description = $"Mate o monstro {monsterName} ({monsterId}) x{amount}"
                };
            }

            return new MissionConditionView
            {
                Raw = condition,
                EditableText = condition,
                Description = "Abater monstro"
            };
        }

        private MissionConditionView ParseFinishItemGotCondition(string condition)
        {
            var match = Regex.Match(condition, @"^ItemGot\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string npcId = match.Groups[1].Value;
                string dialogId = match.Groups[2].Value;
                string itemId = match.Groups[3].Value;
                string amount = match.Groups[4].Value;

                string npcName = GetNpcName(npcId);
                string itemName = GetItemName(itemId);

                return new MissionConditionView
                {
                    Raw = condition,
                    EditableText = $"ItemGot {npcId} {dialogId} {itemId} {amount}",
                    Description = $"Obtenha o item {itemName} ({itemId}) x{amount} com o NPC {npcName} ({npcId}) no diálogo ({dialogId})"
                };
            }

            return new MissionConditionView
            {
                Raw = condition,
                EditableText = condition,
                Description = "Receber item de NPC"
            };
        }

        private MissionConditionView ParseFinishMonsterTreasureGotCondition(string condition)
        {
            var match = Regex.Match(condition, @"^MonsterTreasureGot\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string monsterId = match.Groups[1].Value;
                string dropChance = match.Groups[2].Value;
                string itemId = match.Groups[3].Value;
                string amount = match.Groups[4].Value;

                string monsterName = GetMonsterName(monsterId);
                string itemName = GetItemName(itemId);

                return new MissionConditionView
                {
                    Raw = condition,
                    EditableText = $"MonsterTreasureGot {monsterId} {dropChance} {itemId} {amount}",
                    Description = $"Colete o item {itemName} ({itemId}) x{amount} do monstro {monsterName} ({monsterId}) com chance {dropChance}%"
                };
            }

            return new MissionConditionView
            {
                Raw = condition,
                EditableText = condition,
                Description = "Drop de item de monstro"
            };
        }

        private MissionConditionView ParseFinishCheckBuffCondition(string condition)
        {
            var match = Regex.Match(condition, @"^CheckBuff\s+(\d+)\s+(\d+)\s+(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string buffId = match.Groups[1].Value;
                int mode = int.Parse(match.Groups[2].Value);
                string value = match.Groups[3].Value;

                string description = mode switch
                {
                    1 when value == "1" => $"Necessário possuir o buff {buffId}",
                    1 => $"Necessário buff {buffId} maior ou igual a {value}",
                    2 => $"Necessário buff {buffId} menor ou igual a {value}",
                    3 when value == "0" => $"Bloqueado enquanto buff {buffId} estiver ativo",
                    3 => $"Bloqueado enquanto buff {buffId} for maior ou igual a {value}",
                    4 => $"Bloqueado enquanto buff {buffId} for menor ou igual a {value}",
                    _ => $"Condição de buff {buffId} (modo {mode}, valor {value})"
                };

                return new MissionConditionView
                {
                    Raw = condition,
                    EditableText = $"CheckBuff {buffId} {mode} {value}",
                    Description = description
                };
            }

            return new MissionConditionView
            {
                Raw = condition,
                EditableText = condition,
                Description = "Condição de buff"
            };
        }

        private MissionConditionView ParseFinishAreaTravelCondition(string condition)
        {
            var match = Regex.Match(condition, @"^AreaTravel\s+(\d+)\s+(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string nodeId = match.Groups[1].Value;
                string areaId = match.Groups[2].Value;
                string nodeName = GetNodeName(nodeId);

                return new MissionConditionView
                {
                    Raw = condition,
                    EditableText = $"AreaTravel {nodeId} {areaId}",
                    Description = $"Passe pela área {areaId} no nó {nodeName} ({nodeId})"
                };
            }

            return new MissionConditionView
            {
                Raw = condition,
                EditableText = condition,
                Description = "Passar por área"
            };
        }

        private void LoadAcceptConditionsFromRaw(string raw)
        {
            AcceptConditionItems.Clear();

            foreach (var item in ParseAcceptConditions(raw))
                AcceptConditionItems.Add(item);
        }

        private string BuildAcceptConditionsRaw()
        {
            if (AcceptConditionItems.Count == 0)
                return string.Empty;

            var parts = AcceptConditionItems
                .Select(x => (x.EditableText ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            return parts.Count == 0
                ? string.Empty
                : "#:" + string.Join(":", parts);
        }

        private void AcceptConditionEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading)
                return;

            if (sender is not TextBox tb || tb.Tag is not MissionConditionView item)
                return;

            item.EditableText = tb.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(item.EditableText))
            {
                item.Description = string.Empty;
                return;
            }

            var parsed = ParseSingleCondition(item.EditableText.Trim());
            item.Description = parsed.Description;
        }

        private sealed class MissionConditionView : INotifyPropertyChanged
        {
            private string _editableText;
            private string _description;

            public string Raw { get; set; }

            public string EditableText
            {
                get => _editableText;
                set
                {
                    if (_editableText == value)
                        return;

                    _editableText = value;
                    OnPropertyChanged(nameof(EditableText));
                }
            }

            public string Description
            {
                get => _description;
                set
                {
                    if (_description == value)
                        return;

                    _description = value;
                    OnPropertyChanged(nameof(Description));
                    OnPropertyChanged(nameof(LegendText));
                }
            }

            public string LegendText => string.IsNullOrWhiteSpace(Description)
                ? string.Empty
                : $"[ {Description} ]";

            public event PropertyChangedEventHandler PropertyChanged;

            private void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private readonly Dictionary<int, string> MISSION_TYPE = new()
        {
            { 1, "Personal" },
            { 2, "Emergent" },
            { 3, "Class" },
            { 4, "Team" },
            { 5, "Guild" },
            { 6, "Pvp" },
        };

        private readonly List<FlagDef> MISSION_FLAGS = new()
        {
            new FlagDef { Label = "NoAbandon", Value = 1 },
            new FlagDef { Label = "NoShare", Value = 2 },
            new FlagDef { Label = "Reaccept", Value = 4 },
            new FlagDef { Label = "AutoAccept", Value = 8 },
            new FlagDef { Label = "NoRebirthReset", Value = 16 },
            new FlagDef { Label = "DailyReset", Value = 32 },
            new FlagDef { Label = "WeeklyReset", Value = 64 },
            new FlagDef { Label = "MonthlyReset", Value = 128 },
        };

        private readonly Dictionary<int, string> REPUTATION_DICT = new()
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

        private readonly Dictionary<int, string> MISSION_CLASS = new()
        {
            { 1, "LUTADOR" },
            { 2, "GUERREIRO" },
            { 3, "BERSEKER" },
            { 4, "PALADINO" },

            { 5, "CAÇADOR" },
            { 6, "ARQUEIRO" },
            { 7, "RANGER" },
            { 8, "ASSASSINO" },

            { 9, "ACÓLITO" },
            { 10, "SACERDOTE" },
            { 11, "CLÉRIGO" },
            { 12, "SÁBIO" },

            { 13, "BRUXO" },
            { 14, "MÁGO" },
            { 15, "FEITICEIRO" },
            { 16, "NECROMANTE" },

            { 17, "TITÃ" },
            { 18, "TEMPLÁRIO" },
            { 19, "FRANC ATIRADOR" },
            { 20, "SIRICÁRIO SOMBRIO" },
            { 21, "PROFETA" },
            { 22, "MÍSTICO" },
            { 23, "ARQUIMAGO" },
            { 24, "DEMÓLOGO" },

            { 25, "MAQUINISTA APRENDIZ" },
            { 26, "MAQUINISTA" },
            { 27, "AGRESSOR" },
            { 28, "DEMOLIDOR" },
            { 29, "PRIME" },
            { 30, "OPTIMUS" },

            { 32, "CAVALEIRO DA MORTE" },
            { 33, "CAVALEIRO REAL" },
            { 34, "MERCENÁRIO" },
            { 35, "NINJA" },
            { 36, "MENSAGEIRO DIVINO" },
            { 37, "XAMÃ" },
            { 38, "ARCANO" },
            { 39, "EMISSÁRIO DOS MORTOS" },
            { 40, "DESTRUIDOR" },
            { 41, "CAVALEIRO SAGRADO" },
            { 42, "PREDADOR" },
            { 43, "SHINOBI" },
            { 44, "ARCANJO" },
            { 45, "DRUIDA" },
            { 46, "BRUXO AVANÇADO" },
            { 47, "SHINIGAMI" },

            { 48, "MEGATRON" },
            { 49, "GALVATRON" },
            { 50, "ÔMEGA" },
            { 51, "TITÃ CELESTE" },

            { 52, "VIAJANTE" },
            { 53, "NÔMADE" },
            { 54, "ESPADACHIM" },
            { 55, "ILUSIONISTA" },
            { 56, "SAMURAI" },
            { 57, "AUGURE" },
            { 58, "RONIN" },
            { 59, "ORÁCULO" },
            { 60, "MESTRE DIMENSIONAL" },
            { 61, "CRONOS" },
        };

        private readonly Dictionary<string, string> CONDITION_OPERATORS = new()
        {
            { ">=", "maior ou igual a" },
            { "<=", "menor ou igual a" },
            { "==", "igual a" },
            { ">", "maior que" },
            { "<", "menor que" },
        };

        private static string GetBoxText(TextBox box)
        {
            return box?.Text?.Trim() ?? string.Empty;
        }

        private static int CompareMissionIds(string a, string b)
        {
            if (int.TryParse(a, out var ai) && int.TryParse(b, out var bi))
                return ai.CompareTo(bi);

            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private void PopulateCombos()
        {
            if (MissionTypeCombo != null)
            {
                MissionTypeCombo.ItemsSource = MISSION_TYPE
                    .Select(kv => new ComboOption { Value = kv.Key, Label = kv.Value })
                    .OrderBy(x => x.Value)
                    .ToList();
            }
        }

        private void InitFlagsUi()
        {
            _missionFlagChecks.Clear();

            if (missionflags_grid != null)
            {
                missionflags_grid.Children.Clear();

                foreach (var def in MISSION_FLAGS)
                {
                    var cb = new CheckBox
                    {
                        Content = $"{def.Label} ({def.Value})",
                        Foreground = Brushes.AliceBlue,
                        Margin = new Thickness(0, 0, 14, 10)
                    };

                    _missionFlagChecks.Add((def.Value, cb));
                    missionflags_grid.Children.Add(cb);
                }
            }
        }

        private void HookFlagEvents()
        {
            foreach (var item in _missionFlagChecks)
            {
                item.cb.Checked += MissionFlagCheckChanged;
                item.cb.Unchecked += MissionFlagCheckChanged;
            }
        }

        private void MissionFlagCheckChanged(object sender, RoutedEventArgs e)
        {
            RecomputeMissionFlags();
        }

        private void RecomputeMissionFlags()
        {
            if (_loading || _currentRow == null)
                return;

            ulong total = 0;

            foreach (var (value, cb) in _missionFlagChecks)
            {
                if (cb.IsChecked == true)
                    total |= value;
            }

            total = PreserveUnknownBits(IDX_FLAG, total, MISSION_FLAGS);

            SetRowValue(IDX_FLAG, FormatBitmaskLikeOriginal(IDX_FLAG, total));
            RefreshFlagDisplays(total);
        }

        private void RefreshFlagDisplays(ulong flagVal)
        {
            if (missionflags_value != null)
                missionflags_value.Text = $"{flagVal} | 0x{flagVal:X}";
        }

        private void SetChecksFromValue(
            List<(ulong value, CheckBox cb)> checks,
            ulong value,
            RoutedEventHandler handlerToAttach)
        {
            foreach (var (flag, cb) in checks)
            {
                cb.Checked -= MissionFlagCheckChanged;
                cb.Unchecked -= MissionFlagCheckChanged;

                cb.IsChecked = (value & flag) == flag;

                cb.Checked += handlerToAttach;
                cb.Unchecked += handlerToAttach;
            }
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
            db = LoadMissionDb(clientPath);
            Missions.Clear();

            foreach (var r in db.SRows.OrderBy(x => x.Key, Comparer<string>.Create(CompareMissionIds)))
            {
                string displayName = GetMissionNameForList(r.Key, r.Value);

                Missions.Add(new MissionEntry
                {
                    Id = r.Key,
                    Name = displayName
                });
            }
        }

        private string GetMissionNameForList(string id, List<string> row)
        {
            if (db.TRows.TryGetValue(id, out var t) && !string.IsNullOrWhiteSpace(t.Name))
                return t.Name;

            return GetValue(row, IDX_NAME);
        }

        private List<string> BuildEditedRowFromControls()
        {
            var row = _currentRow != null
                ? new List<string>(_currentRow)
                : new List<string>();

            SetListValue(row, IDX_DECLINE_LEVEL, GetBoxText(DeclineLevelBox));
            SetListValue(row, IDX_TYPE, GetComboValueOrKeepOriginal(MissionTypeCombo, IDX_TYPE));
            SetListValue(row, IDX_TRIGGER_LEVEL, GetBoxText(TriggerLevelBox));
            SetListValue(row, IDX_TRIGGER_NODE_ID, GetBoxText(TriggerNodeIdBox));
            SetListValue(row, IDX_ACCEPT_NPC, GetBoxText(AcceptNpcBox));
            SetListValue(row, IDX_ACCEPT_ITEM, GetBoxText(AcceptItemBox));
            SetListValue(row, IDX_ACCEPT_CONDITIONS, BuildAcceptConditionsRaw());
            SetListValue(row, IDX_ACCEPT_DIALOG_ID, GetBoxText(AcceptDialogIdBox));
            SetListValue(row, IDX_ACCEPT_RAW_CMDS, GetBoxText(AcceptRawCmdsBox));
            SetListValue(row, IDX_FINISH_CONDITIONS, BuildFinishConditionsRaw());
            SetListValue(row, IDX_FINISH_NPC, GetBoxText(FinishNpcBox));
            SetListValue(row, IDX_FINISH_ITEM, GetBoxText(FinishItemBox));
            SetListValue(row, IDX_FINISH_DIALOG_ID, GetBoxText(FinishDialogIdBox));
            SetListValue(row, IDX_REWARDS, BuildRewardsRaw());
            SetListValue(row, IDX_COMPLETE_DIALOG_ID, GetBoxText(CompleteDialogIdBox));
            SetListValue(row, IDX_RECYCLE_ITEMS, GetBoxText(RecycleItemsBox));
            SetListValue(row, IDX_NOTE, GetBoxText(NoteBox));

            ulong flagValue = 0;
            foreach (var (value, cb) in _missionFlagChecks)
            {
                if (cb.IsChecked == true)
                    flagValue |= value;
            }
            flagValue = PreserveUnknownBits(IDX_FLAG, flagValue, MISSION_FLAGS);
            SetListValue(row, IDX_FLAG, FormatBitmaskLikeOriginal(IDX_FLAG, flagValue));

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

        private void PatchIniRowInPlace(string path, Encoding encoding, string missionId, HashSet<int> changedColumns, List<string> editedRow)
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
                if (!string.Equals(lineId, missionId, StringComparison.Ordinal))
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

            throw new InvalidOperationException($"Mission {missionId} não foi encontrada em {Path.GetFileName(path)}.");
        }

        private void PatchTranslateRowInPlace(string path, Encoding encoding, string missionId, string newName, string newClassification)
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
                if (!string.Equals(lineId, missionId, StringComparison.Ordinal))
                    continue;

                while (parts.Count < 4)
                    parts.Add(string.Empty);

                parts[1] = newName ?? string.Empty;
                parts[3] = newClassification ?? string.Empty;

                lines[i] = string.Join("|", parts);
                File.WriteAllLines(path, lines, encoding);
                return;
            }

            var newLine = string.Join("|", new[]
            {
                missionId ?? string.Empty,
                newName ?? string.Empty,
                string.Empty,
                newClassification ?? string.Empty
            });

            var output = lines.ToList();
            output.Add(newLine);
            File.WriteAllLines(path, output, encoding);
        }

        private bool HasCurrentMissionChanges()
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
            string currentClassification = GetBoxText(ClassificationBox);

            if (!string.Equals(currentName, _originalNameSnapshot ?? string.Empty, StringComparison.Ordinal))
                return true;

            if (!string.Equals(currentClassification, _originalClassificationSnapshot ?? string.Empty, StringComparison.Ordinal))
                return true;

            return false;
        }

        private void FinishItemBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading)
                return;

            SetText(FinishItemLegendBox, GetItemLegendText(GetBoxText(FinishItemBox)));
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

            foreach (var kv in db.SRows.OrderBy(x => x.Key, Comparer<string>.Create(CompareMissionIds)))
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

            foreach (var kv in db.TRows.OrderBy(x => x.Key, Comparer<string>.Create(CompareMissionIds)))
            {
                string id = kv.Key ?? string.Empty;
                string name = kv.Value?.Name ?? string.Empty;
                string classification = kv.Value?.Classification ?? string.Empty;
                lines.Add($"{id}|{name}||{classification}");
            }

            using var sw = new StreamWriter(path, false, encoding);
            foreach (var line in lines)
                sw.WriteLine(line);
        }

        private void SaveCurrentMissionFiles()
        {
            if (_currentRow == null)
                return;

            string currentId = GetValue(_currentRow, IDX_MISSION_ID);
            if (string.IsNullOrWhiteSpace(currentId))
                return;

            var editedRow = BuildEditedRowFromControls();
            var changedColumns = GetChangedColumnIndices(_originalRowSnapshot, editedRow);

            string currentName = GetBoxText(NameBox);
            string currentClassification = GetBoxText(ClassificationBox);

            bool translateChanged =
                !string.Equals(currentName, _originalNameSnapshot ?? string.Empty, StringComparison.Ordinal) ||
                !string.Equals(currentClassification, _originalClassificationSnapshot ?? string.Empty, StringComparison.Ordinal);

            string sMissionPath = Path.Combine(clientPath, "data", "db", "S_Mission.ini");
            string cMissionPath = Path.Combine(clientPath, "data", "db", "C_Mission.ini");
            string tMissionPath = Path.Combine(clientPath, "data", "translate", "T_Mission.ini");

            PatchIniRowInPlace(sMissionPath, Encoding.GetEncoding(950), currentId, changedColumns, editedRow);

            if (File.Exists(cMissionPath))
                PatchIniRowInPlace(cMissionPath, Encoding.GetEncoding(950), currentId, changedColumns, editedRow);

            if (translateChanged || !db.TRows.ContainsKey(currentId))
                PatchTranslateRowInPlace(tMissionPath, Encoding.GetEncoding(1252), currentId, currentName, currentClassification);

            _currentRow = editedRow;
            db.SRows[currentId] = new List<string>(editedRow);

            db.TRows[currentId] = new MissionTranslation
            {
                Name = currentName,
                Classification = currentClassification
            };

            _originalRowSnapshot = editedRow.Select(x => x ?? string.Empty).ToList();
            _originalNameSnapshot = currentName;
            _originalClassificationSnapshot = currentClassification;
        }

        private bool ConfirmSaveIfNeeded()
        {
            if (!HasCurrentMissionChanges())
                return true;

            var result = MessageBox.Show(
                "Esta missão foi modificada. Deseja salvar antes de trocar para outra missão?",
                "Salvar alterações",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
                return false;

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    SaveCurrentMissionFiles();
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
        private void AcceptNpcBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading)
                return;

            SetText(AcceptNpcLegendBox, GetNpcLegendText(GetBoxText(AcceptNpcBox)));
        }

        private void FinishNpcBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading)
                return;

            SetText(FinishNpcLegendBox, GetNpcLegendText(GetBoxText(FinishNpcBox)));
        }

        private void AcceptItemBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading)
                return;

            SetText(AcceptItemLegendBox, GetItemLegendText(GetBoxText(AcceptItemBox)));
        }
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (db == null)
                return;

            string filter = (SearchBox.Text ?? string.Empty).Trim().ToLowerInvariant();

            var result = db.SRows
                .Where(x =>
                    string.IsNullOrEmpty(filter) ||
                    x.Key.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    GetMissionNameForList(x.Key, x.Value).Contains(filter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Key, Comparer<string>.Create(CompareMissionIds))
                .Select(x => new MissionEntry
                {
                    Id = x.Key,
                    Name = GetMissionNameForList(x.Key, x.Value)
                })
                .ToList();

            Missions.Clear();
            foreach (var item in result)
                Missions.Add(item);
        }

        private void MissionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionChanged)
                return;

            if (MissionList.SelectedItem is not MissionEntry entry)
                return;

            if (_lastSelectedMissionId != null && _lastSelectedMissionId != entry.Id)
            {
                if (!ConfirmSaveIfNeeded())
                {
                    _suppressSelectionChanged = true;
                    MissionList.SelectedItem = Missions.FirstOrDefault(x => x.Id == _lastSelectedMissionId);
                    _suppressSelectionChanged = false;
                    return;
                }
            }

            if (!db.SRows.TryGetValue(entry.Id, out var rowFromDb))
                return;

            _currentRow = rowFromDb.Select(x => x?.Trim() ?? string.Empty).ToList();
            _loading = true;

            try
            {
                SetText(MissionIdBox, GetValue(_currentRow, IDX_MISSION_ID));
                SetText(DeclineLevelBox, GetValue(_currentRow, IDX_DECLINE_LEVEL));
                SetText(TriggerLevelBox, GetValue(_currentRow, IDX_TRIGGER_LEVEL));
                SetText(TriggerNodeIdBox, GetValue(_currentRow, IDX_TRIGGER_NODE_ID));
                SetText(AcceptNpcBox, GetValue(_currentRow, IDX_ACCEPT_NPC));
                SetText(AcceptItemBox, GetValue(_currentRow, IDX_ACCEPT_ITEM));
                LoadAcceptConditionsFromRaw(GetValue(_currentRow, IDX_ACCEPT_CONDITIONS));
                SetText(AcceptDialogIdBox, GetValue(_currentRow, IDX_ACCEPT_DIALOG_ID));
                SetText(AcceptRawCmdsBox, GetValue(_currentRow, IDX_ACCEPT_RAW_CMDS));
                LoadFinishConditionsFromRaw(GetValue(_currentRow, IDX_FINISH_CONDITIONS));
                SetText(FinishNpcBox, GetValue(_currentRow, IDX_FINISH_NPC));
                SetText(FinishItemBox, GetValue(_currentRow, IDX_FINISH_ITEM));
                SetText(FinishDialogIdBox, GetValue(_currentRow, IDX_FINISH_DIALOG_ID));
                LoadRewardsFromRaw(GetValue(_currentRow, IDX_REWARDS));
                SetText(CompleteDialogIdBox, GetValue(_currentRow, IDX_COMPLETE_DIALOG_ID));
                SetText(RecycleItemsBox, GetValue(_currentRow, IDX_RECYCLE_ITEMS));
                SetText(NoteBox, GetValue(_currentRow, IDX_NOTE));

                if (db.TRows.TryGetValue(entry.Id, out var translation))
                {
                    SetText(NameBox, translation.Name ?? string.Empty);

                    string locationText = !string.IsNullOrWhiteSpace(translation.Description)
                        ? translation.Description
                        : GetValue(_currentRow, IDX_CLASSIFICATION);

                    SetText(ClassificationBox, locationText);
                }
                else
                {
                    SetText(NameBox, GetValue(_currentRow, IDX_NAME));
                    SetText(ClassificationBox, GetValue(_currentRow, IDX_CLASSIFICATION));
                }

                if (int.TryParse(GetValue(_currentRow, IDX_TYPE), out var typeValue))
                    SelectComboByValue(MissionTypeCombo, typeValue);
                else if (MissionTypeCombo != null)
                    MissionTypeCombo.SelectedIndex = -1;

                ulong flagValue = ParseULongSafe(GetValue(_currentRow, IDX_FLAG));
                SetChecksFromValue(_missionFlagChecks, flagValue, MissionFlagCheckChanged);
                RefreshFlagDisplays(flagValue);

                RefreshGeneralLegends();



                _lastSelectedMissionId = entry.Id;
                _originalRowSnapshot = BuildEditedRowFromControls();
                _originalNameSnapshot = GetBoxText(NameBox);
                _originalClassificationSnapshot = GetBoxText(ClassificationBox);
            }

            finally
            {
                _loading = false;
            }
        }

        

        private List<MissionConditionView> ParseAcceptConditions(string raw)
        {
            var result = new List<MissionConditionView>();

            if (string.IsNullOrWhiteSpace(raw))
                return result;

            string cleaned = raw.Trim();

            if (cleaned.StartsWith("#:"))
                cleaned = cleaned.Substring(2);

            var parts = cleaned.Split(new[] { ':' }, StringSplitOptions.None);

            foreach (var part in parts)
            {
                string entry = (part ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(entry))
                {
                    result.Add(new MissionConditionView
                    {
                        Raw = string.Empty,
                        EditableText = string.Empty,
                        Description = string.Empty
                    });
                    continue;
                }

                result.Add(ParseSingleCondition(entry));
            }

            return result;
        }

        private MissionConditionView ParseSingleCondition(string condition)
        {
            if (condition.StartsWith("If_CharLevel", StringComparison.OrdinalIgnoreCase))
                return ParseCharLevelCondition(condition);

            if (condition.StartsWith("If_Reputation", StringComparison.OrdinalIgnoreCase))
                return ParseReputationCondition(condition);

            if (condition.StartsWith("If_FMS", StringComparison.OrdinalIgnoreCase))
                return ParseFmsCondition(condition);

            if (condition.StartsWith("If_Class", StringComparison.OrdinalIgnoreCase))
                return ParseClassCondition(condition);

            if (condition.StartsWith("If_CheckRebirthCount", StringComparison.OrdinalIgnoreCase))
                return ParseRebirthCondition(condition);

            if (condition.StartsWith("If_CheckBuff", StringComparison.OrdinalIgnoreCase))
                return ParseCheckBuffCondition(condition);

            return new MissionConditionView
            {
                Raw = condition,
                EditableText = condition,
                Description = "Condição não mapeada"
            };
        }

        private MissionConditionView ParseCharLevelCondition(string condition)
        {
            var match = Regex.Match(condition, @"If_CharLevel\s*(>=|<=|==|>|<)\s*(\d+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string op = match.Groups[1].Value;
                string val = match.Groups[2].Value;

                return new MissionConditionView
                {
                    Raw = condition,
                    EditableText = $"If_CharLevel {op} {val}",
                    Description = $"Nível do personagem {GetOperatorLabel(op)} {val}"
                };
            }

            return new MissionConditionView
            {
                Raw = condition,
                EditableText = condition,
                Description = "Condição de nível"
            };
        }

        private MissionConditionView ParseReputationCondition(string condition)
        {
            var match = Regex.Match(condition, @"If_Reputation\s+(\d+)\s*(>=|<=|==|>|<)\s*(\d+)", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var repId))
            {
                string repName = REPUTATION_DICT.TryGetValue(repId, out var label)
                    ? label
                    : $"Fama {repId}";

                string op = match.Groups[2].Value;
                string val = match.Groups[3].Value;

                return new MissionConditionView
                {
                    Raw = condition,
                    EditableText = $"If_Reputation {repId} {op} {val}",
                    Description = $"{repName} {GetOperatorLabel(op)} {val}"
                };
            }

            return new MissionConditionView
            {
                Raw = condition,
                EditableText = condition,
                Description = "Condição de fama"
            };
        }

        private MissionConditionView ParseFmsCondition(string condition)
        {
            var doneMatch = Regex.Match(condition, @"If_FMS\s+(\d+)\s+done\b", RegexOptions.IgnoreCase);
            if (doneMatch.Success)
            {
                string fmsId = doneMatch.Groups[1].Value;

                return new MissionConditionView
                {
                    Raw = condition,
                    EditableText = $"If_FMS {fmsId} done",
                    Description = $"Necessário ter completado a quest {fmsId}"
                };
            }

            var compareMatch = Regex.Match(condition, @"If_FMS\s*(\d+)\s*(>=|<=|==|>|<)\s*(\d+)", RegexOptions.IgnoreCase);
            if (compareMatch.Success)
            {
                string fmsId = compareMatch.Groups[1].Value;
                string op = compareMatch.Groups[2].Value;
                string val = compareMatch.Groups[3].Value;

                return new MissionConditionView
                {
                    Raw = condition,
                    EditableText = $"If_FMS {fmsId} {op} {val}",
                    Description = $"FMS {fmsId} {GetOperatorLabel(op)} {val}"
                };
            }

            return new MissionConditionView
            {
                Raw = condition,
                EditableText = condition,
                Description = "Condição de FMS"
            };
        }



        private MissionConditionView ParseClassCondition(string condition)
        {
            var match = Regex.Match(condition, @"If_Class\s+(\d+)", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var classId))
            {
                string className = MISSION_CLASS.TryGetValue(classId, out var label)
                    ? label
                    : $"Classe {classId}";

                return new MissionConditionView
                {
                    Raw = condition,
                    EditableText = $"If_Class {classId}",
                    Description = $"Classe necessária: {className}"
                };
            }

            return new MissionConditionView
            {
                Raw = condition,
                EditableText = condition,
                Description = "Condição de classe"
            };
        }

        private MissionConditionView ParseRebirthCondition(string condition)
        {
            var match = Regex.Match(condition, @"If_CheckRebirthCount\s*(>=|<=|==|>|<)\s*(\d+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string op = match.Groups[1].Value;
                string val = match.Groups[2].Value;

                return new MissionConditionView
                {
                    Raw = condition,
                    EditableText = $"If_CheckRebirthCount {op} {val}",
                    Description = $"Reencarnações necessárias {GetOperatorLabel(op)} {val}"
                };
            }

            return new MissionConditionView
            {
                Raw = condition,
                EditableText = condition,
                Description = "Condição de reencarnação"
            };
        }

        private MissionConditionView ParseCheckBuffCondition(string condition)
        {
            var match = Regex.Match(condition, @"If_CheckBuff\s+(\d+)\s+(\d+)\s+(\d+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string buffId = match.Groups[1].Value;
                int mode = int.Parse(match.Groups[2].Value);
                string value = match.Groups[3].Value;

                string description = mode switch
                {
                    1 when value == "1" => $"Necessário possuir o buff {buffId}",
                    1 => $"Necessário buff {buffId} maior ou igual a {value}",
                    2 => $"Necessário buff {buffId} menor ou igual a {value}",
                    3 when value == "0" => $"Bloqueado enquanto buff {buffId} estiver ativo",
                    3 => $"Bloqueado enquanto buff {buffId} for maior ou igual a {value}",
                    4 => $"Bloqueado enquanto buff {buffId} for menor ou igual a {value}",
                    _ => $"Condição de buff {buffId} (modo {mode}, valor {value})"
                };

                return new MissionConditionView
                {
                    Raw = condition,
                    EditableText = $"If_CheckBuff {buffId} {mode} {value}",
                    Description = description
                };
            }

            return new MissionConditionView
            {
                Raw = condition,
                EditableText = condition,
                Description = "Condição de buff"
            };
        }

        private string GetOperatorLabel(string op)
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
                SaveCurrentMissionFiles();

                MessageBox.Show(
                    "Alterações salvas com sucesso em S_Mission.ini, C_Mission.ini e T_Mission.ini.",
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

        private string GetMissionDescriptionById(string missionId)
        {
            if (string.IsNullOrWhiteSpace(missionId))
                return string.Empty;

            return db != null &&
                   db.TRows.TryGetValue(missionId.Trim(), out var t) &&
                   !string.IsNullOrWhiteSpace(t.Description)
                ? t.Description
                : string.Empty;
        }

        private void RefreshGeneralLegends()
        {
            string missionId = GetBoxText(MissionIdBox);
            string acceptNpcId = GetBoxText(AcceptNpcBox);
            string finishNpcId = GetBoxText(FinishNpcBox);
            string acceptItemId = GetBoxText(AcceptItemBox);

            
            SetText(AcceptNpcLegendBox, GetNpcLegendText(acceptNpcId));
            SetText(FinishNpcLegendBox, GetNpcLegendText(finishNpcId));
            SetText(AcceptItemLegendBox, GetItemLegendText(acceptItemId));
            SetText(FinishItemLegendBox, GetItemLegendText(GetBoxText(FinishItemBox)));
        }
        private string GetNpcLegendText(string npcId)
        {
            if (string.IsNullOrWhiteSpace(npcId))
                return string.Empty;

            return $"[ {npcId}  {GetNpcName(npcId)} ]";
        }

        private string GetItemLegendText(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return string.Empty;

            return $"[ {itemId}  {GetItemName(itemId)} ]";
        }

        private void Clone_Click(object sender, RoutedEventArgs e)
        {
            if (MissionList.SelectedItem is not MissionEntry selected)
            {
                MessageBox.Show(
                    "Selecione uma missão para clonar.",
                    "Clonar missão",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!db.SRows.TryGetValue(selected.Id, out var row))
                return;

            string newId = PromptNewId("Clonar missão", "Informe o novo ID:");

            if (newId == null)
                return;

            if (string.IsNullOrWhiteSpace(newId))
            {
                MessageBox.Show(
                    "O novo ID não pode ficar vazio.",
                    "Clonar missão",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (db.SRows.ContainsKey(newId))
            {
                MessageBox.Show(
                    "Esse ID já existe. Escolha outro ID.",
                    "Clonar missão",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var clonedRow = new List<string>(row);
            while (clonedRow.Count <= IDX_MISSION_ID)
                clonedRow.Add(string.Empty);

            clonedRow[IDX_MISSION_ID] = newId;
            db.SRows[newId] = clonedRow;

            if (db.TRows.TryGetValue(selected.Id, out var translation))
            {
                db.TRows[newId] = new MissionTranslation
                {
                    Name = translation.Name,
                    Classification = translation.Classification
                };
            }
            else
            {
                db.TRows[newId] = new MissionTranslation
                {
                    Name = selected.Name,
                    Classification = string.Empty
                };
            }

            try
            {
                string sMissionPath = Path.Combine(clientPath, "data", "db", "S_Mission.ini");
                string cMissionPath = Path.Combine(clientPath, "data", "db", "C_Mission.ini");
                string tMissionPath = Path.Combine(clientPath, "data", "translate", "T_Mission.ini");

                SaveDataIni(sMissionPath, Encoding.GetEncoding(950));

                if (File.Exists(cMissionPath))
                    SaveDataIni(cMissionPath, Encoding.GetEncoding(950));

                SaveTranslateIni(tMissionPath, Encoding.GetEncoding(1252));
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "A missão foi clonada em memória, mas houve erro ao salvar:\n\n" + ex.Message,
                    "Erro",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            LoadDatabase();

            _suppressSelectionChanged = true;
            MissionList.SelectedItem = Missions.FirstOrDefault(x => x.Id == newId);
            _suppressSelectionChanged = false;

            MissionList.SelectedItem = Missions.FirstOrDefault(x => x.Id == newId);

            MessageBox.Show(
                $"Missão clonada com sucesso para o ID {newId}.",
                "Clonar missão",
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
            _originalClassificationSnapshot = null;
            _lastSelectedMissionId = null;
        }

        private static void SetText(TextBox box, string value)
        {
            if (box != null)
                box.Text = value ?? string.Empty;
        }

        private static void SetText(TextBlock block, string value)
        {
            if (block != null)
                block.Text = value ?? string.Empty;
        }

        private MissionDb LoadMissionDb(string clientPath)
        {
            var db = new MissionDb();

            string sPath = Path.Combine(clientPath, "data", "db", "S_Mission.ini");
            string tPath = Path.Combine(clientPath, "data", "translate", "T_Mission.ini");

            if (File.Exists(sPath))
            {
                var lines = File.ReadAllLines(sPath, Encoding.GetEncoding(950));

                for (int i = 1; i < lines.Length; i++)
                {
                    var parts = lines[i].Split('|');

                    if (parts.Length < 5 || string.IsNullOrWhiteSpace(parts[0]))
                        continue;

                    db.SRows[parts[0].Trim()] = parts.Select(x => x.Trim()).ToList();
                }
            }

            if (File.Exists(tPath))
            {
                var lines = File.ReadAllLines(tPath, Encoding.GetEncoding(1252));

                for (int i = 1; i < lines.Length; i++)
                {
                    var parts = lines[i].Split('|');

                    if (parts.Length < 4 || string.IsNullOrWhiteSpace(parts[0]))
                        continue;

                    db.TRows[parts[0].Trim()] = new MissionTranslation
                    {
                        Name = parts.Length > 1 ? (parts[1] ?? string.Empty).Trim() : string.Empty,
                        Description = parts.Length > 2 ? (parts[2] ?? string.Empty).Trim() : string.Empty,
                        Classification = parts.Length > 3 ? (parts[3] ?? string.Empty).Trim() : string.Empty
                    };
                }
            }

            return db;
        }
    }
}