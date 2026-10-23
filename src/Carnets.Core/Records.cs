using System.Globalization;
using System.Text;

namespace Carnets.Core;

public static class Fields
{
    public static readonly string[] Keys = ["nombre", "apellido", "cedula", "matricula", "filial", "segundoNombre", "segundoApellido", "numeroExequatur", "fechaAdmision", "fechaExpiracion", "referenciaFoto"];
    public static readonly string[] Headers = ["NOMBRE", "APELLIDO", "CEDULA", "MATRÍCULA", "FILIAL", "2do NOMBRE", "2do APELLIDO", "NO. EXEQUATUR", "FECHA DE ADMISION", "FECHA DE EXPIRACION", "FOTOS"];
    public static readonly string[] Labels = ["Nombre", "Apellido", "Cédula", "Matrícula", "Provincia", "2do. Nombre", "2do. Apellido", "No. Exequátur", "Fecha de Admisión", "Fecha de Expiración", "Fotografía"];
    public static string Normalize(string value) => string.Concat(value.Trim().Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToUpperInvariant().Normalize();
    public static Dictionary<string, int> Suggest(IReadOnlyList<string> headers)
    {
        var result = new Dictionary<string, int>();
        for (int i = 0; i < Keys.Length; i++)
        {
            var matches = Enumerable.Range(0, headers.Count).Where(n => Normalize(headers[n]) == Normalize(Headers[i])).ToArray();
            if (matches.Length == 1) result[Keys[i]] = matches[0];
        }
        return result;
    }
}

public sealed record Issue(string Field, string Message, bool Warning = false);
public sealed class Holder
{
    public int Row { get; init; }
    public string Sheet { get; init; } = "";
    public Dictionary<string, string> Values { get; init; } = [];
    public List<Issue> Issues { get; } = [];
    public string this[string key] => Values.GetValueOrDefault(key, "");
    public string Matricula => this["matricula"];
    public string Name => $"{this["nombre"]} {this["apellido"]}";
    public DateOnly? Admission { get; set; }
    public DateOnly? Expiration { get; set; }
    public bool IsExpired(DateOnly today) => Expiration < today;
    public bool IsValid => Issues.All(i => i.Warning);
    public bool Matches(string query) => Fields.Normalize(string.Join(' ', new[] { Matricula, this["cedula"], this["nombre"], this["apellido"], this["segundoNombre"], this["segundoApellido"] })).Contains(Fields.Normalize(query), StringComparison.Ordinal);
}

public static class HolderValidation
{
    public static List<Holder> Load(SheetData sheet, IReadOnlyDictionary<string, int> mapping, DateOnly today, string dateFormat = "dd/MM/yyyy")
    {
        if (Fields.Keys.Any(k => !mapping.TryGetValue(k, out int index) || index < 0 || index >= sheet.Headers.Count) || mapping.Values.Distinct().Count() != Fields.Keys.Length)
            throw new InvalidDataException("Asocie los once campos a columnas diferentes antes de continuar.");
        var result = new List<Holder>();
        foreach (var row in sheet.Rows)
        {
            var holder = new Holder { Row = row.Number, Sheet = sheet.Name };
            foreach (string key in Fields.Keys)
            {
                var cell = row.Cells.GetValueOrDefault(mapping[key], CellData.Empty);
                holder.Values[key] = cell.Display.Trim();
                if (cell.Error is not null) holder.Issues.Add(new(key, cell.Error));
                if (key is "matricula" or "cedula" or "numeroExequatur" && cell.Numeric && !cell.Padded)
                    holder.Issues.Add(new(key, "Origen numérico: confirme los ceros iniciales en Excel; no se agregaron ceros.", true));
                if (key is "fechaAdmision" or "fechaExpiracion")
                {
                    DateOnly? date = null;
                    if (cell.Numeric && double.TryParse(cell.Raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double serial))
                    {
                        try
                        {
                            if (!sheet.Date1904 && serial >= 60 && serial < 61) throw new ArgumentException();
                            date = DateOnly.FromDateTime(sheet.Date1904 ? new DateTime(1904, 1, 1).AddDays(serial) : new DateTime(1899, 12, 31).AddDays(serial >= 60 ? serial - 1 : serial));
                        }
                        catch (ArgumentException) { }
                    }
                    else if (DateOnly.TryParseExact(cell.Display.Trim(), cell.IsoDate ? "yyyy-MM-dd" : dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) date = parsed;
                    if (date is null) holder.Issues.Add(new(key, $"Fecha inválida. Use {dateFormat}."));
                    else holder.Values[key] = date.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
                    if (key == "fechaAdmision") holder.Admission = date; else holder.Expiration = date;
                }
                else if (key is "segundoNombre" or "segundoApellido")
                {
                    if (string.IsNullOrWhiteSpace(holder.Values[key])) holder.Values[key] = "-";
                }
                else if (key != "referenciaFoto" && string.IsNullOrWhiteSpace(holder.Values[key])) holder.Issues.Add(new(key, "Campo obligatorio vacío."));
            }
            if (holder.Expiration < holder.Admission) holder.Issues.Add(new("fechaExpiracion", "La expiración es anterior a la admisión."));
            if (holder.IsExpired(today)) holder.Issues.Add(new("fechaExpiracion", "Carnet vencido. Requiere reconocimiento antes de imprimir.", true));
            result.Add(holder);
        }
        foreach (var group in result.Where(h => h.Matricula.Length > 0).GroupBy(h => h.Matricula, StringComparer.Ordinal).Where(g => g.Count() > 1))
            foreach (var holder in group) holder.Issues.Add(new("matricula", "Matrícula duplicada; corrija todas las filas afectadas en Excel."));
        return result;
    }
}

public sealed class Selection
{
    // Row identity is separate from the business identifier so duplicate IDs can be shown and blocked.
    private readonly HashSet<Holder> selected = [];
    public IReadOnlyCollection<Holder> Items => selected;
    public void Set(Holder holder, bool value) { if (value) selected.Add(holder); else selected.Remove(holder); }
    public void Clear() => selected.Clear();
    public static int ParseCopies(string text) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int n) && n is >= 1 and <= 100 ? n : throw new InvalidDataException("Copias debe ser un entero entre 1 y 100.");
}
