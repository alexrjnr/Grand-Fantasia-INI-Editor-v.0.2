    using System.Collections.Generic;
    using System.IO;

    namespace GrandFantasiaINIEditor.Core
    {
        public static class IniSchemaLoader
        {
            public static Dictionary<string, IniSchema> LoadSchemas(string path)
            {
                var dict = new Dictionary<string, IniSchema>();

                foreach (var line in File.ReadAllLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var parts = line.Split('|');

                    if (parts.Length < 3)
                        continue;

                    string file = parts[0];
                    int cols = int.Parse(parts[1]);
                    string kind = parts[2];

                    dict[file] = new IniSchema(file, cols, kind);
                }

                return dict;
            }
        }
    }