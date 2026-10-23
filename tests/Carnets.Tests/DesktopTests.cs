using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Carnets.Core;
using Carnets.Desktop;

namespace Carnets.Tests;

public sealed class DesktopTests
{
    private static void Sta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
    private static Dictionary<string, string> Values()
    {
        string[] fields = ["JOSÉ", "MUÑOZ", "001-0000000-1", "000123", "SANTO DOMINGO", "-", "PÉREZ", "001-24", "09/03/2026", "31/12/2028", ""];
        return Fields.Keys.Zip(fields).ToDictionary(p => p.First, p => p.Second);
    }
    private static PreparedPhoto SyntheticPhoto()
    {
        // A deliberately non-square, synthetic image tests crop without retaining a real portrait.
        byte[] pixels = new byte[156 * 191 * 4];
        for (int y = 0; y < 191; y++) for (int x = 0; x < 156; x++)
        {
            int i = (y * 156 + x) * 4; pixels[i] = (byte)(x * 255 / 156); pixels[i + 1] = (byte)(y * 255 / 191); pixels[i + 2] = 100; pixels[i + 3] = 255;
        }
        var bitmap = BitmapSource.Create(156, 191, 96, 96, PixelFormats.Bgra32, null, pixels, 156 * 4); bitmap.Freeze();
        return new(bitmap, "synthetic");
    }
    [Fact] public void CardCompositionPreservesPhysicalSizeAndAllTenFields() => Sta(() =>
    {
        var profile = CardProfile.Default(); profile.Validate(System.IO.Path.GetTempPath());
        var composer = new CardComposer(profile, System.IO.Path.GetTempPath());
        var composed = composer.Compose(Values(), SyntheticPhoto(), new(), false);
        Assert.Empty(composed.Errors); Assert.Equal(85.6 * 96 / 25.4, composer.Size.Width, 7); Assert.Equal(53.98 * 96 / 25.4, composer.Size.Height, 7);
        Assert.Equal(10, profile.Fields.Count); Assert.Contains(profile.Fields, f => f.Key == "filial" && f.Label == "Provincia");
        Assert.True(composed.Drawing.IsFrozen);
    });
    [Fact] public void LongTextBlocksInsteadOfSilentlyClippingAtMinimumFont() => Sta(() =>
    {
        var values = Values(); values["nombre"] = new string('W', 200);
        var composer = new CardComposer(CardProfile.Default(), System.IO.Path.GetTempPath());
        Assert.Contains(composer.Compose(values, SyntheticPhoto(), new(), false).Errors, e => e.StartsWith("Nombre:"));
    });
    [Fact] public void BackContainsNoVariableDataAndRemainsSameAcrossHolders() => Sta(() =>
    {
        var composer = new CardComposer(CardProfile.Default(), System.IO.Path.GetTempPath());
        var values = Values(); var first = composer.Compose(values, SyntheticPhoto(), new(), true).Drawing;
        values["nombre"] = "OTRO TITULAR"; values["matricula"] = "999";
        var second = composer.Compose(values, null, new(4, 0, 0), true).Drawing;
        Assert.Equal(Pixels(composer.Visual(first)), Pixels(composer.Visual(second)));
    });
    private static byte[] Pixels(CardVisual visual)
    {
        var size = new Size(CardComposer.Dip(85.6), CardComposer.Dip(53.98));
        visual.Measure(size); visual.Arrange(new Rect(size)); visual.UpdateLayout();
        var bitmap = new RenderTargetBitmap(324, 205, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var bytes = new byte[324 * 205 * 4]; bitmap.CopyPixels(bytes, 324 * 4, 0); return bytes;
    }
    [Fact] public void DifferentCropsChangeOnlyTheRenderedPhotoWithoutMutatingSource() => Sta(() =>
    {
        var composer = new CardComposer(CardProfile.Default(), System.IO.Path.GetTempPath()); var photo = SyntheticPhoto();
        byte[] a = Pixels(composer.Visual(composer.Compose(Values(), photo, new(), false).Drawing));
        byte[] b = Pixels(composer.Visual(composer.Compose(Values(), photo, new(2, 0, 1), false).Drawing));
        Assert.False(a.SequenceEqual(b)); Assert.Equal(156, photo.Bitmap.PixelWidth); Assert.Equal(191, photo.Bitmap.PixelHeight);
    });
    [Fact] public void UnapprovedProfileDoesNotPretendToHaveOriginalArtwork() => Sta(() =>
    {
        var profile = CardProfile.Default(); Assert.False(profile.ArtworkApproved); profile.ArtworkApproved = true;
        Assert.Throws<System.IO.InvalidDataException>(() => profile.Validate(System.IO.Path.GetTempPath()));
    });
    [Fact] public void RenderSyntheticCardsForVisualVerification() => Sta(() =>
    {
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string output = System.IO.Path.Combine(root, "artifacts", "verification"); System.IO.Directory.CreateDirectory(output);
        var composer = new CardComposer(CardProfile.Default(), System.IO.Path.GetTempPath());
        foreach (bool back in new[] { false, true }) MainWindow.SaveDrawing(composer.Visual(composer.Compose(Values(), SyntheticPhoto(), new(), back).Drawing), System.IO.Path.Combine(output, back ? "synthetic-back.png" : "synthetic-front.png"));
        var window = new MainWindow(System.IO.Path.Combine(output, "isolated-settings"));
        window.Show(); window.Measure(new Size(1440, 900)); window.Arrange(new Rect(0, 0, 1440, 900)); window.UpdateLayout();
        Assert.Contains("Carnets", window.Title); Assert.True(window.IsLoaded);
        var grid = (System.Windows.Controls.DataGrid)window.FindName("Records");
        var source = (System.Collections.ObjectModel.ObservableCollection<HolderRow>)grid.ItemsSource;
        var holder = new Holder { Row = 2, Sheet = "Prueba sintética", Values = Values() };
        var row = new HolderRow(holder) { Photo = SyntheticPhoto(), PhotoPath = "synthetic.png" };
        source.Add(row); grid.SelectedItem = row;
        window.UpdateLayout();
        Assert.Single(grid.Items.Cast<object>()); Assert.False(row.Selected); Assert.Equal(85.6 * 96 / 25.4, ((CardVisual)((System.Windows.Controls.Viewbox)window.FindName("Preview")).Child).DesiredSize.Width, 7);
        var bitmap = new RenderTargetBitmap(1440, 900, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = System.IO.File.Create(System.IO.Path.Combine(output, "window.png"))) png.Save(stream);
        window.Close();
    });
}
