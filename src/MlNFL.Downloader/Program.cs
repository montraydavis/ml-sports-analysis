using MlNFL.Downloader.Services;

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    PrintHelp();
    return args.Length == 0 ? 1 : 0;
}

if (!string.Equals(args[0], "download", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Unknown command: {0}", args[0]);
    PrintHelp();
    return 1;
}

var seasons = new List<int>();
var outputRoot = Path.Combine("data", "games");
var force = false;

for (var i = 1; i < args.Length; i++)
{
    switch (args[i])
    {
        case "-s":
        case "--season":
            if (i + 1 >= args.Length || !int.TryParse(args[++i], out var season))
            {
                Console.Error.WriteLine("Missing or invalid value for --season.");
                return 1;
            }

            seasons.Add(season);
            break;
        case "-o":
        case "--output":
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("Missing value for --output.");
                return 1;
            }

            outputRoot = args[++i];
            break;
        case "-f":
        case "--force":
            force = true;
            break;
        default:
            if (int.TryParse(args[i], out var shorthandSeason))
            {
                seasons.Add(shorthandSeason);
                break;
            }

            Console.Error.WriteLine("Unknown argument: {0}", args[i]);
            PrintHelp();
            return 1;
    }
}

if (seasons.Count == 0)
{
    Console.Error.WriteLine("At least one --season value is required.");
    PrintHelp();
    return 1;
}

var invalidSeasons = seasons.Where(s => s < 1999).ToArray();
if (invalidSeasons.Length > 0)
{
    Console.Error.WriteLine(
        "Invalid season(s): {0}. nflverse play-by-play starts in 1999.",
        string.Join(", ", invalidSeasons));
    return 1;
}

using var client = new NflverseClient();
var orchestrator = new DownloadOrchestrator(
    client,
    new ScheduleLoader(client),
    new CsvGameSplitter());

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    await orchestrator.RunAsync(
        seasons.Distinct().ToArray(),
        outputRoot,
        force,
        cts.Token);
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Cancelled.");
    return 130;
}

return 0;

static void PrintHelp()
{
    Console.WriteLine(
        """
        MlNFL.Downloader — download NFL stats and play-by-play from nflverse (no API key).

        Usage:
          MlNFL.Downloader download --season <year> [--season <year> ...] [options]

        Options:
          -s, --season <year>   NFL season to download (1999-present). Repeat for multiple seasons.
          -o, --output <path>   Output root directory (default: data/games)
          -f, --force           Overwrite existing per-game CSV files
          -h, --help            Show help

        Output layout:
          <output>/<year>/<month>/<day>/<game_id>/stats.csv
          <output>/<year>/<month>/<day>/<game_id>/play-by-play.csv

        Examples:
          MlNFL.Downloader download --season 2024
          MlNFL.Downloader download -s 2023 -s 2024 -o data/games
        """);
}
