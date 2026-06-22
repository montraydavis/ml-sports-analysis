namespace MlNFL.Downloader;

internal static class NflverseUrls
{
    public const string SchedulesCsv =
        "https://github.com/nflverse/nfldata/raw/master/data/games.csv";

    public static string PlayByPlayCsvGz(int season) =>
        $"https://github.com/nflverse/nflverse-data/releases/download/pbp/play_by_play_{season}.csv.gz";

    public static string PlayerStatsWeekCsvGz(int season) =>
        $"https://github.com/nflverse/nflverse-data/releases/download/stats_player/stats_player_week_{season}.csv.gz";
}
