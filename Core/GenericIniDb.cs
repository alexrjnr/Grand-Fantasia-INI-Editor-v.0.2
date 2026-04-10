using System.Collections.Generic;

namespace GrandFantasiaINIEditor.Core
{
    public class GenericTranslation
    {
        public List<string> Values = new();
        public string Name
        {
            get => Values.Count > 1 ? Values[1] : string.Empty;
            set { while (Values.Count <= 1) Values.Add(string.Empty); Values[1] = value; }
        }
        public string Description
        {
            get => Values.Count > 2 ? Values[2] : string.Empty;
            set { while (Values.Count <= 2) Values.Add(string.Empty); Values[2] = value; }
        }
    }

    public class GenericIniDb
    {
        public Dictionary<string, List<string>> Rows = new();
        public Dictionary<string, GenericTranslation> Translations = new();
        public IniSchema Schema;
        public IniSchema TranslationSchema;
        public string FilePath;
        public string TranslationFilePath;
        public string VersionLine;
        public string ColumnHeader;
    }
}