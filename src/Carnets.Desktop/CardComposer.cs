using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Carnets.Core;

namespace Carnets.Desktop;

public sealed record PreparedPhoto(BitmapSource Bitmap, string Hash);
public static class PhotoLoader
{
    public static PreparedPhoto Load(string path)
    {
        // One immutable read supplies both preview pixels and fingerprint; no time-of-check gap.
        var bytes = File.ReadAllBytes(path);
        using var stream = new MemoryStream(bytes, false);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        if (decoder is not (PngBitmapDecoder or JpegBitmapDecoder)) throw new InvalidDataException("La foto debe ser PNG o JPEG.");
        var frame = decoder.Frames[0];
        if ((long)frame.PixelWidth * frame.PixelHeight > 50_000_000) throw new InvalidDataException("La foto excede 50 megapíxeles.");
        // BitmapFrame can retain a dispatcher-bound decoder even after Freeze. Copy decoded pixels
        // to a standalone immutable BitmapSource before moving them from the worker to the UI.
        var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        int stride = checked(frame.PixelWidth * 4);
        var pixels = new byte[checked(stride * frame.PixelHeight)]; converted.CopyPixels(pixels, stride, 0);
        var image = BitmapSource.Create(frame.PixelWidth, frame.PixelHeight, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        image.Freeze();
        return new(image, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)));
    }
}

