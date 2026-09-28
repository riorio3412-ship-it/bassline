using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace BASSLINE.Authoring
{
    public sealed class CsvRow
    {
        public readonly Dictionary<string, string> Fields;
        public readonly int RecordNumber;
        public CsvRow(Dictionary<string, string> fields, int recordNumber) { Fields = fields; RecordNumber = recordNumber; }
        public string this[string key] => Fields.TryGetValue(key, out var value) ? value : throw new FormatException("Missing column: " + key);
        public double Number(string key)
        {
            if (!double.TryParse(this[key], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || double.IsNaN(value) || double.IsInfinity(value))
                throw new FormatException("Invalid finite number at record " + RecordNumber + ": " + key);
            return value;
        }
        public string[] List(string key) => this[key].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
    }
    public static class CsvTable
    {
        // Strict quoted CSV, UTF-8 BOM, CRLF and quoted multiline fields. No silent column shifts.
        public static List<CsvRow> Read(string text)
        {
            var records = new List<List<string>>(); var record = new List<string>(); var field = new StringBuilder();
            bool quoted = false, afterQuote = false;
            text = text.TrimStart('\uFEFF');
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; } else { quoted = false; afterQuote = true; } }
                    else field.Append(c);
                    continue;
                }
                if (c == '"')
                {
                    if (field.Length != 0 || afterQuote) throw new FormatException("Unexpected quote");
                    quoted = true; continue;
                }
                if (c == ',' || c == '\r' || c == '\n')
                {
                    record.Add(field.ToString()); field.Clear(); afterQuote = false;
                    if (c != ',')
                    {
                        if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                        if (!(record.Count == 1 && record[0] == "")) records.Add(record);
                        record = new List<string>();
                    }
                    continue;
                }
                if (afterQuote) throw new FormatException("Unexpected character after closing quote");
                field.Append(c);
            }
            if (quoted) throw new FormatException("Unterminated quoted field");
            if (field.Length > 0 || afterQuote || record.Count > 0) { record.Add(field.ToString()); records.Add(record); }
            if (records.Count == 0) throw new FormatException("Empty CSV");
            var header = records[0];
            if (header.Any(string.IsNullOrWhiteSpace) || header.Distinct(StringComparer.Ordinal).Count() != header.Count)
                throw new FormatException("Empty or duplicate header");
            var rows = new List<CsvRow>();
            for (int r = 1; r < records.Count; r++)
            {
                if (records[r].Count != header.Count) throw new FormatException("Column count at record " + (r + 1));
                var fields = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int c = 0; c < header.Count; c++) fields.Add(header[c], records[r][c]);
                rows.Add(new CsvRow(fields, r + 1));
            }
            return rows;
        }
        public static List<CsvRow> Load(string path) => Read(File.ReadAllText(path, Encoding.UTF8));
        public static string Canonical(CsvRow row) => string.Join("\n", row.Fields.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => x.Key.Length + ":" + x.Key + x.Value.Length + ":" + x.Value));
    }
}
