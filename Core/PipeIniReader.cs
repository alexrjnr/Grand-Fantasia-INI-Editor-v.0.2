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
        public string VersionLine;
        public string ColumnHeader;
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
            var result = new IniFileResult { FilePath = path };

            if (rawLines.Length > 0)
            {
                if (rawLines[0].Trim().StartsWith("#"))
                {
                    result.VersionLine = rawLines[0];
                    if (rawLines.Length > 1) result.ColumnHeader = rawLines[1];
                }
                else
                {
                    result.ColumnHeader = rawLines[0];
                }
            }

            var rows = new List<List<string>>();

            if (schema.Kind == "SC")
            {
                // Read the whole file to handle multi-line records correctly
                string allContent = File.ReadAllText(path, enc);
                
                // Group lines to preserve the header rule
                var allLines = rawLines.ToList();
                if (allLines.Count <= 1) return new IniFileResult();
                
                // Join everything AFTER the header line
                // We use the rawLines already read to skip the first line accurately
                int skipLines = !string.IsNullOrEmpty(result.VersionLine) ? 2 : 1;
                string dataBody = string.Join("\n", allLines.Skip(skipLines));
                
                // Split by Pipe
                string[] allFields = dataBody.Split('|');
                
                // Each record has exactly schema.Columns fields
                for (int i = 0; i + schema.Columns <= allFields.Length; i += schema.Columns)
                {
                    var row = new List<string>();
                    for (int j = 0; j < schema.Columns; j++)
                    {
                        row.Add(allFields[i + j].Trim());
                    }
                    
                    if (!string.IsNullOrWhiteSpace(row[0]))
                    {
                        rows.Add(row);
                    }
                }
            }
            else
            {
                string currentId = null;
                List<string> currentValues = new List<string>();
                var currentLastField = new StringBuilder();

                void FlushCurrent()
                {
                    if (!string.IsNullOrWhiteSpace(currentId))
                    {
                        if (currentLastField.Length > 0)
                        {
                            currentValues.Add(currentLastField.ToString());
                            currentLastField.Clear();
                        }
                        rows.Add(currentValues);
                    }
                }

                int startFrom = !string.IsNullOrEmpty(result.VersionLine) ? 2 : 1;
                for (int i = startFrom; i < rawLines.Length; i++)
                {
                    string line = rawLines[i] ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = line.Split('|').ToList();
                    // Handle leading pipe
                    if (parts.Count > 0 && string.IsNullOrWhiteSpace(parts[0]))
                    {
                        parts.RemoveAt(0);
                    }

                    bool startsWithId = parts.Count > 0 && int.TryParse(parts[0].Trim(), out _);

                    if (startsWithId)
                    {
                        FlushCurrent();
                        currentId = parts[0].Trim();
                        currentValues = parts.Select(p => p.Trim()).ToList();
                        
                        // If there was a trailing pipe, the last part might be empty
                        if (currentValues.Count > 1 && string.IsNullOrWhiteSpace(currentValues.Last()))
                        {
                            currentValues.RemoveAt(currentValues.Count - 1);
                        }
                    }
                    else
                    {
                        if (currentId == null) continue;
                        if (currentValues.Count > 0)
                        {
                            // Append to the last column (Description/Max field)
                            int lastIdx = currentValues.Count - 1;
                            currentValues[lastIdx] += "\n" + line.Trim();
                        }
                    }
                }

                FlushCurrent();
            }

            result.Rows = rows;
            return result;
        }
    }
}