// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Noël Danjou

using System.Text;

namespace AddressDiff;

// Minimal CSV helpers: quote-aware line parsing, header indexing, and
// semicolon output using the OS newline.
static class Csv
{
    public static List<string> ParseLine(string line, char separator)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == separator) { fields.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }

        fields.Add(sb.ToString());
        return fields;
    }

    // Map header labels to their column index, case-insensitively.
    public static Dictionary<string, int> Header(List<string> header)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Count; i++)
            map.TryAdd(header[i].Trim(), i);
        return map;
    }

    // Index of the first matching column name, or -1. Accepts aliases so a
    // reader can tolerate format variants.
    public static int Column(Dictionary<string, int> index, params string[] names)
    {
        foreach (var name in names)
            if (index.TryGetValue(name, out var i)) return i;
        return -1;
    }

    public static string Field(List<string> fields, int index) =>
        index >= 0 && index < fields.Count ? fields[index].Trim() : "";

    public static void Write(string path, string[] header, IEnumerable<string[]> rows)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false)) { NewLine = Environment.NewLine };
        writer.WriteLine(string.Join(';', header.Select(Escape)));
        foreach (var row in rows)
            writer.WriteLine(string.Join(';', row.Select(Escape)));
    }

    static string Escape(string? value)
    {
        var v = value ?? "";
        return v.Contains(';') || v.Contains('"') || v.Contains('\n')
            ? $"\"{v.Replace("\"", "\"\"")}\""
            : v;
    }
}
