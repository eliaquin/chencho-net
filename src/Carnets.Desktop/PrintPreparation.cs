using System.IO;
using System.Security.Cryptography;
using Carnets.Core;

namespace Carnets.Desktop;

public static class PrintSafety
{
    public static void ValidateMode(OperatorSettings settings, CardProfile profile, bool calibration)
    {
        settings.Validate();
        if (settings.Mode is PrintMode.SimulationAutomatic or PrintMode.SimulationManual) return;
        if (string.IsNullOrWhiteSpace(settings.Printer)) throw new InvalidDataException("Elija una impresora instalada. No se cambiará a otra automáticamente.");
        if (calibration) return;
        if (!profile.ArtworkApproved) throw new InvalidDataException("El arte no está aprobado. Incorpore logo y firmas originales al perfil y revise su calidad antes de imprimir en producción. Puede usar simulación o una prueba de calibración.");
        if (settings.ValidatedPrinter != settings.Printer) throw new InvalidDataException("La validación física corresponde a otra impresora.");
        if (settings.Mode == PrintMode.Automatic && !settings.AutomaticValidated) throw new InvalidDataException("El dúplex automático requiere validación física previa.");
        if (settings.Mode == PrintMode.Manual && (!settings.ManualValidated || string.IsNullOrWhiteSpace(settings.Reinsertion) || string.IsNullOrWhiteSpace(settings.Feed))) throw new InvalidDataException("Valide y documente alimentación y reinserción antes de habilitar la operación manual.");
    }
    public static string ResourceStamp(string profilePath, CardProfile profile)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string path in new[] { profilePath }.Concat(new[] { profile.Logo, profile.PresidentSignature, profile.SecretarySignature }.Where(s => !string.IsNullOrEmpty(s)).Select(s => Path.Combine(Path.GetDirectoryName(profilePath)!, s))))
        {
            using var stream = File.OpenRead(path); var buffer = new byte[8192]; int length;
            while ((length = stream.Read(buffer)) != 0) hash.AppendData(buffer, 0, length);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
}

/// <summary>Revalidates shared render rules and immutable photo bytes for both spoolers.</summary>
public sealed class PreparedPrintAdapter(IPrintAdapter inner, CardComposer composer, string profilePath, CardProfile profile, string resourceStamp, bool calibration) : IPrintAdapter
{
    public async Task PrepareAsync(CardSnapshot card, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        string current;
        try { current = PrintSafety.ResourceStamp(profilePath, profile); }
        catch (IOException) { throw new SubmissionException("El perfil o sus recursos ya no están disponibles. Revise el diseño antes de repetir."); }
        if (current != resourceStamp) throw new SubmissionException("El perfil o sus gráficos cambiaron después de la revisión. Recargue el perfil y revise el diseño.");
        var photo = await Task.Run(() => PhotoLoader.Load(card.PhotoPath), cancellation);
        if (photo.Hash != card.PhotoHash) throw new SubmissionException("La foto cambió después de la revisión. Revísela antes de repetir.");
        var errors = composer.Compose(card.Values, photo, card.Crop, false, calibration).Errors.Concat(composer.Compose(card.Values, null, new(), true, calibration).Errors).ToArray();
        if (errors.Length > 0) throw new SubmissionException(string.Join("\n", errors));
        await inner.PrepareAsync(card, cancellation);
        if (PrintSafety.ResourceStamp(profilePath, profile) != resourceStamp) throw new SubmissionException("Los recursos cambiaron durante la preparación. Revise el diseño antes de repetir.");
    }
    public Task<Receipt> SendAsync(Submission submission, CancellationToken cancellation) => inner.SendAsync(submission, cancellation);
}
