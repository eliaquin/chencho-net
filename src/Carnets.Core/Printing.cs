namespace Carnets.Core;

public enum CardSide { Both, Front, Back }
public enum SubmissionState { Pending, Preparing, Sending, Accepted, Incomplete, Failed, Unknown }
public sealed record CardSnapshot(string Matricula, IReadOnlyDictionary<string, string> Values, string PhotoPath, string PhotoHash, Crop Crop, IReadOnlyList<string>? Warnings = null);
public sealed record Submission(CardSnapshot Card, int Copy, int Copies, CardSide Side);
public sealed record Receipt(string? JobId, string Message);
public interface IPrintAdapter
{
    Task PrepareAsync(CardSnapshot card, CancellationToken cancellation);
    Task<Receipt> SendAsync(Submission submission, CancellationToken cancellation);
}
public sealed class SubmissionException(string message, bool uncertain = false) : Exception(message)
{
    public bool Uncertain { get; } = uncertain;
}
public sealed class CardResult(CardSnapshot card, int copy, int copies)
{
    public CardSnapshot Card { get; } = card;
    public int Copy { get; } = copy;
    public int Copies { get; } = copies;
    public SubmissionState State { get; set; } = SubmissionState.Pending;
    public List<string> JobIds { get; } = [];
    public string Message { get; set; } = "Pendiente";
    public int AcceptedFaces { get; set; }
}
public sealed class PrintRun
{
    private int running;
    public List<CardResult> Results { get; } = [];
    public bool IsRunning => running != 0;
    public async Task ExecuteAsync(IReadOnlyList<CardSnapshot> cards, int copies, bool manual, CardSide recovery,
        IPrintAdapter adapter, Func<CardResult, Task<bool>> confirmReinsertion, Action changed, CancellationToken cancellation)
    {
        if (Interlocked.Exchange(ref running, 1) != 0) throw new InvalidOperationException("Ya hay un envío en curso.");
        try
        {
            if (cards.Count == 0 || copies is < 1 or > 100) throw new InvalidDataException("Selección o copias inválidas.");
            Results.Clear();
            foreach (var card in cards)
                if (manual && recovery == CardSide.Both) for (int copy = 1; copy <= copies; copy++) Results.Add(new(card, copy, 1));
                else Results.Add(new(card, 1, copies));
            changed();
            foreach (var result in Results)
            {
                if (cancellation.IsCancellationRequested) break;
                try
                {
                    result.State = SubmissionState.Preparing; result.Message = "Preparando"; changed();
                    await adapter.PrepareAsync(result.Card, cancellation);
                    if (manual && recovery == CardSide.Both)
                    {
                        await Send(CardSide.Front);
                        result.State = SubmissionState.Incomplete; result.Message = "Frente en cola; pendiente de reinserción"; changed();
                        if (cancellation.IsCancellationRequested || !await confirmReinsertion(result)) break;
                        // Recheck resources before the back as well; changed photos require operator review.
                        await adapter.PrepareAsync(result.Card, cancellation);
                        await Send(CardSide.Back);
                    }
                    else await Send(recovery);
                    result.State = recovery == CardSide.Both ? SubmissionState.Accepted : SubmissionState.Incomplete;
                    result.Message = recovery == CardSide.Both ? "Enviado a la cola; salida física sin verificar" : "Cara de recuperación enviada; verifique la tarjeta";
                    changed();
                }
                catch (OperationCanceledException)
                {
                    result.State = result.AcceptedFaces > 0 ? SubmissionState.Incomplete : SubmissionState.Pending;
                    result.Message = "Envíos detenidos; revise la cola de Windows para lo ya aceptado."; changed(); break;
                }
                catch (Exception ex)
                {
                    result.State = ex is SubmissionException { Uncertain: true } ? SubmissionState.Unknown : result.AcceptedFaces > 0 ? SubmissionState.Incomplete : SubmissionState.Failed;
                    result.Message = ex.Message; changed(); break;
                }
                async Task Send(CardSide side)
                {
                    cancellation.ThrowIfCancellationRequested();
                    result.State = SubmissionState.Sending; result.Message = "Enviando"; changed();
                    var receipt = await adapter.SendAsync(new(result.Card, result.Copy, result.Copies, side), cancellation);
                    if (receipt.JobId is not null) result.JobIds.Add(receipt.JobId);
                    result.AcceptedFaces += (side == CardSide.Both ? 2 : 1) * result.Copies;
                    result.Message = receipt.Message; changed();
                }
            }
        }
        finally { Interlocked.Exchange(ref running, 0); changed(); }
    }
}

/// <summary>A deterministic in-memory spooler, not a Zebra firmware or Windows driver emulator.</summary>
public sealed class SimulatedPrintAdapter(int failAt = 0, bool uncertain = false) : IPrintAdapter
{
    public List<Submission> Jobs { get; } = [];
    public Task PrepareAsync(CardSnapshot card, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (PhotoResolver.Fingerprint(card.PhotoPath) != card.PhotoHash) throw new SubmissionException("La foto cambió desde la revisión. Revísela antes de un nuevo envío.");
        return Task.CompletedTask;
    }
    public async Task<Receipt> SendAsync(Submission submission, CancellationToken cancellation)
    {
        await Task.Delay(120, cancellation);
        if (failAt > 0 && Jobs.Count + 1 == failAt) throw new SubmissionException(uncertain ? "SIMULACIÓN: resultado desconocido; no se reintentará." : "SIMULACIÓN: fallo de envío; no se reintentará.", uncertain);
        Jobs.Add(submission);
        return new($"SIM-{Jobs.Count:D4}", "SIMULACIÓN: aceptado por la cola virtual; ninguna tarjeta física impresa");
    }
}
