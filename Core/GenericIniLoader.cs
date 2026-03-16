using System;
using System.Linq;

namespace GrandFantasiaINIEditor.Core
{
    public static class GenericIniLoader
    {
        public static GenericIniDb Load(string clientRoot, string schemasPath, string dataFileName, string translateFileName = null)
        {
            var schemas = IniSchemaLoader.LoadSchemas(schemasPath);

            if (!schemas.TryGetValue(dataFileName, out var dataSchema))
                throw new InvalidOperationException($"Schema não encontrado para {dataFileName}.");

            var dataRead = PipeIniReader.Read(clientRoot, dataSchema);

            if (dataRead == null)
            {
                dataRead = new IniFileResult
                {
                    FilePath = System.IO.Path.Combine(clientRoot, "data", "db", dataFileName)
                };
            }

            var db = new GenericIniDb
            {
                Schema = dataSchema,
                FilePath = dataRead.FilePath
            };

            foreach (var row in dataRead.Rows)
            {
                if (row == null || row.Count == 0)
                    continue;

                string id = row[0]?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                db.Rows[id] = row.Select(x => x?.Trim() ?? string.Empty).ToList();
            }

            if (!string.IsNullOrWhiteSpace(translateFileName))
            {
                if (!schemas.TryGetValue(translateFileName, out var transSchema))
                    throw new InvalidOperationException($"Schema de tradução não encontrado para {translateFileName}.");

                var transRead = PipeIniReader.Read(clientRoot, transSchema);

                if (transRead != null)
                {
                    db.TranslationSchema = transSchema;
                    db.TranslationFilePath = transRead.FilePath;

                    foreach (var row in transRead.Rows)
                    {
                        if (row == null || row.Count < 2)
                            continue;

                        string id = row[0]?.Trim();
                        if (string.IsNullOrWhiteSpace(id))
                            continue;

                        db.Translations[id] = new GenericTranslation
                        {
                            Name = row[1]?.Trim() ?? string.Empty,
                            Description = row.Count > 2 ? row[2].TrimEnd() : string.Empty
                        };
                    }
                }
            }

            return db;
        }
    }
}