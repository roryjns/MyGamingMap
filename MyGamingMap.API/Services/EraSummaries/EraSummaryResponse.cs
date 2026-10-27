namespace MyGamingMap.API.Services.EraSummaries;

public sealed class EraSummariesResponse
{
    public required List<EraSummaryEntry> Eras { get; init; }
}

public sealed class EraSummaryEntry
{
    public required string Start { get; init; } // Used to match against the input.
    public required string Label { get; init; }
    public required string Summary { get; init; }
}