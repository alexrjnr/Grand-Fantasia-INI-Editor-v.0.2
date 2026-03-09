namespace GrandFantasiaINIEditor.Core
{
    public class IniSchema
    {
        public string FileName { get; set; }
        public int Columns { get; set; }
        public string Kind { get; set; }

        public IniSchema(string fileName, int columns, string kind)
        {
            FileName = fileName;
            Columns = columns;
            Kind = kind;
        }
    }
}