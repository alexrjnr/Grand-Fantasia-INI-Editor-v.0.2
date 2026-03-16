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

            var rawLines = File.ReadAllLines(path, enc);

            var rows = new List<List<string>>();

            if (schema.Kind == "SC")
            {
                var lines = rawLines.ToList();
                if (lines.Count <= 1)
                    return new IniFileResult();

                lines.RemoveAt(0); // remove header

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
            }
            else
            {
                string currentId = null;
                string currentName = null;
                var currentDesc = new StringBuilder();

                void FlushCurrent()
                {
                    if (!string.IsNullOrWhiteSpace(currentId))
                    {
                        rows.Add(new List<string> { currentId, currentName, currentDesc.ToString() });
                    }
                }

                for (int i = 1; i < rawLines.Length; i++)
                {
                    string line = rawLines[i] ?? string.Empty;

                    int firstPipe = line.IndexOf('|');
                    bool startsWithId =
                        firstPipe > 0 &&
                        int.TryParse(line.Substring(0, firstPipe).Trim(), out _);

                    if (startsWithId)
                    {
                        FlushCurrent();

                        var parts = line.Split(new[] { '|' }, 3);

                        currentId = parts.Length > 0 ? parts[0].Trim() : string.Empty;
                        currentName = parts.Length > 1 ? parts[1].Trim() : string.Empty;

                        currentDesc.Clear();
                        if (parts.Length > 2)
                            currentDesc.Append(parts[2]);
                    }
                    else
                    {
                        if (currentId == null)
                            continue;

                        if (line == "|")
                            continue;

                        if (currentDesc.Length > 0)
                            currentDesc.AppendLine();

                        currentDesc.Append(line);
                    }
                }

                FlushCurrent();
            }

            return new IniFileResult
            {
                FilePath = path,
                Rows = rows
            };
        }
    }
}