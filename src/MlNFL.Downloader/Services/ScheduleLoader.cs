using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using MlNFL.Downloader.Models;

namespace MlNFL.Downloader.Services;

internal sealed class ScheduleLoader(NflverseClient client)
{
    public async Task<IReadOnlyDictionary<string, NflGameSchedule>> LoadAsync(
        IEnumerable<int> seasons,
        CancellationToken cancellationToken)
    {
        var seasonSet = seasons.ToHashSet();
        var games = new Dictionary<string, NflGameSchedule>(StringComparer.Ordinal);

        await using var stream = await client.OpenCsvStreamAsync(
            NflverseUrls.SchedulesCsv,
            cancellationToken);
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CsvConfiguration);

        await foreach (var record in csv.GetRecordsAsync<ScheduleRow>(cancellationToken))
        {
            if (!seasonSet.Contains(record.Season))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(record.GameId) ||
                string.IsNullOrWhiteSpace(record.GameDay) ||
                string.IsNullOrWhiteSpace(record.AwayTeam) ||
                string.IsNullOrWhiteSpace(record.HomeTeam) ||
                string.IsNullOrWhiteSpace(record.GameType))
            {
                continue;
            }

            if (!DateOnly.TryParse(record.GameDay, CultureInfo.InvariantCulture, out var gameDay))
            {
                continue;
            }

            games[record.GameId] = new NflGameSchedule
            {
                GameId = record.GameId,
                Season = record.Season,
                Week = record.Week,
                GameType = record.GameType,
                AwayTeam = record.AwayTeam,
                HomeTeam = record.HomeTeam,
                GameDay = gameDay,
            };
        }

        return games;
    }

    private static readonly CsvConfiguration CsvConfiguration = new(CultureInfo.InvariantCulture)
    {
        PrepareHeaderForMatch = args => args.Header.ToLowerInvariant(),
        MissingFieldFound = null,
        HeaderValidated = null,
    };

    private sealed class ScheduleRow
    {
        [Name("game_id")]
        public string GameId { get; init; } = string.Empty;

        [Name("season")]
        public int Season { get; init; }

        [Name("week")]
        public int Week { get; init; }

        [Name("game_type")]
        public string GameType { get; init; } = string.Empty;

        [Name("away_team")]
        public string AwayTeam { get; init; } = string.Empty;

        [Name("home_team")]
        public string HomeTeam { get; init; } = string.Empty;

        [Name("gameday")]
        public string GameDay { get; init; } = string.Empty;
    }
}
