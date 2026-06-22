using MlNFL.Downloader.Models;

namespace MlNFL.Downloader.Services;

internal sealed class DownloadOrchestrator(
    NflverseClient client,
    ScheduleLoader scheduleLoader,
    CsvGameSplitter splitter)
{
    public async Task RunAsync(
        IReadOnlyList<int> seasons,
        string outputRoot,
        bool force,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputRoot);

        Console.WriteLine("Loading schedules...");
        var schedules = await scheduleLoader.LoadAsync(seasons, cancellationToken);
        Console.WriteLine(
            "Loaded {0} games for season(s): {1}.",
            schedules.Count,
            string.Join(", ", seasons.OrderBy(s => s)));

        foreach (var season in seasons.OrderBy(s => s))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await DownloadSeasonAsync(season, schedules, outputRoot, force, cancellationToken);
        }

        Console.WriteLine("Done.");
    }

    private async Task DownloadSeasonAsync(
        int season,
        IReadOnlyDictionary<string, NflGameSchedule> schedules,
        string outputRoot,
        bool force,
        CancellationToken cancellationToken)
    {
        var seasonSchedules = schedules.Values
            .Where(g => g.Season == season)
            .ToDictionary(g => g.GameId, g => g, StringComparer.Ordinal);

        if (seasonSchedules.Count == 0)
        {
            Console.WriteLine("Season {0}: no scheduled games found, skipping.", season);
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Season {0} ({1} games)", season, seasonSchedules.Count);

        await SplitRemoteCsvAsync(
            NflverseUrls.PlayByPlayCsvGz(season),
            "play-by-play.csv",
            season,
            seasonSchedules,
            outputRoot,
            force,
            (stream, ct) => splitter.SplitByGameIdColumnAsync(
                stream,
                "play-by-play.csv",
                outputRoot,
                seasonSchedules,
                force,
                ct),
            cancellationToken);

        await SplitRemoteCsvAsync(
            NflverseUrls.PlayerStatsWeekCsvGz(season),
            "stats.csv",
            season,
            seasonSchedules,
            outputRoot,
            force,
            (stream, ct) => splitter.SplitPlayerStatsAsync(
                stream,
                outputRoot,
                new GameLookup(seasonSchedules),
                seasonSchedules,
                force,
                ct),
            cancellationToken);
    }

    private async Task SplitRemoteCsvAsync(
        string url,
        string outputFileName,
        int season,
        IReadOnlyDictionary<string, NflGameSchedule> seasonSchedules,
        string outputRoot,
        bool force,
        Func<Stream, CancellationToken, Task<SplitResult>> splitAsync,
        CancellationToken cancellationToken)
    {
        Console.Write("  Downloading {0}... ", outputFileName);

        try
        {
            await using var stream = await client.OpenCsvStreamAsync(url, cancellationToken);
            var result = await splitAsync(stream, cancellationToken);

            Console.WriteLine(
                "wrote {0} game file(s), skipped {1} existing game(s), {2} row(s) ({3} row(s) skipped).",
                result.GamesWritten,
                result.GamesSkipped,
                result.RowsWritten,
                result.RowsSkipped);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            Console.WriteLine("not found (season may not be published yet).");
        }
    }
}
