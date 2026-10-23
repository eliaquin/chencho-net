using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Carnets.Core;

namespace Carnets.Desktop;

internal static class SyntheticData
{
    public static HolderRow[] Create(string directory)
    {
        Directory.CreateDirectory(directory);
        var data = new List<SheetRow>();
        for (int i = 1; i <= 2; i++)
        {
            string path = Path.Combine(directory, $"demo-{i}.png");
            var drawing = new DrawingVisual();
            using (var dc = drawing.RenderOpen())
            {
                dc.DrawRectangle(Brushes.AliceBlue, null, new Rect(0, 0, 300, 380));
                var brush = i == 1 ? Brushes.SteelBlue : Brushes.Teal;
                dc.DrawEllipse(brush, null, new Point(150, 140), 70, 85);
                dc.DrawEllipse(brush, null, new Point(150, 380), 130, 150);
            }
            var bitmap = new RenderTargetBitmap(300, 380, 96, 96, PixelFormats.Pbgra32); bitmap.Render(drawing);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(path)) encoder.Save(stream);
            string[] values = [i == 1 ? "JOSÉ" : "ANA", "DEMOSTRACIÓN", "000-0000000-0", $"DEMO-{i:D3}", "SANTO DOMINGO", "-", "-", "PRUEBA", "01/01/2026", "31/12/2028", path];
            data.Add(new(i + 1, values.Select((s, index) => (s, index)).ToDictionary(p => p.index, p => new CellData(p.s, p.s))));
        }
        var holders = HolderValidation.Load(new("Demostración ficticia", Fields.Headers, data, false), Fields.Suggest(Fields.Headers), DateOnly.FromDateTime(DateTime.Now));
        return holders.Select(h =>
        {
            var photo = PhotoLoader.Load(h["referenciaFoto"]);
            return new HolderRow(h) { PhotoPath = h["referenciaFoto"], Photo = photo, ReviewedPhotoHash = photo.Hash, PhotoValidated = true };
        }).ToArray();
    }
}
