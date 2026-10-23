using System.Security.Cryptography;

namespace Carnets.Core;

public sealed record PhotoResolution(string? Path, IReadOnlyList<string> Candidates, string? Error);
public static class PhotoResolver
{
    public static bool Supported(string path) => new[] { ".png", ".jpg", ".jpeg" }.Contains(System.IO.Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    public static PhotoResolution Resolve(string reference, string matricula, string folder, bool relocateExplicit = false)
    {
        reference = reference.Trim();
        if (reference.Length > 0 && !relocateExplicit)
        {
            string path = System.IO.Path.IsPathRooted(reference) ? reference : System.IO.Path.Combine(folder, reference);
            if (File.Exists(path) && Supported(path)) return new(System.IO.Path.GetFullPath(path), [], null);
            // Case insensitive exact filename matching works even when the folder is case sensitive.
            if (!System.IO.Path.IsPathRooted(reference) && Directory.Exists(System.IO.Path.GetDirectoryName(path)))
            {
                var matches = Directory.EnumerateFiles(System.IO.Path.GetDirectoryName(path)!).Where(f => Supported(f) && string.Equals(System.IO.Path.GetFileName(f), System.IO.Path.GetFileName(path), StringComparison.OrdinalIgnoreCase)).ToArray();
                if (matches.Length == 1) return new(matches[0], [], null);
            }
            return new(null, [], "La referencia de FOTOS no existe o no es PNG/JPEG. Elija un archivo o confirme buscar su nombre en la carpeta de fotos.");
        }
        if (!Directory.Exists(folder)) return new(null, [], "Seleccione una carpeta de fotos existente.");
        var candidates = Directory.EnumerateFiles(folder).Where(Supported).Where(f => reference.Length > 0
            ? string.Equals(System.IO.Path.GetFileName(f), System.IO.Path.GetFileName(reference), StringComparison.OrdinalIgnoreCase)
            : string.Equals(System.IO.Path.GetFileNameWithoutExtension(f), matricula, StringComparison.OrdinalIgnoreCase)).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        return candidates.Length switch
        {
            1 => new(candidates[0], [], null),
            0 => new(null, [], "No se encontró una fotografía. Elija el archivo explícitamente."),
            _ => new(null, candidates, "Hay varias fotografías coincidentes. Elija una explícitamente.")
        };
    }
    public static string Fingerprint(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}

public sealed record Crop(double Zoom = 1, double X = 0.5, double Y = 0.5);
