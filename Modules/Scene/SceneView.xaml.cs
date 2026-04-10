using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using GrandFantasiaINIEditor.Core;

namespace GrandFantasiaINIEditor.Modules.Scene
{
    public partial class SceneView : UserControl
    {
        private readonly string clientPath;
        private readonly ObservableCollection<SceneEntry> Scenes = new();
        private readonly ObservableCollection<NpcInstance> NpcInstances = new();
        private readonly ObservableCollection<MonsterInstance> MonsterInstances = new();
        private readonly ObservableCollection<SoundInstance> SoundInstances = new();

        private readonly Dictionary<string, string> _nodeNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _npcNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _monsterNames = new(StringComparer.OrdinalIgnoreCase);

        private bool _loading;
        private string _currentSceneFile;
        private SceneData _currentSceneData;

        public SceneView(string clientPath)
        {
            InitializeComponent();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            this.clientPath = clientPath;

            SceneList.ItemsSource = Scenes;
            NpcSubList.ItemsSource = NpcInstances;
            MonsterSubList.ItemsSource = MonsterInstances;
            SoundSubList.ItemsSource = SoundInstances;

            LoadTranslationLookups();
            LoadSceneList();
        }

        private void LoadTranslationLookups()
        {
            _nodeNames.Clear();
            _npcNames.Clear();
            _monsterNames.Clear();

            string translatePath = Path.Combine(clientPath, "data", "translate");
            LoadSimpleNameLookup(Path.Combine(translatePath, "T_Node.ini"), _nodeNames, 0, 1, Encoding.GetEncoding(1252));
            LoadMultilineNameLookup(Path.Combine(translatePath, "T_Npc.ini"), _npcNames, Encoding.GetEncoding(1252));
            LoadSimpleNameLookup(Path.Combine(translatePath, "T_Monster.ini"), _monsterNames, 0, 1, Encoding.GetEncoding(1252));
        }

        private void LoadSimpleNameLookup(string path, Dictionary<string, string> target, int idColumn, int nameColumn, Encoding encoding)
        {
            if (!File.Exists(path)) return;
            var lines = File.ReadAllLines(path, encoding);
            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split('|');
                if (parts.Length <= Math.Max(idColumn, nameColumn)) continue;
                string id = parts[idColumn].Trim();
                string name = parts[nameColumn].Trim();
                if (!string.IsNullOrWhiteSpace(id) && !target.ContainsKey(id))
                    target[id] = name;
            }
        }