public sealed class CardComposer(CardProfile profile, string profileDirectory)
{
    public static double Dip(double mm) => mm * 96 / 25.4;
    public Size Size => new(Dip(profile.WidthMm), Dip(profile.HeightMm));
    public (DrawingGroup Drawing, List<string> Errors) Compose(IReadOnlyDictionary<string, string> values, PreparedPhoto? photo, Crop crop, bool back, bool calibration = false)
    {
        var group = new DrawingGroup();
        var errors = new List<string>();
        using (var dc = group.Open())
        {
            dc.PushClip(new RectangleGeometry(new Rect(Size)));
            dc.DrawRectangle((Brush)new BrushConverter().ConvertFromString(back ? profile.BackBackground : profile.Background)!, null, new Rect(Size));
            if (back) Back(dc); else Front(dc);
            if (calibration)
            {
                var pen = new Pen(Brushes.Magenta, Dip(.15));
                dc.DrawRectangle(null, pen, Mm(3, 3, profile.WidthMm - 6, profile.HeightMm - 6));
                dc.DrawLine(pen, new(Dip(3), Dip(27)), new(Dip(82.6), Dip(27)));
                Text(dc, back ? "REVERSO · CALIBRACIÓN" : "FRENTE · CALIBRACIÓN", 10, Brushes.Magenta, 7, 26, 70, 6, false);
            }
            dc.Pop();
        }
        group.Freeze();
        return (group, errors);

        void Front(DrawingContext dc)
        {
            var logo = profile.Fixed["logo"];
            if (string.IsNullOrEmpty(profile.Logo))
            {
                double radius = Math.Min(logo.Width, logo.Height) / 2;
                dc.DrawEllipse(Brushes.White, new Pen(Brushes.LightSteelBlue, Dip(.4)), new(Dip(logo.X + logo.Width / 2), Dip(logo.Y + logo.Height / 2)), Dip(radius), Dip(radius));
                Text(dc, "ICPARD", logo.Points, Brushes.DarkBlue, logo.X, logo.Y + logo.Height * .3, logo.Width, logo.Height * .5, true);
            }
            else Asset(dc, profile.Logo, Mm(logo.X, logo.Y, logo.Width, logo.Height));
            if (!FixedText(dc, profile.Institution, "institution", Brushes.White, false, true, 5.5)) errors.Add("El nombre institucional no cabe en la cabecera del perfil.");
            if (!FixedText(dc, profile.MemberLegend, "memberLegend", Brushes.White, true, true, 5)) errors.Add("La leyenda del frente no cabe en el perfil.");
            var frame = Mm(profile.PhotoX, profile.PhotoY, profile.PhotoWidth, profile.PhotoHeight);
            dc.DrawRectangle(Brushes.WhiteSmoke, null, frame);
            if (photo is not null)
            {
                double scale = Math.Max(frame.Width / photo.Bitmap.PixelWidth, frame.Height / photo.Bitmap.PixelHeight) * Math.Clamp(crop.Zoom, 1, 4);
                double w = photo.Bitmap.PixelWidth * scale, h = photo.Bitmap.PixelHeight * scale;
                dc.PushClip(new RectangleGeometry(frame));
                dc.DrawImage(photo.Bitmap, new(frame.X - (w - frame.Width) * crop.X, frame.Y - (h - frame.Height) * crop.Y, w, h));
                dc.Pop();
            }
            foreach (var field in profile.Fields)
            {
                if (!Text(dc, field.Label, field.LabelPoints, Brushes.White, field.X, field.Y, field.Width, 2.3, false)) errors.Add($"La etiqueta de {field.Key} no cabe en el perfil.");
                dc.DrawRectangle(Brushes.AliceBlue, null, Mm(field.X, field.Y + 2.3, field.Width, field.Height - 2.3));
                if (!Text(dc, values.GetValueOrDefault(field.Key, ""), field.Points, Brushes.Black, field.X + .5, field.Y + 2.5, field.Width - 1, field.Height - 2.7, false, false, 7)) errors.Add($"{field.Label}: el texto no cabe con fuente mínima de 7 puntos. Corrija el Excel o el perfil.");
            }
        }
        void Back(DrawingContext dc)
        {
            if (!FixedText(dc, profile.BackTitleText, "backTitle", Brushes.Black, true)) errors.Add("La cabecera del reverso no cabe en el perfil.");
            var band = profile.Fixed["backBand"];
            dc.DrawRectangle(Brushes.Black, null, Mm(band.X, band.Y, band.Width, band.Height));
            if (!FixedText(dc, profile.Legend, "backLegend", Brushes.White, true, true, 6)) errors.Add("La leyenda institucional no cabe en el reverso.");
            Signature(profile.PresidentSignature, profile.President, profile.PresidentRole, "president");
            Signature(profile.SecretarySignature, profile.Secretary, profile.SecretaryRole, "secretary");
            void Signature(string asset, string name, string role, string prefix)
            {
                var signature = profile.Fixed[prefix + "Signature"];
                if (!string.IsNullOrEmpty(asset)) Asset(dc, asset, Mm(signature.X, signature.Y, signature.Width, signature.Height));
                else Text(dc, "Firma pendiente de arte original", 4, Brushes.Gray, signature.X, signature.Y + signature.Height * .3, signature.Width, signature.Height * .5, true);
                dc.DrawLine(new Pen(Brushes.Black, Dip(.1)), new(Dip(signature.X), Dip(signature.Y + signature.Height + 1)), new(Dip(signature.X + signature.Width), Dip(signature.Y + signature.Height + 1)));
                if (!FixedText(dc, name, prefix + "Name", Brushes.Black, true, false, 4)) errors.Add("El nombre del firmante no cabe en el reverso.");
                if (!FixedText(dc, role, prefix + "Role", Brushes.DimGray, true, false, 4)) errors.Add("El cargo institucional no cabe en el reverso.");
            }
        }
    }
    private bool FixedText(DrawingContext dc, string text, string key, Brush brush, bool center, bool multiline = false, double? minimum = null)
    {
        var element = profile.Fixed[key];
        return Text(dc, text, element.Points, brush, element.X, element.Y, element.Width, element.Height, center, multiline, minimum);
    }
    private void Asset(DrawingContext dc, string relative, Rect frame)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(Path.GetFullPath(Path.Combine(profileDirectory, relative))); bitmap.EndInit(); bitmap.Freeze();
        double scale = Math.Min(frame.Width / bitmap.PixelWidth, frame.Height / bitmap.PixelHeight);
        double w = bitmap.PixelWidth * scale, h = bitmap.PixelHeight * scale;
        dc.DrawImage(bitmap, new(frame.X + (frame.Width - w) / 2, frame.Y + (frame.Height - h) / 2, w, h));
    }
    private static Rect Mm(double x, double y, double w, double h) => new(Dip(x), Dip(y), Dip(w), Dip(h));
    private bool Text(DrawingContext dc, string value, double points, Brush brush, double x, double y, double w, double h, bool center, bool multiline = false, double? minimum = null)
    {
        FormattedText formatted;
        double size = points;
        bool fits;
        do
        {
            formatted = new(value, CultureInfo.GetCultureInfo("es-DO"), FlowDirection.LeftToRight, new Typeface(profile.Font), size * 96 / 72, brush, 1);
            fits = formatted.WidthIncludingTrailingWhitespace <= Dip(w) && formatted.Height <= Dip(h) && (multiline || !value.Contains('\n') && !value.Contains('\r'));
            if (fits || minimum is null || size <= minimum) break;
            size = Math.Max(minimum.Value, size - .25);
        } while (true);
        dc.PushClip(new RectangleGeometry(Mm(x, y, w, h)));
        dc.DrawText(formatted, new(Dip(x) + (center ? Math.Max(0, (Dip(w) - formatted.WidthIncludingTrailingWhitespace) / 2) : 0), Dip(y)));
        dc.Pop();
        return fits;
    }
    public CardVisual Visual(DrawingGroup drawing) => new(drawing, Size);
}

public sealed class CardVisual(DrawingGroup drawing, Size size) : FrameworkElement
{
    public DrawingGroup Drawing { get; } = drawing;
    protected override Size MeasureOverride(Size availableSize) => size;
    protected override Size ArrangeOverride(Size finalSize) => size;
    protected override void OnRender(DrawingContext drawingContext) => drawingContext.DrawDrawing(Drawing);
}
