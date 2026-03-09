using System.Collections.Generic;

namespace GrandFantasiaINIEditor.Core
{
    public class ItemTranslation
    {
        public string Name;
        public string Description;
    }

    public class ItemDb
    {
        public Dictionary<string, List<string>> SRows = new();
        public Dictionary<string, ItemTranslation> TRows = new();
    }

    public static class ItemLoader
    {
        public static ItemDb Load(string clientRoot, string schemasPath)
        {
            var schemas = IniSchemaLoader.LoadSchemas(schemasPath);

            var db = new ItemDb();

            var sSchema = schemas.GetValueOrDefault("S_Item.ini");
            var tSchema = schemas.GetValueOrDefault("T_Item.ini");

            if (sSchema != null)
            {
                var res = PipeIniReader.Read(clientRoot, sSchema);

                if (res != null)
                {
                    foreach (var row in res.Rows)
                    {
                        var id = row[0];

                        if (!string.IsNullOrWhiteSpace(id))
                            db.SRows[id] = row;
                    }
                }
            }

            if (tSchema != null)
            {
                var res = PipeIniReader.Read(clientRoot, tSchema);

                if (res != null)
                {
                    foreach (var row in res.Rows)
                    {
                        if (row.Count < 3)
                            continue;

                        string id = row[0];

                        db.TRows[id] = new ItemTranslation
                        {
                            Name = row[1],
                            Description = row[2]
                        };
                    }
                }
            }

            return db;
        }
    }
}