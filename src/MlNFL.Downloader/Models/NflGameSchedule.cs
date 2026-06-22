namespace MlNFL.Downloader.Models;

internal sealed class NflGameSchedule
{
    public required string GameId { get; init; }
    public required int Season { get; init; }
    public required int Week { get; init; }
    public required string GameType { get; init; }
    public required string AwayTeam { get; init; }
    public required string HomeTeam { get; init; }
    public required DateOnly GameDay { get; init; }
}
