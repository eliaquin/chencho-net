using System.Text.Json;

namespace Carnets.Core;

public enum PrintMode { SimulationAutomatic, SimulationManual, Automatic, Manual }
public sealed class OperatorSettings
{
    public int SchemaVersion { get; set; } = 1;
    public Dictionary<string, string> ColumnHeaders { get; set; } = [];
    public string PhotosFolder { get; set; } = "";
    public string DateFormat { get; set; } = "dd/MM/yyyy";
    public string Printer { get; set; } = "";
    public PrintMode Mode { get; set; } = PrintMode.SimulationAutomatic;
    public bool AutomaticValidated { get; set; }
    public bool ManualValidated { get; set; }
    public string ValidatedPrinter { get; set; } = "";
    public string Reinsertion { get; set; } = "";
    public string Feed { get; set; } = "";
    public double FrontX { get; set; }
    public double FrontY { get; set; }
    public double BackX { get; set; }
    public double BackY { get; set; }
    public int BackRotation { get; set; }
    public bool ShortEdge { get; set; }
    public void Validate()
    {
        if (SchemaVersion != 1 || ColumnHeaders is null || PhotosFolder is null || Printer is null || DateFormat is null || ValidatedPrinter is null || Reinsertion is null || Feed is null || !Enum.IsDefined(Mode) || BackRotation is not (0 or 180) || new[] { FrontX, FrontY, BackX, BackY }.Any(v => !double.IsFinite(v) || Math.Abs(v) > 10) || DateFormat is not ("dd/MM/yyyy" or "MM/dd/yyyy" or "yyyy-MM-dd")) throw new InvalidDataException("Configuración incompatible o inválida.");
    }
    public OperatorSettings Snapshot() => JsonSerializer.Deserialize<OperatorSettings>(JsonSerializer.Serialize(this))!;
}

public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public static OperatorSettings Load(string path)
    {
        if (!File.Exists(path)) return new();
        try
        {
            var settings = JsonSerializer.Deserialize<OperatorSettings>(File.ReadAllText(path)) ?? throw new InvalidDataException();
            settings.Validate();
            return settings;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException) { throw new InvalidDataException("La configuración es inválida. Se conservará para diagnóstico; restablezca explícitamente para continuar.", ex); }
    }
    public static void Save(string path, OperatorSettings settings)
    {
        settings.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options)); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
