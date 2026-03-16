using System.Collections.Generic;

namespace GrandFantasiaINIEditor.Core
{
    public class GenericTranslation
    {
        public string Name;
        public string Description;
    }

    public class GenericIniDb
    {
        public Dictionary<string, List<string>> Rows = new();
        public Dictionary<string, GenericTranslation> Translations = new();
        public IniSchema Schema;
        public IniSchema TranslationSchema;
        public string FilePath;
        public string TranslationFilePath;
    }
}