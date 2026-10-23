using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Carnets.Core;

namespace Carnets.Desktop;

public static class Dialogs
{
    private sealed record ConfirmationItem(CardSnapshot Card, string Label);
    public static Window Shell(Window owner, string title, UIElement body, Action accept, double width = 650)
    {
        var window = new Window { Owner = owner, Title = title, Width = width, Height = 680, MinWidth = 440, MinHeight = 300, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = System.Windows.Media.Brushes.White, FontSize = 14 };
        var dock = new DockPanel { Margin = new Thickness(20) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancelar", IsCancel = true, Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(8) };
        var ok = new Button { Content = "Confirmar", IsDefault = true, Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(8) };
        ok.Click += (_, _) => { try { accept(); window.DialogResult = true; } catch (Exception ex) { MessageBox.Show(window, ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning); } };
        buttons.Children.Add(cancel); buttons.Children.Add(ok); DockPanel.SetDock(buttons, Dock.Bottom); dock.Children.Add(buttons); dock.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); window.Content = dock;
        return window;
    }
    public static string? Sheet(Window owner, string[] names)
    {
        var list = new ListBox { ItemsSource = names, SelectedItem = names.Contains("Hoja1") ? "Hoja1" : names.First(), Margin = new Thickness(8) };
        string? result = null;
        var dialog = Shell(owner, "Elegir hoja del Excel", list, () => result = (string)list.SelectedItem, 500); dialog.Height = 350;
        return dialog.ShowDialog() == true ? result : null;
    }
    public static bool ConfirmPrint(Window owner, IReadOnlyList<CardSnapshot> cards, int copies, string mode, string printer, CardSide side, bool expired, bool calibration, CardComposer composer)
    {
        var body = new StackPanel();
        body.Children.Add(new TextBlock { Text = $"{(calibration ? "PRUEBA DE CALIBRACIÓN" : "REVISIÓN ANTES DE ENVIAR")}\n{mode}\nDestino: {printer}\n{cards.Count} titulares × {copies} copias = {cards.Count * copies} tarjetas\nCaras: {(side == CardSide.Both ? "frente y reverso" : side == CardSide.Front ? "SOLO FRENTE · recuperación explícita" : "SOLO REVERSO · recuperación explícita")}\n\nLa aceptación por una cola no demuestra salida física. Revise toda la selección, incluidos los titulares ocultos por el filtro.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        var list = new ListBox { ItemsSource = cards.Select(c => new ConfirmationItem(c, $"{c.Matricula} · {c.Values.GetValueOrDefault("nombre")} {c.Values.GetValueOrDefault("apellido")}{(c.Warnings?.Count > 0 ? " · advertencias" : "")}")), DisplayMemberPath = nameof(ConfirmationItem.Label), MaxHeight = 170, MinHeight = 80 };
        body.Children.Add(list);
        var name = new TextBlock { Margin = new Thickness(0, 8, 0, 8), TextWrapping = TextWrapping.Wrap }; body.Children.Add(name);
        var preview = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2, Margin = new Thickness(0, 8, 0, 8) }; body.Children.Add(preview);
        list.SelectionChanged += (_, _) =>
        {
            preview.Children.Clear();
            if (list.SelectedItem is not ConfirmationItem choice) return;
            var card = choice.Card;
            name.Text = $"{card.Matricula} · {card.Values.GetValueOrDefault("nombre")} {card.Values.GetValueOrDefault("apellido")}\n" + string.Join("\n", card.Warnings ?? []);
            try
            {
                var photo = PhotoLoader.Load(card.PhotoPath);
                if (photo.Hash != card.PhotoHash) throw new InvalidDataException("La foto cambió; cancele y revísela de nuevo.");
                foreach (bool back in new[] { false, true }) preview.Children.Add(new Viewbox { Child = composer.Visual(composer.Compose(card.Values, photo, card.Crop, back, calibration).Drawing), Margin = new Thickness(4) });
            }
            catch (Exception) { name.Text += "\nFoto cambiada o ilegible. Cancele y revise; el envío se bloqueará si no coincide."; }
        };
        list.SelectedIndex = 0;
        var acknowledgement = new CheckBox { Content = "Reconozco que hay carnets vencidos y deseo enviar sus fechas sin modificarlas.", Margin = new Thickness(0, 12, 0, 12), Visibility = expired ? Visibility.Visible : Visibility.Collapsed };
        body.Children.Add(acknowledgement);
        var dialog = Shell(owner, "Confirmar envío de carnets", body, () => { if (expired && acknowledgement.IsChecked != true) throw new InvalidDataException("Reconozca explícitamente los vencimientos para continuar."); }, 800);
        return dialog.ShowDialog() == true;
    }
    public static Dictionary<string, int>? Mapping(Window owner, SheetData sheet, OperatorSettings settings, bool force)
    {
        var suggested = Fields.Suggest(sheet.Headers);
        foreach (var (key, header) in settings.ColumnHeaders)
        {
            var matches = Enumerable.Range(0, sheet.Headers.Count).Where(i => Fields.Normalize(sheet.Headers[i]) == Fields.Normalize(header)).ToArray();
            if (Fields.Keys.Contains(key) && matches.Length == 1) suggested[key] = matches[0];
        }
        if (!force && suggested.Count == Fields.Keys.Length && suggested.Values.Distinct().Count() == Fields.Keys.Length) return suggested;
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = "Asocie los once campos a columnas diferentes. El número distingue encabezados iguales. Los campos adicionales permanecen fuera del carnet.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        var inputs = new Dictionary<string, ComboBox>();
        for (int i = 0; i < Fields.Keys.Length; i++)
        {
            panel.Children.Add(new TextBlock { Text = Fields.Labels[i], Margin = new Thickness(0, 6, 0, 3) });
            var combo = new ComboBox { ItemsSource = sheet.Headers.Select((h, index) => $"{index + 1}: {h}"), SelectedIndex = suggested.GetValueOrDefault(Fields.Keys[i], -1), Padding = new Thickness(6) }; inputs[Fields.Keys[i]] = combo; panel.Children.Add(combo);
        }
        Dictionary<string, int>? result = null;
        var dialog = Shell(owner, "Asociar columnas del Excel", panel, () =>
        {
            var map = inputs.ToDictionary(p => p.Key, p => p.Value.SelectedIndex);
            if (map.Values.Any(i => i < 0) || map.Values.Distinct().Count() != Fields.Keys.Length) throw new InvalidDataException("Seleccione una columna diferente para cada campo.");
            result = map;
        });
        return dialog.ShowDialog() == true ? result : null;
    }
    public static OperatorSettings? Settings(Window owner, OperatorSettings original, string[] printers, string profilePath)
    {
        var next = original.Snapshot(); var panel = new StackPanel(); var text = new Dictionary<string, TextBox>();
        void Label(string s) => panel.Children.Add(new TextBlock { Text = s, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 4) });
        void Input(string label, string key, string value) { Label(label); var box = new TextBox { Text = value, Padding = new Thickness(6) }; text[key] = box; panel.Children.Add(box); }
        Label("Impresora instalada (nunca se cambia automáticamente)");
        var printer = new ComboBox { ItemsSource = printers, SelectedItem = original.Printer, Padding = new Thickness(6) }; panel.Children.Add(printer);
        Label("Modalidad");
        var mode = new ComboBox { ItemsSource = new[] { "Simulación automática", "Simulación manual guiada", "Dúplex automático real", "Manual guiada real" }, SelectedIndex = (int)original.Mode, Padding = new Thickness(6) }; panel.Children.Add(mode);
        Label("Formato de fechas escritas como texto (se aplica al volver a cargar)");
        var dateFormat = new ComboBox { ItemsSource = new[] { "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd" }, SelectedItem = original.DateFormat }; panel.Children.Add(dateFormat);
        foreach (var (key, label, value) in new[] { ("fx", "Offset frente X (mm)", original.FrontX), ("fy", "Offset frente Y (mm)", original.FrontY), ("bx", "Offset reverso X (mm)", original.BackX), ("by", "Offset reverso Y (mm)", original.BackY) }) Input(label, key, value.ToString(CultureInfo.InvariantCulture));
        Label("Giro del reverso"); var rotation = new ComboBox { ItemsSource = new[] { "0°", "180°" }, SelectedIndex = original.BackRotation == 0 ? 0 : 1 }; panel.Children.Add(rotation);
        var edge = new CheckBox { Content = "Dúplex por borde corto", IsChecked = original.ShortEdge, Margin = new Thickness(0, 12, 0, 6) }; panel.Children.Add(edge);
        Label("Marque validación física solo después de probar esa impresora real. La simulación no la acredita.");
        var auto = new CheckBox { Content = "Dúplex automático físicamente validado", IsChecked = original.AutomaticValidated }; panel.Children.Add(auto);
        var manual = new CheckBox { Content = "Alimentación y reinserción manual físicamente validadas", IsChecked = original.ManualValidated }; panel.Children.Add(manual);
        Input("Instrucciones de reinserción comprobadas", "reinsertion", original.Reinsertion); Input("Modo de alimentación comprobado", "feed", original.Feed);
        Label("Perfil JSON local: " + profilePath + "\nLa impresión de producción exige logo, firmas originales y aprobación del arte.");
        var dialog = Shell(owner, "Configuración local", panel, () =>
        {
            double Number(string key) => double.TryParse(text[key].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value) && Math.Abs(value) <= 10 ? value : throw new InvalidDataException("Offsets: use valores entre -10 y 10 mm, con punto decimal.");
            next.Printer = printer.SelectedItem as string ?? original.Printer; next.Mode = (PrintMode)mode.SelectedIndex; next.DateFormat = (string)dateFormat.SelectedItem;
            next.FrontX = Number("fx"); next.FrontY = Number("fy"); next.BackX = Number("bx"); next.BackY = Number("by"); next.BackRotation = rotation.SelectedIndex == 0 ? 0 : 180; next.ShortEdge = edge.IsChecked == true;
            next.AutomaticValidated = auto.IsChecked == true; next.ManualValidated = manual.IsChecked == true; next.Reinsertion = text["reinsertion"].Text.Trim(); next.Feed = text["feed"].Text.Trim();
            // Validation belongs to the exact printer, not whichever device is selected later.
            bool printerChanged = next.Printer != original.ValidatedPrinter;
            if (printerChanged && (next.AutomaticValidated || next.ManualValidated) && MessageBox.Show(owner, "¿Confirma que las comprobaciones físicas se realizaron en la impresora ahora elegida?", "Validación física", MessageBoxButton.YesNo) != MessageBoxResult.Yes) throw new InvalidDataException("No se guardó la validación física.");
            next.ValidatedPrinter = next.Printer;
            if (next.ManualValidated && (next.Reinsertion.Length == 0 || next.Feed.Length == 0)) throw new InvalidDataException("Documente reinserción y alimentación antes de validar la modalidad manual.");
            if (next.Mode is PrintMode.Automatic or PrintMode.Manual && string.IsNullOrWhiteSpace(next.Printer)) throw new InvalidDataException("Elija expresamente una impresora instalada.");
            next.Validate();
        });
        return dialog.ShowDialog() == true ? next : null;
    }
}
