namespace MlNFL.Downloader.Services;

internal static class GamePathResolver
{
    public static string GetGameDirectory(string outputRoot, DateOnly gameDay, string gameId) =>
        Path.Combine(
            outputRoot,
            gameDay.Year.ToString("D4"),
            gameDay.Month.ToString("D2"),
            gameDay.Day.ToString("D2"),
            gameId);

    public static string GetPlayByPlayPath(string gameDirectory) =>
        Path.Combine(gameDirectory, "play-by-play.csv");

    public static string GetStatsPath(string gameDirectory) =>
        Path.Combine(gameDirectory, "stats.csv");
}
