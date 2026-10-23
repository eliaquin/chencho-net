using System.Printing;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using Carnets.Core;

namespace Carnets.Desktop;

public sealed class WindowsPrintAdapter(OperatorSettings settings, CardComposer composer, bool calibration = false) : IPrintAdapter
{
    private readonly Dictionary<string, (System.Windows.Media.DrawingGroup Front, System.Windows.Media.DrawingGroup Back)> prepared = [];
    public static string[] Printers()
    {
        using var server = new LocalPrintServer();
        using var queues = server.GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections });
        return queues.Select(q => q.FullName).Order(StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
    public Task PrepareAsync(CardSnapshot card, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var photo = PhotoLoader.Load(card.PhotoPath);
        if (photo.Hash != card.PhotoHash) throw new SubmissionException("La foto cambió después de revisarla. Revísela de nuevo.");
        var front = composer.Compose(card.Values, photo, card.Crop, false, calibration);
        var back = composer.Compose(card.Values, null, new(), true, calibration);
        if (front.Errors.Count + back.Errors.Count > 0) throw new SubmissionException(string.Join("\n", front.Errors.Concat(back.Errors)));
        prepared[card.Matricula] = (front.Drawing, back.Drawing);
        return Task.CompletedTask;
    }
    public async Task<Receipt> SendAsync(Submission submission, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!Printers().Contains(settings.Printer, StringComparer.Ordinal)) throw new SubmissionException("La impresora seleccionada no está instalada. No se usará otra impresora.");
        using var server = new LocalPrintServer();
        using var queue = new PrintQueue(server, settings.Printer);
        queue.Refresh();
        if (queue.IsOffline || queue.IsInError || queue.IsNotAvailable) throw new SubmissionException("La cola informa que la impresora no está disponible. Revise su estado.");
        var ticket = queue.UserPrintTicket?.Clone() ?? queue.DefaultPrintTicket.Clone();
        ticket.CopyCount = submission.Copies;
        ticket.PageOrientation = PageOrientation.Landscape;
        ticket.PageMediaSize = new PageMediaSize(CardComposer.Dip(53.98), CardComposer.Dip(85.6));
        ticket.PageScalingFactor = 100;
        ticket.PagesPerSheet = 1;
        ticket.Duplexing = submission.Side == CardSide.Both ? settings.ShortEdge ? Duplexing.TwoSidedShortEdge : Duplexing.TwoSidedLongEdge : Duplexing.OneSided;
        var capabilities = queue.GetPrintCapabilities(ticket);
        if (submission.Side == CardSide.Both && !capabilities.DuplexingCapability.Contains(ticket.Duplexing.Value)) throw new SubmissionException("El controlador no ofrece el dúplex elegido. Use una modalidad validada.");
        var validated = queue.MergeAndValidatePrintTicket(queue.DefaultPrintTicket, ticket).ValidatedPrintTicket;
        if (validated.Duplexing != ticket.Duplexing || validated.CopyCount != submission.Copies || validated.PageOrientation != PageOrientation.Landscape || validated.PageScalingFactor is not (null or 100)) throw new SubmissionException("El controlador cambió dúplex, orientación, escala o copias. Revise el perfil antes de imprimir.");
        var media = validated.PageMediaSize;
        if (media?.Width is not double width || media.Height is not double height || Math.Abs(width - CardComposer.Dip(53.98)) > 1 || Math.Abs(height - CardComposer.Dip(85.6)) > 1) throw new SubmissionException("El controlador no conservó el tamaño CR80. Configure el tamaño de tarjeta en sus propiedades.");
        var doc = new FixedDocument();
        doc.DocumentPaginator.PageSize = composer.Size;
        var frozen = prepared[submission.Card.Matricula];
        foreach (bool back in submission.Side switch { CardSide.Front => new[] { false }, CardSide.Back => new[] { true }, _ => new[] { false, true } })
        {
            var drawing = back ? frozen.Back : frozen.Front;
            var visual = composer.Visual(drawing);
            visual.RenderTransform = new System.Windows.Media.TransformGroup
            {
                Children = new() {
                    new System.Windows.Media.RotateTransform(back ? settings.BackRotation : 0, composer.Size.Width / 2, composer.Size.Height / 2),
                    new System.Windows.Media.TranslateTransform(CardComposer.Dip(back ? settings.BackX : settings.FrontX), CardComposer.Dip(back ? settings.BackY : settings.FrontY))
                }
            };
            var page = new FixedPage { Width = composer.Size.Width, Height = composer.Size.Height };
            page.Children.Add(visual);
            page.Measure(composer.Size); page.Arrange(new Rect(composer.Size)); page.UpdateLayout();
            var content = new PageContent(); ((IAddChild)content).AddChild(page); doc.Pages.Add(content);
        }
        string jobName = "Carnets-" + Guid.NewGuid().ToString("N");
        queue.CurrentJobSettings.Description = jobName;
        var writer = PrintQueue.CreateXpsDocumentWriter(queue);
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        writer.WritingCompleted += (_, e) => { if (e.Error is not null) done.TrySetException(new SubmissionException("El envío falló o quedó incierto. Revise la cola de Windows antes de repetir.", true)); else if (e.Cancelled) done.TrySetException(new SubmissionException("El controlador canceló el envío; revise la cola.", true)); else done.TrySetResult(); };
        try { writer.WriteAsync(doc.DocumentPaginator, validated); await done.Task; }
        catch (SubmissionException) { throw; }
        catch (Exception) { throw new SubmissionException("Resultado de envío desconocido. Revise la cola de Windows; no se reintentará.", true); }
        string? jobId = null;
        try { queue.Refresh(); using var jobs = queue.GetPrintJobInfoCollection(); jobId = jobs.FirstOrDefault(j => j.Name == jobName)?.JobIdentifier.ToString(); }
        catch (PrintSystemException) { /* A completed job may already have disappeared. */ }
        return new(jobId, "Enviado a la cola; salida física sin verificar.");
    }
}
