namespace MyGamingMap.API.Services.EraSummaries;

public interface IEraSummaryService
{
    /// Writes labels and summaries for the whole set of eras in one request,
    /// so the model can see sibling eras and write the set as a chronicle.
    Task<IReadOnlyList<(string Label, string Summary)?>> SummariseAllAsync(
        IReadOnlyList<EraFacts> facts,
        CancellationToken ct = default);
}