        private void LoadMultilineNameLookup(string path, Dictionary<string, string> target, Encoding encoding)
        {
            if (!File.Exists(path)) return;
            var lines = File.ReadAllLines(path, encoding);
            string currentId = null;
            var currentName = new StringBuilder();

            void Flush() {
                if (!string.IsNullOrWhiteSpace(currentId) && !target.ContainsKey(currentId))
                    target[currentId] = currentName.ToString().Trim();
            }

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i] ?? "";
                int pipe = line.IndexOf('|');
                if (pipe > 0 && int.TryParse(line.Substring(0, pipe).Trim(), out _))
                {
                    Flush();
                    var parts = line.Split('|');
                    currentId = parts[0].Trim();
                    currentName.Clear();
                    if (parts.Length > 1) currentName.Append(parts[1].Trim());
                }
                else if (currentId != null)
                {
                    currentName.Append(" ").Append(line.Trim().Trim('|').Trim());
                }
            }
            Flush();
        }

        private void LoadSceneList()
        {
            Scenes.Clear();
            string scenePath = Path.Combine(clientPath, "data", "scene");
            if (!Directory.Exists(scenePath)) return;

            var files = Directory.GetFiles(scenePath, "S*.ini")
                                 .OrderBy(f => f)
                                 .ToList();

            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                string idStr = Regex.Match(fileName, @"\d+").Value;
                int id = int.Parse(idStr);
                string displayName = _nodeNames.TryGetValue(id.ToString(), out var name) ? name : "";
                
                Scenes.Add(new SceneEntry { 
                    Id = id.ToString(), 
                    FileName = fileName, 
                    DisplayName = displayName,
                    FullPath = file
                });
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = SearchBox.Text.ToLower();
            var filtered = Scenes.Where(s => s.Id.Contains(filter) || s.DisplayName.ToLower().Contains(filter)).ToList();
            // In a real app we'd use a CollectionView but for simplicity we can just swap the source if needed
            // Actually, keep it simple for now and just use the full list.
        }

        private void SceneList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SceneList.SelectedItem is not SceneEntry entry) return;
            LoadScene(entry.FullPath);
        }

        private void LoadScene(string path)
        {
            _loading = true;
            _currentSceneFile = path;
            _currentSceneData = new SceneData();

            // S*.ini files are typically Big5 (950) or GB2312 depending on the region.
            // Using 950 for traditional Chinese (common in many older clients).
            string content = File.ReadAllText(path, Encoding.GetEncoding(950));
            ParseSceneContent(content);

            DisplaySceneData();
            _loading = false;
        }

        private void ParseSceneContent(string content)
        {
            // The section headers in GF scene files often end with a comma, e.g. [npc],
            string[] sections = { "[globe_data]", "[born_area]", "[revive_area]", "[gateway_area]", "[normal_area]", "[event_area]", "[position]", "[mob_patrol_mode]", "[reborn_monsters]", "[npc]", "[environment_sound]", "[event]", "[dynamic_block]" };
            
            Dictionary<string, string> sectionData = new();
            string currentSection = null;
            StringBuilder sectionContent = new();

            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                // Check if line is a header (starts with [ and contains section name)
                bool isHeader = false;
                foreach (var s in sections) {
                    if (trimmed.StartsWith(s)) {
                        if (currentSection != null) sectionData[currentSection] = sectionContent.ToString();
                        currentSection = s;
                        sectionContent.Clear();
                        isHeader = true;
                        break;
                    }
                }
                
                if (!isHeader && currentSection != null)
                {
                    sectionContent.AppendLine(line);
                }
            }
            if (currentSection != null) sectionData[currentSection] = sectionContent.ToString();

            // Store raw sections first
            _currentSceneData.GlobeData = sectionData.GetValueOrDefault("[globe_data]", "").Trim();
            _currentSceneData.BornArea = sectionData.GetValueOrDefault("[born_area]", "").Trim();
            _currentSceneData.ReviveArea = sectionData.GetValueOrDefault("[revive_area]", "").Trim();
            _currentSceneData.GatewayArea = sectionData.GetValueOrDefault("[gateway_area]", "").Trim();
            _currentSceneData.NormalArea = sectionData.GetValueOrDefault("[normal_area]", "").Trim();
            _currentSceneData.EventArea = sectionData.GetValueOrDefault("[event_area]", "").Trim();
            _currentSceneData.Position = sectionData.GetValueOrDefault("[position]", "").Trim();
            _currentSceneData.PatrolMode = sectionData.GetValueOrDefault("[mob_patrol_mode]", "").Trim();
            _currentSceneData.RebornMonsters = sectionData.GetValueOrDefault("[reborn_monsters]", "").Trim();
            _currentSceneData.Npc = sectionData.GetValueOrDefault("[npc]", "").Trim();
            _currentSceneData.EnvironmentSound = sectionData.GetValueOrDefault("[environment_sound]", "").Trim();
            _currentSceneData.Event = sectionData.GetValueOrDefault("[event]", "").Trim();
            _currentSceneData.DynamicBlock = sectionData.GetValueOrDefault("[dynamic_block]", "").Trim();

            // Parse lists
            ParseNpcs(_currentSceneData.Npc);
            ParseMonsters(_currentSceneData.RebornMonsters);
            ParseSounds(_currentSceneData.EnvironmentSound);
        }

        private void ParseNpcs(string raw)
        {
            NpcInstances.Clear();
            var lines = raw.Split('\n');
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                var parts = trimmed.Split(',');
                // NPCs typically have at least 12 fields (index 0 to 11)
                // -1,64041,148,286,5.49779,N006,½åªÌ¤Ú¤Úº¿,0,0,0,0,1,
                if (parts.Length < 7) continue;

                var npc = new NpcInstance {
                    HandleId = parts[0],
                    NpcTid = parts[1],
                    X = parts[2],
                    Y = parts[3],
                    Face = parts[4],
                    Model = parts[5],
                    Desc = parts[6],
                    RawLine = trimmed
                };
                npc.DisplayName = _npcNames.TryGetValue(npc.NpcTid, out var name) ? name : npc.Desc;
                NpcInstances.Add(npc);
            }
        }

        private void ParseMonsters(string raw)
        {
            MonsterInstances.Clear();
            var lines = raw.Split('\n');
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                var parts = trimmed.Split(',');
                // Monsters typically have around 24 fields
                // 1,55001,0,30,0,1,1,234,234,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
                if (parts.Length < 8) continue;

                var mob = new MonsterInstance {
                    HandleId = parts[0],
                    MonsterTid = parts[1],
                    PatrolId = parts[2],
                    MaxAmount = parts[3],
                    RebornSeconds = parts[4],
                    X = parts[5],
                    Y = parts[6],
                    Face = parts[7],
                    RawLine = trimmed
                };
                mob.DisplayName = _monsterNames.TryGetValue(mob.MonsterTid, out var name) ? name : "Unknown Monster";
                MonsterInstances.Add(mob);
            }
        }

        private void ParseSounds(string raw)
        {
            SoundInstances.Clear();
            var lines = raw.Split('\n');
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                var parts = trimmed.Split(',');
                if (parts.Length < 6) continue;

                var sound = new SoundInstance {
                    HandleId = parts[0],
                    Time = parts[1],
                    X = parts[2],
                    Y = parts[3],
                    Face = parts[4],
                    SoundName = parts[5],
                    RawLine = trimmed
                };
                SoundInstances.Add(sound);
            }
        }

        private void DisplaySceneData()
        {
            GlobeDataBox.Text = _currentSceneData.GlobeData;
            BornAreaBox.Text = _currentSceneData.BornArea;
            ReviveAreaBox.Text = _currentSceneData.ReviveArea;
            GatewayAreaBox.Text = _currentSceneData.GatewayArea;
            NormalAreaBox.Text = _currentSceneData.NormalArea;
            EventAreaBox.Text = _currentSceneData.EventArea;
            PositionBox.Text = _currentSceneData.Position;
            PatrolModeBox.Text = _currentSceneData.PatrolMode;
            EventBox.Text = _currentSceneData.Event;
            DynamicBlockBox.Text = _currentSceneData.DynamicBlock;
        }

        private void NpcSubList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NpcSubList.SelectedItem is not NpcInstance npc) return;
            _loading = true;
            NpcTidBox.Text = npc.NpcTid;
            NpcXBox.Text = npc.X;
            NpcYBox.Text = npc.Y;
            NpcFaceBox.Text = npc.Face;
            NpcModelBox.Text = npc.Model;
            NpcDescBox.Text = npc.Desc;
            _loading = false;
        }

        private void MonsterSubList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MonsterSubList.SelectedItem is not MonsterInstance mob) return;
            _loading = true;
            MonsterTidBox.Text = mob.MonsterTid;
            MonsterPatrolBox.Text = mob.PatrolId;
            MonsterAmountBox.Text = mob.MaxAmount;
            MonsterRebornBox.Text = mob.RebornSeconds;
            MonsterXBox.Text = mob.X;
            MonsterYBox.Text = mob.Y;
            MonsterFaceBox.Text = mob.Face;
            _loading = false;
        }

        private void SoundSubList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SoundSubList.SelectedItem is not SoundInstance sound) return;
            _loading = true;
            SoundTimeBox.Text = sound.Time;
            SoundXBox.Text = sound.X;
            SoundYBox.Text = sound.Y;
            SoundNameBox.Text = sound.SoundName;
            _loading = false;
        }

        private void NpcField_Changed(object sender, TextChangedEventArgs e)
        {
            if (_loading || NpcSubList.SelectedItem is not NpcInstance npc) return;
            npc.NpcTid = NpcTidBox.Text;
            npc.X = NpcXBox.Text;
            npc.Y = NpcYBox.Text;
            npc.Face = NpcFaceBox.Text;
            npc.Model = NpcModelBox.Text;
            npc.Desc = NpcDescBox.Text;
            npc.RebuildRawLine();
        }

        private void MonsterField_Changed(object sender, TextChangedEventArgs e)
        {
            if (_loading || MonsterSubList.SelectedItem is not MonsterInstance mob) return;
            mob.MonsterTid = MonsterTidBox.Text;
            mob.PatrolId = MonsterPatrolBox.Text;
            mob.MaxAmount = MonsterAmountBox.Text;
            mob.RebornSeconds = MonsterRebornBox.Text;
            mob.X = MonsterXBox.Text;
            mob.Y = MonsterYBox.Text;
            mob.Face = MonsterFaceBox.Text;
            mob.RebuildRawLine();
        }

        private void SoundField_Changed(object sender, TextChangedEventArgs e)
        {
            if (_loading || SoundSubList.SelectedItem is not SoundInstance sound) return;
            sound.Time = SoundTimeBox.Text;
            sound.X = SoundXBox.Text;
            sound.Y = SoundYBox.Text;
            sound.SoundName = SoundNameBox.Text;
            sound.RebuildRawLine();
        }

        private void AddNpc_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentSceneFile)) return;
            
            // Find lowest HandleId (e.g. -1, -2...)
            int minId = 0;
            if (NpcInstances.Any()) {
                minId = NpcInstances.Min(n => int.TryParse(n.HandleId, out int id) ? id : 0);
            }
            int newId = Math.Min(-1, minId - 1);

            var npc = new NpcInstance {
                HandleId = newId.ToString(),
                NpcTid = "64040", // Default NPC TID
                X = "0",
                Y = "0",
                Face = "0",
                Model = "N006",
                Desc = "New NPC",
                RawLine = ""
            };
            npc.RebuildRawLine();
            npc.DisplayName = _npcNames.TryGetValue(npc.NpcTid, out var name) ? name : npc.Desc;
            NpcInstances.Add(npc);
            NpcSubList.SelectedItem = npc;
        }

        private void RemoveNpc_Click(object sender, RoutedEventArgs e)
        {
            if (NpcSubList.SelectedItem is NpcInstance npc)
            {
                NpcInstances.Remove(npc);
            }
        }

        private void AddMonster_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentSceneFile)) return;

            int maxId = 0;
            if (MonsterInstances.Any()) {
                maxId = MonsterInstances.Max(m => int.TryParse(m.HandleId, out int id) ? id : 0);
            }
            int newId = maxId + 1;

            var mob = new MonsterInstance {
                HandleId = newId.ToString(),
                MonsterTid = "55001",
                PatrolId = "0",
                MaxAmount = "1",
                RebornSeconds = "30",
                X = "0",
                Y = "0",
                Face = "0",
                RawLine = ""
            };
            mob.RebuildRawLine();
            mob.DisplayName = _monsterNames.TryGetValue(mob.MonsterTid, out var name) ? name : "New Monster";
            MonsterInstances.Add(mob);
            MonsterSubList.SelectedItem = mob;
        }

        private void RemoveMonster_Click(object sender, RoutedEventArgs e)
        {
            if (MonsterSubList.SelectedItem is MonsterInstance mob)
            {
                MonsterInstances.Remove(mob);
            }
        }

        private void AddSound_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentSceneFile)) return;

            int maxId = 0;
            if (SoundInstances.Any()) {
                maxId = SoundInstances.Max(s => int.TryParse(s.HandleId, out int id) ? id : 0);
            }
            int newId = maxId + 1;

            var sound = new SoundInstance {
                HandleId = newId.ToString(),
                Time = "50",
                X = "0",
                Y = "0",
                Face = "0",
                SoundName = "NewSound",
                RawLine = ""
            };
            sound.RebuildRawLine();
            SoundInstances.Add(sound);
            SoundSubList.SelectedItem = sound;
        }

        private void RemoveSound_Click(object sender, RoutedEventArgs e)
        {
            if (SoundSubList.SelectedItem is SoundInstance sound)
            {
                SoundInstances.Remove(sound);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentSceneFile)) return;

            var sb = new StringBuilder();
            void AppendSection(string header, string content) {
                // S*.ini headers typically have a comma
                if (!header.EndsWith(",")) header += ",";
                sb.AppendLine(header);
                if (!string.IsNullOrWhiteSpace(content)) sb.AppendLine(content.Trim());
            }

            AppendSection("[globe_data]", GlobeDataBox.Text);
            AppendSection("[born_area]", BornAreaBox.Text);
            AppendSection("[revive_area]", ReviveAreaBox.Text);
            AppendSection("[gateway_area]", GatewayAreaBox.Text);
            AppendSection("[normal_area]", NormalAreaBox.Text);
            AppendSection("[event_area]", EventAreaBox.Text);
            AppendSection("[position]", PositionBox.Text);
            AppendSection("[mob_patrol_mode]", PatrolModeBox.Text);
            
            sb.AppendLine("[reborn_monsters],");
            foreach (var mob in MonsterInstances) {
                mob.RebuildRawLine();
                sb.AppendLine(mob.RawLine);
            }
            
            sb.AppendLine("[npc],");
            foreach (var npc in NpcInstances) {
                npc.RebuildRawLine();
                sb.AppendLine(npc.RawLine);
            }
            
            sb.AppendLine("[environment_sound],");
            foreach (var sound in SoundInstances) {
                sound.RebuildRawLine();
                sb.AppendLine(sound.RawLine);
            }
            
            AppendSection("[event]", EventBox.Text);
            AppendSection("[dynamic_block]", DynamicBlockBox.Text);

            try {
                // Using 950 (Big5) for saving to match the game client's expected encoding
                File.WriteAllText(_currentSceneFile, sb.ToString(), Encoding.GetEncoding(950));
                MessageBox.Show("Salvo com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
            } catch (Exception ex) {
                MessageBox.Show("Erro ao salvar: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Classes AUX
        public class SceneEntry {
            public string Id { get; set; }
            public string FileName { get; set; }
            public string DisplayName { get; set; }
            public string FullPath { get; set; }
            public override string ToString() => string.IsNullOrWhiteSpace(DisplayName) ? $"{FileName}" : $"{Id} - {DisplayName}";
        }

        public class SceneData {
            public string GlobeData, BornArea, ReviveArea, GatewayArea, NormalArea, EventArea, Position, PatrolMode, RebornMonsters, Npc, EnvironmentSound, Event, DynamicBlock;
        }

        public class NpcInstance {
            public string HandleId { get; set; }
            public string NpcTid { get; set; }
            public string X { get; set; }
            public string Y { get; set; }
            public string Face { get; set; }
            public string Model { get; set; }
            public string Desc { get; set; }
            public string DisplayName { get; set; }
            public string RawLine { get; set; }
            public override string ToString() => $"{NpcTid} - {DisplayName}";

            public void RebuildRawLine() {
                // GF NPCs usually have 12 fields. 
                // -1,64041,148,286,5.49779,N006,½åªÌ¤Ú¤Úº¿,0,0,0,0,1,
                RawLine = $"{HandleId},{NpcTid},{X},{Y},{Face},{Model},{Desc},0,0,0,0,1,";
            }
        }

        public class MonsterInstance {
            public string HandleId { get; set; }
            public string MonsterTid { get; set; }
            public string PatrolId { get; set; }
            public string MaxAmount { get; set; }
            public string RebornSeconds { get; set; }
            public string X { get; set; }
            public string Y { get; set; }
            public string Face { get; set; }
            public string DisplayName { get; set; }
            public string RawLine { get; set; }
            public override string ToString() => $"{MonsterTid} - {DisplayName}";

            public void RebuildRawLine() {
                // Monsters typically have 24 fields. 
                // We'll preserve extra fields if they exist, otherwise fill with 0s.
                var parts = string.IsNullOrEmpty(RawLine) ? new string[25] : RawLine.Split(',');
                if (parts.Length < 25) {
                    var newParts = new string[25];
                    Array.Copy(parts, newParts, parts.Length);
                    for (int i = parts.Length; i < 25; i++) newParts[i] = "0";
                    parts = newParts;
                }

                parts[0] = HandleId;
                parts[1] = MonsterTid;
                parts[2] = PatrolId;
                parts[3] = MaxAmount;
                parts[4] = RebornSeconds;
                parts[5] = X;
                parts[6] = Y;
                parts[7] = Face;
                // Field 13 is often the Model ID (M002 etc) - we might want to expose this later
                // For now, let's ensure we don't clear it if it exists.
                
                RawLine = string.Join(",", parts.Take(24)) + ",";
            }
        }

        public class SoundInstance {
            public string HandleId { get; set; }
            public string Time { get; set; }
            public string X { get; set; }
            public string Y { get; set; }
            public string Face { get; set; }
            public string SoundName { get; set; }
            public string RawLine { get; set; }
            public override string ToString() => $"{SoundName}";

            public void RebuildRawLine() {
                // Sounds typically have 7 fields.
                // 1,40,274.432,217.326,-12.7271,WaterF,,
                RawLine = $"{HandleId},{Time},{X},{Y},{Face},{SoundName},,";
            }
        }
    }
}
