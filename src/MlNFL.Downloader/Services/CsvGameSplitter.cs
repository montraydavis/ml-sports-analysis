using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using MlNFL.Downloader.Models;

namespace MlNFL.Downloader.Services;

internal sealed class CsvGameSplitter
{
    private static readonly CsvConfiguration ReadConfiguration = new(CultureInfo.InvariantCulture)
    {
        MissingFieldFound = null,
        HeaderValidated = null,
        BadDataFound = null,
    };

    private static readonly CsvConfiguration WriteConfiguration = new(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
    };

    public Task<SplitResult> SplitByGameIdColumnAsync(
        Stream csvStream,
        string outputFileName,
        string outputRoot,
        IReadOnlyDictionary<string, NflGameSchedule> schedules,
        bool force,
        CancellationToken cancellationToken) =>
        SplitAsync(
            csvStream,
            outputFileName,
            outputRoot,
            schedules,
            force,
            headers => Array.FindIndex(
                headers,
                h => string.Equals(h, "game_id", StringComparison.OrdinalIgnoreCase)),
            (headers, fields, gameIdIndex) => fields[gameIdIndex],
            cancellationToken);

    public Task<SplitResult> SplitPlayerStatsAsync(
        Stream csvStream,
        string outputRoot,
        GameLookup gameLookup,
        IReadOnlyDictionary<string, NflGameSchedule> schedules,
        bool force,
        CancellationToken cancellationToken) =>
        SplitAsync(
            csvStream,
            "stats.csv",
            outputRoot,
            schedules,
            force,
            headers => -1,
            (headers, fields, _) => ResolveStatsGameId(headers, fields, gameLookup),
            cancellationToken);

    private static string? ResolveStatsGameId(
        string[] headers,
        string[] fields,
        GameLookup gameLookup)
    {
        var seasonIndex = FindColumn(headers, "season");
        var weekIndex = FindColumn(headers, "week");
        var seasonTypeIndex = FindColumn(headers, "season_type");
        var teamIndex = FindColumn(headers, "team");
        var opponentIndex = FindColumn(headers, "opponent_team");

        if (seasonIndex < 0 || weekIndex < 0 || seasonTypeIndex < 0 ||
            teamIndex < 0 || opponentIndex < 0)
        {
            return null;
        }

        if (!int.TryParse(fields[seasonIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var season) ||
            !int.TryParse(fields[weekIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var week))
        {
            return null;
        }

        var seasonType = fields[seasonTypeIndex];
        var team = fields[teamIndex];
        var opponent = fields[opponentIndex];

        if (string.IsNullOrWhiteSpace(seasonType) ||
            string.IsNullOrWhiteSpace(team) ||
            string.IsNullOrWhiteSpace(opponent))
        {
            return null;
        }

        return gameLookup.ResolveGameId(season, week, seasonType, team, opponent);
    }

    private static int FindColumn(string[] headers, string name) =>
        Array.FindIndex(headers, h => string.Equals(h, name, StringComparison.OrdinalIgnoreCase));

    private async Task<SplitResult> SplitAsync(
        Stream csvStream,
        string outputFileName,
        string outputRoot,
        IReadOnlyDictionary<string, NflGameSchedule> schedules,
        bool force,
        Func<string[], int> resolveGameIdColumnIndex,
        Func<string[], string[], int, string?> resolveGameId,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(csvStream);
        using var csv = new CsvReader(reader, ReadConfiguration);

        if (!await csv.ReadAsync())
        {
            throw new InvalidDataException("CSV stream is empty.");
        }

        csv.ReadHeader();
        var headers = csv.HeaderRecord
            ?? throw new InvalidDataException("CSV header row is missing.");

        var gameIdIndex = resolveGameIdColumnIndex(headers);
        if (gameIdIndex < 0 && !string.Equals(outputFileName, "stats.csv", StringComparison.Ordinal))
        {
            throw new InvalidDataException("CSV is missing a game_id column.");
        }

        var writers = new Dictionary<string, GameCsvWriter>(StringComparer.Ordinal);
        var gamesWritten = new HashSet<string>(StringComparer.Ordinal);
        var gamesSkipped = new HashSet<string>(StringComparer.Ordinal);
        var rowsWritten = 0;
        var rowsSkipped = 0;

        try
        {
            while (await csv.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fields = new string[headers.Length];
                for (var i = 0; i < headers.Length; i++)
                {
                    fields[i] = csv.GetField(i) ?? string.Empty;
                }

                var gameId = gameIdIndex >= 0
                    ? fields[gameIdIndex]
                    : resolveGameId(headers, fields, gameIdIndex);

                if (string.IsNullOrWhiteSpace(gameId))
                {
                    rowsSkipped++;
                    continue;
                }

                if (!schedules.TryGetValue(gameId, out var schedule))
                {
                    rowsSkipped++;
                    continue;
                }

                var gameDirectory = GamePathResolver.GetGameDirectory(
                    outputRoot,
                    schedule.GameDay,
                    gameId);
                var outputPath = Path.Combine(gameDirectory, outputFileName);

                if (!writers.TryGetValue(gameId, out var writer))
                {
                    if (!force && File.Exists(outputPath))
                    {
                        gamesSkipped.Add(gameId);
                        rowsSkipped++;
                        continue;
                    }

                    Directory.CreateDirectory(gameDirectory);
                    writer = GameCsvWriter.Create(outputPath, headers);
                    writers[gameId] = writer;
                    gamesWritten.Add(gameId);
                }
                else if (gamesSkipped.Contains(gameId))
                {
                    rowsSkipped++;
                    continue;
                }

                await writer.WriteRowAsync(fields, cancellationToken);
                rowsWritten++;
            }
        }
        finally
        {
            foreach (var writer in writers.Values)
            {
                await writer.DisposeAsync();
            }
        }

        return new SplitResult(
            gamesWritten.Count,
            gamesSkipped.Count,
            rowsWritten,
            rowsSkipped);
    }

    private sealed class GameCsvWriter : IAsyncDisposable
    {
        private readonly StreamWriter _streamWriter;
        private readonly CsvWriter _csvWriter;
        private bool _disposed;

        private GameCsvWriter(StreamWriter streamWriter, CsvWriter csvWriter, string[] headers)
        {
            _streamWriter = streamWriter;
            _csvWriter = csvWriter;
            Headers = headers;
            _csvWriter.WriteField(Headers);
            _csvWriter.NextRecord();
        }

        public string[] Headers { get; }

        public static GameCsvWriter Create(string outputPath, string[] headers)
        {
            var streamWriter = new StreamWriter(outputPath);
            var csvWriter = new CsvWriter(streamWriter, WriteConfiguration);
            return new GameCsvWriter(streamWriter, csvWriter, headers);
        }

        public Task WriteRowAsync(string[] fields, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (var i = 0; i < fields.Length; i++)
            {
                _csvWriter.WriteField(fields[i]);
            }

            _csvWriter.NextRecord();
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            await _csvWriter.DisposeAsync();
            await _streamWriter.DisposeAsync();
        }
    }
}

internal readonly record struct SplitResult(
    int GamesWritten,
    int GamesSkipped,
    int RowsWritten,
    int RowsSkipped);
