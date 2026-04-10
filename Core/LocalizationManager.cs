using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;

namespace GrandFantasiaINIEditor.Core
{
    public class LocalizationManager : INotifyPropertyChanged
    {
        private static LocalizationManager _instance;
        public static LocalizationManager Instance => _instance ??= new LocalizationManager();

        private Dictionary<string, object> _localizedStrings = new();
        private Dictionary<string, JsonElement> _commandLegends = new();
        private string _currentLanguage = "pt-BR";

        public event PropertyChangedEventHandler PropertyChanged;

        private LocalizationManager()
        {
            LoadLanguage(_currentLanguage);
        }

        public string CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    LoadLanguage(_currentLanguage);
                    OnPropertyChanged(nameof(CurrentLanguage));
                    OnPropertyChanged("Item"); // Notify that all bindings might have changed
                }
            }
        }

        public void LoadLanguage(string langCode)
        {
            try
            {
                _currentLanguage = langCode;
                _localizedStrings.Clear();
                _commandLegends.Clear();

                // Load main language file
                string mainPath = GetFilePath($"{langCode}.json");
                if (File.Exists(mainPath))
                {
                    string json = File.ReadAllText(mainPath);
                    var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    if (data != null)
                    {
                        _localizedStrings = data;
                    }
                }

                // Load auxiliary commands file
                string commandsPath = GetFilePath($"commands.{langCode}.json");
                if (File.Exists(commandsPath))
                {
                    string json = File.ReadAllText(commandsPath);
                    var commandsData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                    if (commandsData != null)
                    {
                        foreach (var kvp in commandsData)
                        {
                            _commandLegends[kvp.Key] = kvp.Value;
                        }
                    }
                }
                
                OnPropertyChanged("Item");
            }
            catch (Exception)
            {
                // Silently fails on design-time
            }
        }

        public Dictionary<string, JsonElement> GetCommandLegends()
        {
            return _commandLegends;
        }

        private string GetFilePath(string fileName)
        {
            // Try base directory first (where the EXE is)
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string path = Path.Combine(baseDir, "Lang", fileName);
            if (File.Exists(path)) return path;

            // Try walking up from base directory (useful for Debug/netX.X-windows folders)
            var current = new DirectoryInfo(baseDir);
            while (current != null)
            {
                path = Path.Combine(current.FullName, "Lang", fileName);
                if (File.Exists(path)) return path;
                current = current.Parent;
            }

            // Try current working directory as last resort
            path = Path.Combine(Directory.GetCurrentDirectory(), "Lang", fileName);
            return path;
        }

        public string GetLocalizedString(string locKey)
        {
            if (string.IsNullOrWhiteSpace(locKey)) return string.Empty;
            if (_localizedStrings.Count == 0) return $"[NO_DATA:{locKey}]";

            try
            {
                string[] parts = locKey.Split('.');
                object current = _localizedStrings;

                foreach (var part in parts)
                {
                    if (string.IsNullOrWhiteSpace(part)) return $"[{locKey}]";

                    if (current is Dictionary<string, object> dict)
                    {
                        if (dict.TryGetValue(part, out var next))
                            current = next;
                        else
                            return $"[{locKey}]";
                    }
                    else if (current is JsonElement element)
                    {
                        if (element.ValueKind == JsonValueKind.Object)
                        {
                            if (element.TryGetProperty(part, out var property))
                                current = property;
                            else
                                return $"[{locKey}]";
                        }
                        else return $"[{locKey}]";
                    }
                    else return $"[{locKey}]";
                }

                if (current is JsonElement finalElement)
                {
                    if (finalElement.ValueKind == JsonValueKind.String)
                        return finalElement.GetString() ?? string.Empty;
                    return finalElement.ToString();
                }

                return current?.ToString() ?? $"[{locKey}]";
            }
            catch
            {
                return $"[ERR:{locKey}]";
            }
        }

        public Dictionary<string, string> GetDictionary(string key)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(key)) return result;

            try
            {
                string[] parts = key.Split('.');
                object current = _localizedStrings;

                foreach (var part in parts)
                {
                    if (string.IsNullOrWhiteSpace(part)) return result;

                    if (current is Dictionary<string, object> dict)
                    {
                        if (dict != null && dict.TryGetValue(part, out var next))
                            current = next;
                        else
                            return result;
                    }
                    else if (current is JsonElement element && element.ValueKind == JsonValueKind.Object)
                    {
                        if (element.TryGetProperty(part, out var property))
                            current = property;
                        else return result;
                    }
                    else return result;
                }

                if (current is Dictionary<string, object> finalDict)
                {
                    foreach (var kv in finalDict)
                        if (kv.Key != null)
                            result[kv.Key] = kv.Value?.ToString() ?? string.Empty;
                }
                else if (current is JsonElement finalElement && finalElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in finalElement.EnumerateObject())
                        result[prop.Name] = prop.Value.ToString();
                }
            }
            catch
            {
                // Silently return empty on error
            }

            return result;
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class LocConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string key)
            {
                return LocalizationManager.Instance.GetLocalizedString(key);
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
