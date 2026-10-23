using System.IO;
using System.Text.Json;
using Carnets.Core;

namespace Carnets.Desktop;

public sealed class FieldLayout
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 28;
    public double Height { get; set; } = 7;
    public double Points { get; set; } = 8;
    public double LabelPoints { get; set; } = 4.8;
}
public sealed record FixedElement(double X, double Y, double Width, double Height, double Points = 6);
public sealed class CardProfile
{
    public int SchemaVersion { get; set; } = 1;
    public double WidthMm { get; set; } = 85.6;
    public double HeightMm { get; set; } = 53.98;
    public string Font { get; set; } = "Arial";
    public string Background { get; set; } = "#263F82";
    public string Institution { get; set; } = "INSTITUTO DE CONTADORES PÚBLICOS AUTORIZADOS\nDE LA REPÚBLICA DOMINICANA";
    public string Legend { get; set; } = "ESTA CREDENCIAL IDENTIFICA AL TITULAR\nCOMO MIEMBRO DEL ICPARD";
    public string President { get; set; } = "Lic. Bacilio Sánchez";
    public string Secretary { get; set; } = "Lic. Jesús Mejía Álvarez";
    public string PresidentRole { get; set; } = "Presidente Nacional";
    public string SecretaryRole { get; set; } = "Secretario General";
    public string MemberLegend { get; set; } = "CARNET\nDE MIEMBRO";
    public string BackTitleText { get; set; } = "ICPARD";
    public string BackBackground { get; set; } = "#FFFFFF";
    public Dictionary<string, FixedElement> Fixed { get; set; } = FixedDefaults();
    public string Logo { get; set; } = "";
    public string PresidentSignature { get; set; } = "";
    public string SecretarySignature { get; set; } = "";
    public bool ArtworkApproved { get; set; }
    public double PhotoX { get; set; } = 2;
    public double PhotoY { get; set; } = 25;
    public double PhotoWidth { get; set; } = 22;
    public double PhotoHeight { get; set; } = 27;
    public List<FieldLayout> Fields { get; set; } = [];
    private static Dictionary<string, FixedElement> FixedDefaults() => new()
    {
        ["logo"] = new(5, 2, 16, 14, 7), ["institution"] = new(23, 5, 61, 10, 6.8), ["memberLegend"] = new(2, 18, 22, 7, 6.5),
        ["backTitle"] = new(4, 2, 77.6, 14, 30), ["backBand"] = new(3, 17, 79.6, 12), ["backLegend"] = new(4, 19, 77.6, 9, 7.4),
        ["presidentSignature"] = new(9, 32, 29, 10), ["secretarySignature"] = new(47, 32, 29, 10),
        ["presidentName"] = new(6, 44, 35, 4, 5.5), ["secretaryName"] = new(44, 44, 35, 4, 5.5),
        ["presidentRole"] = new(6, 48, 35, 3, 5), ["secretaryRole"] = new(44, 48, 35, 3, 5)
    };
    public static CardProfile Default()
    {
        var profile = new CardProfile();
        string[] keys = ["nombre", "segundoNombre", "apellido", "segundoApellido", "cedula", "numeroExequatur", "matricula", "fechaAdmision", "filial", "fechaExpiracion"];
        for (int i = 0; i < keys.Length; i++) profile.Fields.Add(new() { Key = keys[i], Label = Core.Fields.Labels[Array.IndexOf(Core.Fields.Keys, keys[i])], X = i % 2 == 0 ? 25.8 : 55.5, Y = 17 + i / 2 * 7.1, Width = 28.4, Height = 6.9 });
        return profile;
    }
    public void Validate(string directory)
    {
        if (SchemaVersion != 1 || WidthMm != 85.6 || HeightMm != 53.98 || Fields is null || Fields.Count != 10 || Fields.Any(f => f is null || f.Label is null) || Fields.Select(f => f.Key).Distinct().Count() != 10 || Core.Fields.Keys.Where(k => k != "referenciaFoto").Except(Fields.Select(f => f.Key)).Any() || string.IsNullOrWhiteSpace(Font) || Institution is null || Legend is null || President is null || Secretary is null || Logo is null || PresidentSignature is null || SecretarySignature is null || PresidentRole is null || SecretaryRole is null) throw new InvalidDataException("Perfil de diseño incompatible.");
        if (new[] { PhotoX, PhotoY, PhotoWidth, PhotoHeight }.Any(v => !double.IsFinite(v)) || PhotoX < 0 || PhotoY < 0 || PhotoWidth <= 0 || PhotoHeight <= 0 || PhotoX + PhotoWidth > WidthMm || PhotoY + PhotoHeight > HeightMm || Fields.Any(f => new[] { f.X, f.Y, f.Width, f.Height, f.Points }.Any(v => !double.IsFinite(v)) || f.X < 0 || f.Y < 0 || f.Width <= 1 || f.Height < 6 || f.X + f.Width > WidthMm || f.Y + f.Height > HeightMm || f.Points < 7)) throw new InvalidDataException("Geometría inválida en el perfil de diseño.");
        _ = System.Windows.Media.ColorConverter.ConvertFromString(Background);
        _ = System.Windows.Media.ColorConverter.ConvertFromString(BackBackground);
        if (MemberLegend is null || BackTitleText is null || Fixed is null || FixedDefaults().Keys.Except(Fixed.Keys).Any() || Fixed.Values.Any(f => f is null || new[] { f.X, f.Y, f.Width, f.Height, f.Points }.Any(v => !double.IsFinite(v)) || f.X < 0 || f.Y < 0 || f.Width <= 0 || f.Height <= 0 || f.X + f.Width > WidthMm || f.Y + f.Height > HeightMm || f.Points <= 0) || Fields.Any(f => !double.IsFinite(f.LabelPoints) || f.LabelPoints <= 0)) throw new InvalidDataException("Elementos fijos inválidos en el perfil.");
        var photoBounds = new System.Windows.Rect(PhotoX, PhotoY, PhotoWidth, PhotoHeight);
        var fieldBounds = Fields.Select(f => new System.Windows.Rect(f.X, f.Y, f.Width, f.Height)).ToArray();
        if (fieldBounds.Any(r => r.IntersectsWith(photoBounds)) || fieldBounds.SelectMany((r, i) => fieldBounds.Skip(i + 1).Select(other => r.IntersectsWith(other))).Any(overlap => overlap)) throw new InvalidDataException("Los campos o la foto se superponen en el perfil.");
        foreach (string asset in new[] { Logo, PresidentSignature, SecretarySignature }.Where(s => !string.IsNullOrEmpty(s)))
        {
            string full = Path.GetFullPath(Path.Combine(directory, asset));
            if (Path.IsPathRooted(asset) || !full.StartsWith(Path.GetFullPath(directory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) throw new InvalidDataException("Recurso de diseño ausente o fuera de la carpeta del perfil.");
        }
        if (ArtworkApproved && new[] { Logo, PresidentSignature, SecretarySignature }.Any(string.IsNullOrWhiteSpace)) throw new InvalidDataException("Un perfil aprobado requiere logo y ambas firmas originales.");
    }
    public static CardProfile Load(string path)
    {
        var p = JsonSerializer.Deserialize<CardProfile>(File.ReadAllText(path)) ?? throw new InvalidDataException("Perfil vacío.");
        p.Validate(Path.GetDirectoryName(path)!);
        return p;
    }
    public static void SaveDefault(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(Default(), new JsonSerializerOptions { WriteIndented = true })); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
