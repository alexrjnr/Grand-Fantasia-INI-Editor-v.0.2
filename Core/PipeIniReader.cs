using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GrandFantasiaINIEditor.Core
{
    public class IniFileResult
    {
        public string FilePath;
        public List<List<string>> Rows = new();
    }

    public static class PipeIniReader
    {
        static readonly Encoding BIG5 = Encoding.GetEncoding(950);
        static readonly Encoding ANSI = Encoding.GetEncoding(1252);

        static List<string> ResolvePaths(string baseDir, string file)
        {
            return new List<string>
            {
                Path.Combine(baseDir, file),
                Path.Combine(baseDir,"data","db",file),
                Path.Combine(baseDir,"data","translate",file),
                Path.Combine(baseDir,"data","Translate",file)
            };
        }

        public static IniFileResult Read(string baseDir, IniSchema schema)
        {
            var path = ResolvePaths(baseDir, schema.FileName)
                .FirstOrDefault(File.Exists);

            if (path == null)
                return null;

            Encoding enc = schema.Kind == "SC" ? BIG5 : ANSI;

            var lines = File.ReadAllLines(path, enc).ToList();

            if (schema.Kind == "SC")
            {
                if (lines.Count <= 1)
                    return new IniFileResult();

                lines.RemoveAt(0); // remove header
            }

            var rows = new List<List<string>>();

            foreach (var line in lines)
            {
                var cols = line.Split('|');

                if (cols.Length < schema.Columns)
                    continue;

                var row = new List<string>();

                for (int i = 0; i < schema.Columns; i++)
                    row.Add(cols[i].Trim());

                if (string.IsNullOrWhiteSpace(row[0]))
                    continue;

                rows.Add(row);
            }

            return new IniFileResult
            {
                FilePath = path,
                Rows = rows
            };
        }
    }
}