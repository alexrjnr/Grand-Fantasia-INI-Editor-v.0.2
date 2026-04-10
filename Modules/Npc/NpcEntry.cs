using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using GrandFantasiaINIEditor.Core;

namespace GrandFantasiaINIEditor.Modules.Npc
{
    public enum NpcCursorType
    {
        None = 1,
        Talk = 2,
        Examine = 3,
        Transport = 4,
        Repair = 5,
        Auction = 6,
        Mail = 7
    }

    public class NpcEntry : INotifyPropertyChanged
    {
        public sealed class CommandTokenLine
        {
            public string Token { get; set; }
            public string Legend { get; set; }
            public int SectionIndex { get; set; }
            public int TokenIndex { get; set; }
        }

        public sealed class CommandSectionsView
        {
            public List<CommandTokenLine> EntryTokens { get; set; } = new();
            public List<CommandTokenLine> ConditionTokens { get; set; } = new();
            public List<CommandTokenLine> SuccessTokens { get; set; } = new();
            public List<CommandTokenLine> FailTokens { get; set; } = new();
        }

        // 0: NpcID
        private string _id;
        public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }

        // 1: ModelString
        private string _modelString;
        public string ModelString { get => _modelString; set { _modelString = value; OnPropertyChanged(); } }

        // 2: NpcName (from S_Npc)
        private string _npcName;
        public string NpcName
        {
            get => _npcName;
            set
            {
                _npcName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(CompactDisplayName));
            }
        }

        // Translation Name (from T_Npc)
        private string _translatedName;
        public string TranslatedName
        {
            get => _translatedName;
            set
            {
                _translatedName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(CompactDisplayName));
            }
        }

        // Translation Description (from T_Npc)
        private string _translatedDesc;
        public string TranslatedDesc { get => _translatedDesc; set { _translatedDesc = value; OnPropertyChanged(); } }

        // 3: NpcControl
        private string _npcControl;
        public string NpcControl { get => _npcControl; set { _npcControl = value; OnPropertyChanged(); } }

        // 4: CursorType
        private NpcCursorType _cursorType;
        public NpcCursorType CursorType { get => _cursorType; set { _cursorType = value; OnPropertyChanged(); } }

        // 5: DialogId1
        private string _dialogId1;
        public string DialogId1 { get => _dialogId1; set { _dialogId1 = value; OnPropertyChanged(); } }

        // 6: DialogId2
        private string _dialogId2;
        public string DialogId2 { get => _dialogId2; set { _dialogId2 = value; OnPropertyChanged(); } }

        // 7: DialogRate
        private string _dialogRate;
        public string DialogRate { get => _dialogRate; set { _dialogRate = value; OnPropertyChanged(); } }

        // 8: NpcCmd
        private string _npcCmd;
        public string NpcCmd
        {
            get => _npcCmd;
            set
            {
                _npcCmd = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NpcBaseDialogId));
                OnPropertyChanged(nameof(NpcCmdLegend));
                OnPropertyChanged(nameof(NpcCmdSections));
            }
        }

        public string NpcBaseDialogId
        {
            get
            {
                string raw = (_npcCmd ?? string.Empty).Trim();
                int cut = raw.IndexOf("##", StringComparison.Ordinal);
                if (cut >= 0)
                    raw = raw.Substring(0, cut);

                return NormalizeSegment(raw);
            }
            set
            {
                string id = (value ?? string.Empty).Trim().Trim(':').Trim('#').Trim();
                NpcCmd = string.IsNullOrWhiteSpace(id) ? "##" : $"{id}##";
            }
        }

        // OptionCmds (9 to 16)
        private string _optionCmd1;
        public string OptionCmd1
        {
            get => _optionCmd1;
            set
            {
                _optionCmd1 = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OptionCmd1Legend));
                OnPropertyChanged(nameof(OptionCmd1Sections));
            }
        }

        private string _optionCmd2;
        public string OptionCmd2
        {
            get => _optionCmd2;
            set
            {
                _optionCmd2 = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OptionCmd2Legend));
                OnPropertyChanged(nameof(OptionCmd2Sections));
            }
        }

        private string _optionCmd3;
        public string OptionCmd3
        {
            get => _optionCmd3;
            set
            {
                _optionCmd3 = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OptionCmd3Legend));
                OnPropertyChanged(nameof(OptionCmd3Sections));
            }
        }

        private string _optionCmd4;
        public string OptionCmd4
        {
            get => _optionCmd4;
            set
            {
                _optionCmd4 = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OptionCmd4Legend));
                OnPropertyChanged(nameof(OptionCmd4Sections));
            }
        }

        private string _optionCmd5;
        public string OptionCmd5
        {
            get => _optionCmd5;
            set
            {
                _optionCmd5 = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OptionCmd5Legend));
                OnPropertyChanged(nameof(OptionCmd5Sections));
            }
        }

        private string _optionCmd6;
        public string OptionCmd6
        {
            get => _optionCmd6;
            set
            {
                _optionCmd6 = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OptionCmd6Legend));
                OnPropertyChanged(nameof(OptionCmd6Sections));
            }
        }

        private string _optionCmd7;
        public string OptionCmd7
        {
            get => _optionCmd7;
            set
            {
                _optionCmd7 = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OptionCmd7Legend));
                OnPropertyChanged(nameof(OptionCmd7Sections));
            }
        }

        private string _optionCmd8;
        public string OptionCmd8
        {
            get => _optionCmd8;
            set
            {
                _optionCmd8 = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OptionCmd8Legend));
                OnPropertyChanged(nameof(OptionCmd8Sections));
            }
        }

        // 17: LocateLimit
        private string _locateLimit;
        public string LocateLimit { get => _locateLimit; set { _locateLimit = value; OnPropertyChanged(); } }

        // 18: ControlFlag
        private string _controlFlag;
        public string ControlFlag { get => _controlFlag; set { _controlFlag = value; OnPropertyChanged(); } }

        // 19: Note
        private string _note;
        public string Note { get => _note; set { _note = value; OnPropertyChanged(); } }


        // Display Property for DataGrid
        public string DisplayName => !string.IsNullOrWhiteSpace(TranslatedName) ? TranslatedName : NpcName;
        public string CompactDisplayName => CollapseLineBreaks(DisplayName);
        public static Func<string, string> ItemNameResolver { get; set; } = id => string.IsNullOrWhiteSpace(id) ? "Item" : $"Item {id}";
        public static Func<string, string> NpcNameResolver { get; set; } = id => string.IsNullOrWhiteSpace(id) ? "NPC" : $"NPC {id}";
        public static Func<string, string> MonsterNameResolver { get; set; } = id => string.IsNullOrWhiteSpace(id) ? "Monstro" : $"Monstro {id}";
        public static Func<string, string> NodeNameResolver { get; set; } = id => string.IsNullOrWhiteSpace(id) ? "No" : $"No {id}";

        public string NpcCmdLegend => BuildCommandLegend(NpcCmd, true);
        public string OptionCmd1Legend => BuildCommandLegend(OptionCmd1, false);
        public string OptionCmd2Legend => BuildCommandLegend(OptionCmd2, false);
        public string OptionCmd3Legend => BuildCommandLegend(OptionCmd3, false);
        public string OptionCmd4Legend => BuildCommandLegend(OptionCmd4, false);
        public string OptionCmd5Legend => BuildCommandLegend(OptionCmd5, false);
        public string OptionCmd6Legend => BuildCommandLegend(OptionCmd6, false);
        public string OptionCmd7Legend => BuildCommandLegend(OptionCmd7, false);
        public string OptionCmd8Legend => BuildCommandLegend(OptionCmd8, false);
        public CommandSectionsView NpcCmdSections => BuildCommandSections(NpcCmd);
        public CommandSectionsView OptionCmd1Sections => BuildCommandSections(OptionCmd1);
        public CommandSectionsView OptionCmd2Sections => BuildCommandSections(OptionCmd2);
        public CommandSectionsView OptionCmd3Sections => BuildCommandSections(OptionCmd3);
        public CommandSectionsView OptionCmd4Sections => BuildCommandSections(OptionCmd4);
        public CommandSectionsView OptionCmd5Sections => BuildCommandSections(OptionCmd5);
        public CommandSectionsView OptionCmd6Sections => BuildCommandSections(OptionCmd6);
        public CommandSectionsView OptionCmd7Sections => BuildCommandSections(OptionCmd7);
        public CommandSectionsView OptionCmd8Sections => BuildCommandSections(OptionCmd8);

        private static string CollapseLineBreaks(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
        }

        private static string BuildCommandLegend(string raw, bool isBase)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "[ vazio ]";

            string source = raw.Trim();
            string compact = CollapseLineBreaks(source).Trim();
            var parts = compact.Split('#');

            if (isBase)
            {
                string head = parts.Length > 0 ? NormalizeSegment(parts[0]) : string.Empty;
                if (int.TryParse(head, out var dialogId))
                {
                    string format = LocalizationManager.Instance.GetLocalizedString("Npc.Legends.InternalDialog");
                    return $"[ {string.Format(format ?? "Abre dialogo ({0}).", dialogId)} ]";
                }
            }

            string command = string.Empty;
            string condition = string.Empty;
            string success = string.Empty;
            string fail = string.Empty;

            foreach (var token in parts)
            {
                string value = NormalizeSegment(token);
                if (string.IsNullOrEmpty(value))
                    continue;

                if (string.IsNullOrEmpty(command))
                {
                    command = value;
                    continue;
                }

                if (string.IsNullOrEmpty(condition))
                {
                    condition = value;
                    continue;
                }

                if (string.IsNullOrEmpty(success))
                {
                    success = value;
                    continue;
                }

                if (string.IsNullOrEmpty(fail))
                {
                    fail = value;
                    continue;
                }
            }

            if (string.IsNullOrEmpty(command))
                command = NormalizeSegment(compact);

            string commandText = DescribeEntryCommand(command);
            string conditionText = DescribeCondition(condition);
            string successText = DescribeActions(success, "Ao cumprir: sem acao definida.", "Ao cumprir");
            string failText = DescribeFail(fail);

            return $"[ {commandText} | {conditionText} | {successText} | {failText} ]";
        }

        private static string NormalizeSegment(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
                return string.Empty;

            return segment.Trim().Trim(':').Trim().Trim('#').Trim();
        }

        private static string DescribeEntryCommand(string command)
        {
            string prefix = LocalizationManager.Instance.GetLocalizedString("Npc.Commands.Prefix") ?? "Comando: ";
            
            if (string.IsNullOrWhiteSpace(command))
                return prefix + (LocalizationManager.Instance.GetLocalizedString("Npc.Commands.EntryNone") ?? "entrada sem comando.");

            var talkMatch = Regex.Match(command, @"^Talk\s+(\d+)\s+(\d+)$", RegexOptions.IgnoreCase);
            if (talkMatch.Success)
            {
                string template = LocalizationManager.Instance.GetLocalizedString("Npc.Commands.Talk") ?? "Falar: {0}";
                return string.Format(template, talkMatch.Groups[1].Value);
            }

            var serviceMatch = Regex.Match(command, @"^Service\s+(\d+)\s+(\d+)\s+(\d+)$", RegexOptions.IgnoreCase);
            if (serviceMatch.Success)
            {
                string serviceType = serviceMatch.Groups[2].Value;
                string serviceLabel = DescribeServiceType(serviceType);
                string template = LocalizationManager.Instance.GetLocalizedString("Npc.Commands.Service") ?? "Serviço {0} ({1})";
                return string.Format(template, serviceLabel, serviceMatch.Groups[3].Value);
            }

            if (int.TryParse(command, out var dialogId))
            {
                string template = LocalizationManager.Instance.GetLocalizedString("Npc.Legends.InternalDialog") ?? "Abre dialogo ({0}).";
                return prefix + string.Format(template, dialogId);
            }

            return prefix + $"{command}.";
        }

        private static string DescribeCondition(string conditionBlock)
        {
            if (string.IsNullOrWhiteSpace(conditionBlock))
                return (LocalizationManager.Instance.GetLocalizedString("Npc.Conditions.Prefix") ?? "Condicao: ") + 
                       (LocalizationManager.Instance.GetLocalizedString("Npc.Conditions.None") ?? "sem requisito (sempre disponivel).");

            string[] conditions = conditionBlock
                .Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();

            if (conditions.Length == 0)
                return (LocalizationManager.Instance.GetLocalizedString("Npc.Conditions.Prefix") ?? "Condicao: ") + 
                       (LocalizationManager.Instance.GetLocalizedString("Npc.Conditions.None") ?? "sem requisito (sempre disponivel).");

            string[] described = conditions.Select(DescribeSingleCondition).ToArray();
            return (LocalizationManager.Instance.GetLocalizedString("Npc.Conditions.Prefix") ?? "Condicao: ") + string.Join(" | ", described);
        }

        private static string DescribeSingleCondition(string condition)
        {
            var invMatch = Regex.Match(condition, @"^If_InventoryItem\s+(\d+)\s*(>=|<=|>|<|==|=|!=)\s*(\d+)$", RegexOptions.IgnoreCase);
            if (invMatch.Success)
            {
                string itemId = invMatch.Groups[1].Value;
                string itemName = ResolveName(ItemNameResolver, itemId, "Item");
                string op = DescribeOperator(invMatch.Groups[2].Value);
                string amount = invMatch.Groups[3].Value;
                string template = LocalizationManager.Instance.GetLocalizedString("Npc.Conditions.InventoryItem") ?? "Possuir item {0} ({1}) {2} {3}";
                return string.Format(template, itemName, itemId, op, amount);
            }

            var levelMatch = Regex.Match(condition, @"^If_CharLevel\s*(>=|<=|>|<|==|=|!=)\s*(\d+)$", RegexOptions.IgnoreCase);
            if (levelMatch.Success)
            {
                string op = DescribeOperator(levelMatch.Groups[1].Value);
                string level = levelMatch.Groups[2].Value;
                string template = LocalizationManager.Instance.GetLocalizedString("Npc.Conditions.CharLevel") ?? "Nível do personagem deve ser {0} {1}.";
                return string.Format(template, op, level);
            }

            var npcItemMatch = Regex.Match(condition, @"^NPCItem\s+(\d+)$", RegexOptions.IgnoreCase);
            if (npcItemMatch.Success)
            {
                string itemId = npcItemMatch.Groups[1].Value;
                string itemName = ResolveName(ItemNameResolver, itemId, "Item");
                string template = LocalizationManager.Instance.GetLocalizedString("Npc.Conditions.NpcItem") ?? "Entregar ao NPC o Item ({0}) [{1}].";
                return string.Format(template, itemId, itemName);
            }

            var npcTalkMatch = Regex.Match(condition, @"^NPCTalk\s+(\d+)\s+(\d+)$", RegexOptions.IgnoreCase);
            if (npcTalkMatch.Success)
            {
                string npcId = npcTalkMatch.Groups[1].Value;
                string npcName = ResolveName(NpcNameResolver, npcId, "NPC");
                string template = LocalizationManager.Instance.GetLocalizedString("Npc.Conditions.NpcTalk") ?? "Falar com NPC ({0}) [{1}] no diálogo ({2}).";
                return string.Format(template, npcId, npcName, npcTalkMatch.Groups[2].Value);
            }

            var itemCollectMatch = Regex.Match(condition, @"^ItemCollect\s+(\d+)\s+(\d+)$", RegexOptions.IgnoreCase);
            if (itemCollectMatch.Success)
            {
                string itemId = itemCollectMatch.Groups[1].Value;
                string itemName = ResolveName(ItemNameResolver, itemId, "Item");
                string template = LocalizationManager.Instance.GetLocalizedString("Npc.Conditions.ItemCollect") ?? "Coletar Item ({0}) [{1}] x{2}.";
                return string.Format(template, itemId, itemName, itemCollectMatch.Groups[2].Value);
            }

            var monsterKilledMatch = Regex.Match(condition, @"^MonsterKilled\s+(\d+)\s+(\d+)$", RegexOptions.IgnoreCase);
            if (monsterKilledMatch.Success)
            {
                string monsterId = monsterKilledMatch.Groups[1].Value;
                string monsterName = ResolveName(MonsterNameResolver, monsterId, "Monstro");
                string template = LocalizationManager.Instance.GetLocalizedString("Npc.Conditions.MonsterKilled") ?? "Derrotar Monstro ({0}) [{1}] x{2}.";
                return string.Format(template, monsterId, monsterName, monsterKilledMatch.Groups[2].Value);
            }

            var itemGivenMatch = Regex.Match(condition, @"^ItemGiven\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)$", RegexOptions.IgnoreCase);
            if (itemGivenMatch.Success)
            {
                string npcId = itemGivenMatch.Groups[1].Value;
                string npcName = ResolveName(NpcNameResolver, npcId, "NPC");
                string itemId = itemGivenMatch.Groups[3].Value;
                string itemName = ResolveName(ItemNameResolver, itemId, "Item");
                string template = LocalizationManager.Instance.GetLocalizedString("Npc.Conditions.ItemGiven") ?? "Entregar ao NPC ({0}) [{1}] o Item ({2}) [{3}] x{4} no diálogo ({5}).";
                return string.Format(template, npcId, npcName, itemId, itemName, itemGivenMatch.Groups[4].Value, itemGivenMatch.Groups[2].Value);
            }

            var itemGotMatch = Regex.Match(condition, @"^ItemGot\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)$", RegexOptions.IgnoreCase);
            if (itemGotMatch.Success)
            {
                string npcId = itemGotMatch.Groups[1].Value;
                string npcName = ResolveName(NpcNameResolver, npcId, "NPC");
                string itemId = itemGotMatch.Groups[3].Value;
                string itemName = ResolveName(ItemNameResolver, itemId, "Item");
                string template = LocalizationManager.Instance.GetLocalizedString("Npc.Conditions.ItemGot") ?? "Receber do NPC ({0}) [{1}] o Item ({2}) [{3}] x{4} no diálogo ({5}).";
                return string.Format(template, npcId, npcName, itemId, itemName, itemGotMatch.Groups[4].Value, itemGotMatch.Groups[2].Value);
            }

            var treasureMatch = Regex.Match(condition, @"^MonsterTreasureGot\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)$", RegexOptions.IgnoreCase);
            if (treasureMatch.Success)
            {
                string monsterId = treasureMatch.Groups[1].Value;
                string monsterName = ResolveName(MonsterNameResolver, monsterId, "Monstro");
                string itemId = treasureMatch.Groups[3].Value;
                string itemName = ResolveName(ItemNameResolver, itemId, "Item");
                string template = LocalizationManager.Instance.GetLocalizedString("Npc.Conditions.MonsterTreasureGot") ?? "Obter Item ({0}) [{1}] x{2} do Monstro ({3}) [{4}] com chance {5}%.";
                return string.Format(template, itemId, itemName, treasureMatch.Groups[4].Value, monsterId, monsterName, treasureMatch.Groups[2].Value);
            }

            var checkBuffMatch = Regex.Match(condition, @"^CheckBuff\s+(\d+)\s+(\d+)\s+(\d+)$", RegexOptions.IgnoreCase);
            if (checkBuffMatch.Success)
            {
                string buffId = checkBuffMatch.Groups[1].Value;
                string mode = checkBuffMatch.Groups[2].Value;
                string value = checkBuffMatch.Groups[3].Value;
                
                string key = mode switch
                {
                    "1" when value == "1" => "Npc.Conditions.BuffActive",
                    "1" => "Npc.Conditions.BuffGE",
                    "2" => "Npc.Conditions.BuffLE",
                    "3" when value == "0" => "Npc.Conditions.BuffActiveBlocked",
                    "3" => "Npc.Conditions.BuffGEBlocked",
                    "4" => "Npc.Conditions.BuffLEBlocked",
                    _ => "Npc.Conditions.BuffOther"
                };

                string template = LocalizationManager.Instance.GetLocalizedString(key);
                if (key == "Npc.Conditions.BuffOther")
                    return string.Format(template ?? "Checagem de Buff ({0}), modo {1}, valor {2}", buffId, mode, value);
                else
                    return string.Format(template ?? "Buff {0}...", buffId, value);
            }

            var areaTravelMatch = Regex.Match(condition, @"^AreaTravel\s+(\d+)\s+(\d+)$", RegexOptions.IgnoreCase);
            if (areaTravelMatch.Success)
            {
                string nodeId = areaTravelMatch.Groups[1].Value;
                string nodeName = ResolveName(NodeNameResolver, nodeId, "No");
                string template = LocalizationManager.Instance.GetLocalizedString("Npc.Conditions.AreaTravel") ?? "Passar pela área ({0}) no Nó ({1}) [{2}]";
                return string.Format(template, areaTravelMatch.Groups[2].Value, nodeId, nodeName);
            }

            string customTemplate = LocalizationManager.Instance.GetLocalizedString("Npc.Conditions.Custom") ?? "Condição personalizada: {0}";
            return string.Format(customTemplate, condition);
        }

        private static string DescribeActions(string actionBlock, string emptyText, string label)
        {
            if (string.IsNullOrWhiteSpace(actionBlock))
                return emptyText;

            string[] actions = actionBlock
                .Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();

            if (actions.Length == 0)
                return emptyText;

            string[] described = actions.Select(DescribeSingleAction).ToArray();
            return $"{label}: " + string.Join("; ", described);
        }

        private static string DescribeFail(string failBlock)
        {
            string failPrefix = LocalizationManager.Instance.GetLocalizedString("Npc.Actions.FailPrefix") ?? "Ao falhar";
            string failNone = LocalizationManager.Instance.GetLocalizedString("Npc.Actions.FailNone") ?? "sem ação definida";
            
            if (string.IsNullOrWhiteSpace(failBlock))
                return $"{failPrefix}: {failNone}.";

            if (int.TryParse(failBlock, out var dialogId))
            {
                string template = LocalizationManager.Instance.GetLocalizedString("Npc.Legends.InternalDialog") ?? "Abre dialogo ({0}).";
                return $"{failPrefix}: " + string.Format(template, dialogId);
            }

            return DescribeActions(failBlock, $"{failPrefix}: {failNone}.", failPrefix);
        }

        private static string DescribeSingleAction(string action)
        {
            if (string.IsNullOrWhiteSpace(action))
                return LocalizationManager.Instance.GetLocalizedString("Npc.Actions.Empty") ?? "ação vazia";

            var invMatch = Regex.Match(action, @"^InventoryItem\s+(\d+)\s*([+-])\s*(\d+)$", RegexOptions.IgnoreCase);
            if (invMatch.Success)
            {
                string itemId = invMatch.Groups[1].Value;
                string itemName = ResolveName(ItemNameResolver, itemId, "Item");
                string signalResource = invMatch.Groups[2].Value == "-" ? "Common.RemoveAction" : "Common.AddAction";
                string signal = LocalizationManager.Instance.GetLocalizedString(signalResource) ?? (invMatch.Groups[2].Value == "-" ? "remove" : "adiciona");
                string template = LocalizationManager.Instance.GetLocalizedString("Npc.Actions.InventoryItem") ?? "{0} Item ({1}) [{2}] x{3}";
                return string.Format(template, signal, itemId, itemName, invMatch.Groups[3].Value);
            }

            var buffMatch = Regex.Match(action, @"^GiveBuff\s+(\d+)\s+(\d+)$", RegexOptions.IgnoreCase);
            if (buffMatch.Success)
            {
                string template = LocalizationManager.Instance.GetLocalizedString("Npc.Actions.GiveBuff") ?? "aplica Buff ({0}) por {1} segundos";
                return string.Format(template, buffMatch.Groups[1].Value, buffMatch.Groups[2].Value);
            }

            var triggerMatch = Regex.Match(action, @"^Event_TriggerEvent\s+(\d+)\s+(\d+)\s+(\d+)$", RegexOptions.IgnoreCase);
            if (triggerMatch.Success)
            {
                string template = LocalizationManager.Instance.GetLocalizedString("Npc.Actions.EventTrigger") ?? "dispara Event_TriggerEvent ({0}, {1}, {2})";
                return string.Format(template, triggerMatch.Groups[1].Value, triggerMatch.Groups[2].Value, triggerMatch.Groups[3].Value);
            }

            var talkMatch = Regex.Match(action, @"^Talk\s+(\d+)\s+(\d+)$", RegexOptions.IgnoreCase);
            if (talkMatch.Success)
            {
                string template = LocalizationManager.Instance.GetLocalizedString("Npc.Actions.Talk") ?? "Talk: opção ({0}) e resposta ({1})";
                return string.Format(template, talkMatch.Groups[1].Value, talkMatch.Groups[2].Value);
            }

            var serviceMatch = Regex.Match(action, @"^Service\s+(\d+)\s+(\d+)\s+(\d+)$", RegexOptions.IgnoreCase);
            if (serviceMatch.Success)
            {
                string serviceType = serviceMatch.Groups[2].Value;
                string serviceLabel = DescribeServiceType(serviceType);
                string template = LocalizationManager.Instance.GetLocalizedString("Npc.Actions.Service") ?? "Service: diálogo ({0}), tipo ({1}: {2}), ID da loja ({3})";
                return string.Format(template, serviceMatch.Groups[1].Value, serviceType, serviceLabel, serviceMatch.Groups[3].Value);
            }

            if (int.TryParse(action, out var dialogId))
            {
                string template = LocalizationManager.Instance.GetLocalizedString("Npc.Legends.InternalDialog") ?? "Abre dialogo ({0}).";
                return string.Format(template, dialogId);
            }

            return action;
        }

        private static string DescribeOperator(string op)
        {
            return op switch
            {
                ">=" => LocalizationManager.Instance.GetLocalizedString("Common.Operators.GE") ?? "maior ou igual a",
                "<=" => LocalizationManager.Instance.GetLocalizedString("Common.Operators.LE") ?? "menor ou igual a",
                ">" => LocalizationManager.Instance.GetLocalizedString("Common.Operators.GT") ?? "maior que",
                "<" => LocalizationManager.Instance.GetLocalizedString("Common.Operators.LT") ?? "menor que",
                "==" => LocalizationManager.Instance.GetLocalizedString("Common.Operators.EQ") ?? "igual a",
                "=" => LocalizationManager.Instance.GetLocalizedString("Common.Operators.EQ") ?? "igual a",
                "!=" => LocalizationManager.Instance.GetLocalizedString("Common.Operators.NE") ?? "diferente de",
                _ => op
            };
        }

        private static string ResolveName(Func<string, string> resolver, string id, string fallbackPrefix)
        {
            if (resolver == null)
                return $"{fallbackPrefix} {id}";

            string resolved = resolver(id);
            return string.IsNullOrWhiteSpace(resolved)
                ? $"{fallbackPrefix} {id}"
                : resolved;
        }

        private static CommandSectionsView BuildCommandSections(string raw)
        {
            return new CommandSectionsView
            {
                EntryTokens = BuildSectionTokenLines(raw, 1, "entry"),
                ConditionTokens = BuildSectionTokenLines(raw, 2, "condition"),
                SuccessTokens = BuildSectionTokenLines(raw, 3, "success"),
                FailTokens = BuildSectionTokenLines(raw, 4, "fail")
            };
        }

        private static List<CommandTokenLine> BuildSectionTokenLines(string raw, int sectionIndex, string role)
        {
            string source = raw ?? string.Empty;
            string[] sections = source.Split(new[] { '#' }, StringSplitOptions.None);
            string sectionValue = sectionIndex < sections.Length ? sections[sectionIndex] ?? string.Empty : string.Empty;

            var tokens = ParseSectionSlots(sectionValue);

            var result = new List<CommandTokenLine>(tokens.Count);
            for (int i = 0; i < tokens.Count; i++)
            {
                string token = tokens[i] ?? string.Empty;
                result.Add(new CommandTokenLine
                {
                    Token = token,
                    Legend = DescribeSectionToken(role, token),
                    SectionIndex = sectionIndex,
                    TokenIndex = i
                });
            }

            return result;
        }

        private static string DescribeSectionToken(string role, string token)
        {
            string normalized = NormalizeSegment(token);

            if (string.IsNullOrWhiteSpace(normalized))
                return LocalizationManager.Instance.GetLocalizedString("Npc.Legends.EmptySlot") ?? "Campo vazio (slot entre dois ':' mantido para futura edição).";

            if (string.Equals(role, "condition", StringComparison.OrdinalIgnoreCase))
                return DescribeSingleCondition(normalized);

            if (string.Equals(role, "entry", StringComparison.OrdinalIgnoreCase))
                return DescribeEntryCommand(normalized);

            if (Regex.IsMatch(normalized, @"^If_", RegexOptions.IgnoreCase))
                return DescribeSingleCondition(normalized);

            if (Regex.IsMatch(normalized, @"^(Talk|Service|InventoryItem|GiveBuff|Event_TriggerEvent)\b", RegexOptions.IgnoreCase))
                return DescribeSingleAction(normalized);

            if (int.TryParse(normalized, out var dialogId))
            {
                string template = LocalizationManager.Instance.GetLocalizedString("Npc.Legends.InternalDialog") ?? "Abre dialogo ({0}).";
                return string.Format(template, dialogId);
            }

            string customTemplate = LocalizationManager.Instance.GetLocalizedString("Npc.Legends.CommandValue") ?? "Comando/valor: {0}.";
            return string.Format(customTemplate, normalized);
        }

        public static string ReplaceTokenInSection(string raw, int sectionIndex, int tokenIndex, string newToken)
        {
            if (sectionIndex < 0 || tokenIndex < 0)
                return raw ?? string.Empty;

            string source = raw ?? string.Empty;
            var sections = source.Split(new[] { '#' }, StringSplitOptions.None).ToList();

            while (sections.Count <= sectionIndex)
                sections.Add(string.Empty);

            var tokens = ParseSectionSlots(sections[sectionIndex]);

            while (tokens.Count <= tokenIndex)
                tokens.Add(string.Empty);

            tokens[tokenIndex] = newToken ?? string.Empty;
            sections[sectionIndex] = BuildSectionFromSlots(tokens);

            return string.Join("#", sections);
        }

        private static List<string> ParseSectionSlots(string sectionValue)
        {
            string value = sectionValue ?? string.Empty;

            if (value.StartsWith(":", StringComparison.Ordinal))
                value = value.Substring(1);

            if (value.EndsWith(":", StringComparison.Ordinal))
                value = value.Substring(0, value.Length - 1);

            var slots = value.Split(new[] { ':' }, StringSplitOptions.None).ToList();
            if (slots.Count == 0)
                slots.Add(string.Empty);

            return slots;
        }

        private static string BuildSectionFromSlots(List<string> slots)
        {
            if (slots == null || slots.Count == 0)
                return "::";

            return ":" + string.Join(":", slots) + ":";
        }

        private static string DescribeServiceType(string serviceType)
        {
            var services = LocalizationManager.Instance.GetDictionary("Npc.ServiceTypes");
            if (services != null && services.TryGetValue(serviceType, out var localized))
                return localized;

            return LocalizationManager.Instance.GetLocalizedString("Npc.ServiceTypes.NotMapped") ?? "tipo de service nao mapeado";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public NpcEntry Clone()
        {
            return (NpcEntry)this.MemberwiseClone();
        }
    }
}
