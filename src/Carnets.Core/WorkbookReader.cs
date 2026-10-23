using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace Carnets.Core;

public sealed record CellData(string Raw, string Display, bool Numeric = false, bool Padded = false, bool IsoDate = false, string? Error = null)
{
    public static readonly CellData Empty = new("", "");
}
public sealed record SheetRow(int Number, Dictionary<int, CellData> Cells);
public sealed record SheetData(string Name, IReadOnlyList<string> Headers, IReadOnlyList<SheetRow> Rows, bool Date1904);
public sealed record SheetInfo(string Name, string Part);

/// <summary>Read-only SpreadsheetML reader. Never evaluates formulas, links, macros or connections.</summary>
public sealed class WorkbookReader : IDisposable
{
    private readonly ZipArchive archive;
    private readonly string[] strings;
    private readonly string[] formats;
    public IReadOnlyList<SheetInfo> Sheets { get; }
    public bool Date1904 { get; }
    private static IEnumerable<XElement> Children(XContainer node, string name) => node.Elements().Where(e => e.Name.LocalName == name);
    private static IEnumerable<XElement> Desc(XContainer node, string name) => node.Descendants().Where(e => e.Name.LocalName == name);
    private static string? Attr(XElement element, string name) => element.Attributes().FirstOrDefault(a => a.Name.LocalName == name)?.Value;
    public WorkbookReader(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Seleccione un libro .xlsx; no se admiten otros formatos.");
        try { archive = ZipFile.OpenRead(path); }
        catch (Exception ex) when (ex is InvalidDataException or IOException) { throw new InvalidDataException("No se pudo leer el libro. Compruebe que no esté cifrado, dañado o bloqueado.", ex); }
        try
        {
            var workbook = Read("xl/workbook.xml");
            Date1904 = Desc(workbook, "workbookPr").Any(e => Attr(e, "date1904") is "1" or "true");
            var rels = Desc(Read("xl/_rels/workbook.xml.rels"), "Relationship").Where(e => Attr(e, "TargetMode") != "External").ToDictionary(e => Attr(e, "Id")!, e => e);
            Sheets = Desc(workbook, "sheet").Where(e => rels.ContainsKey(Attr(e, "id") ?? "")).Select(e =>
            {
                var relation = rels[Attr(e, "id")!];
                var target = Attr(relation, "Target") ?? throw new InvalidDataException("Hoja sin ubicación.");
                var part = new Uri(new Uri("https://local/xl/workbook.xml"), target).AbsolutePath.TrimStart('/');
                return (Info: new SheetInfo(Attr(e, "name")!, part), Type: Attr(relation, "Type"));
            }).Where(x => x.Type?.EndsWith("/worksheet", StringComparison.Ordinal) == true).Select(x => x.Info).ToList();
            if (Sheets.Count == 0) throw new InvalidDataException("El libro no contiene hojas de cálculo legibles.");
            strings = archive.GetEntry("xl/sharedStrings.xml") is null ? [] : Desc(Read("xl/sharedStrings.xml"), "si").Select(e => string.Concat(Desc(e, "t").Select(t => t.Value))).ToArray();
            if (archive.GetEntry("xl/styles.xml") is not null)
            {
                var styles = Read("xl/styles.xml");
                var custom = Desc(styles, "numFmt").ToDictionary(e => Attr(e, "numFmtId")!, e => Attr(e, "formatCode")!);
                formats = Desc(styles, "cellXfs").SelectMany(e => Children(e, "xf")).Select(e => custom.GetValueOrDefault(Attr(e, "numFmtId") ?? "", "")).ToArray();
            }
            else formats = [];
        }
        catch { archive.Dispose(); throw; }
    }
    private XDocument Read(string part)
    {
        var entry = archive.GetEntry(part) ?? throw new InvalidDataException("Estructura de libro incompleta.");
        if (entry.Length > 64 * 1024 * 1024) throw new InvalidDataException("Una parte del libro excede el límite de 64 MB.");
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 64 * 1024 * 1024 });
        return XDocument.Load(reader);
    }
    public SheetData Load(string name)
    {
        var sheet = Sheets.SingleOrDefault(s => s.Name == name) ?? throw new InvalidDataException("La hoja seleccionada no existe.");
        var rows = new List<SheetRow>();
        foreach (var row in Desc(Read(sheet.Part), "row"))
        {
            int number = int.TryParse(Attr(row, "r"), out int n) ? n : rows.Count + 1;
            var cells = new Dictionary<int, CellData>();
            int next = 0;
            foreach (var cell in Children(row, "c"))
            {
                string reference = Attr(cell, "r") ?? "";
                int col = 0;
                foreach (char c in reference.TakeWhile(char.IsLetter)) col = checked(col * 26 + char.ToUpperInvariant(c) - 'A' + 1);
                col = col == 0 ? next : col - 1;
                if (col > 16383) throw new InvalidDataException("Columna fuera del límite de Excel.");
                next = col + 1;
                string type = Attr(cell, "t") ?? "n";
                string raw = Children(cell, "v").FirstOrDefault()?.Value ?? "";
                string value = raw;
                string? error = null;
                if (type == "s") value = int.TryParse(raw, out int idx) && idx >= 0 && idx < strings.Length ? strings[idx] : throw new InvalidDataException("Índice de texto inválido en Excel.");
                if (type == "inlineStr") value = string.Concat(Desc(cell, "t").Select(t => t.Value));
                bool numeric = type == "n" && raw.Length > 0;
                bool padded = false;
                string format = int.TryParse(Attr(cell, "s"), out int style) && style >= 0 && style < formats.Length ? formats[style] : "";
                // An explicit all-zero identifier mask preserves leading zeroes; date styles never turn IDs into dates.
                if (numeric && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double numberValue))
                {
                    value = numberValue.ToString("G17", CultureInfo.InvariantCulture);
                    string section = format.Split(';')[0];
                    if (Regex.IsMatch(section, "^0{2,}$") && numberValue == Math.Truncate(numberValue)) { value = numberValue.ToString(section, CultureInfo.InvariantCulture); padded = true; }
                }
                if (type == "e") error = "Excel contiene un error; corrija la celda en el archivo original.";
                if (Children(cell, "f").Any() && raw.Length == 0) error = "Fórmula sin valor calculado disponible; calcule y guarde el Excel antes de cargarlo.";
                cells[col] = new(raw, value, numeric, padded, type == "d", error);
            }
            if (number == 1 || cells.Values.Any(c => !string.IsNullOrWhiteSpace(c.Display) || c.Error is not null)) rows.Add(new(number, cells));
        }
        var header = rows.SingleOrDefault(r => r.Number == 1) ?? throw new InvalidDataException("Faltan los encabezados en la primera fila.");
        int count = header.Cells.Count == 0 ? 0 : header.Cells.Keys.Max() + 1;
        if (count == 0) throw new InvalidDataException("La primera fila no tiene encabezados.");
        return new(name, Enumerable.Range(0, count).Select(i => header.Cells.GetValueOrDefault(i, CellData.Empty).Display).ToList(), rows.Where(r => r.Number != 1).ToList(), Date1904);
    }
    public void Dispose() => archive.Dispose();
}
