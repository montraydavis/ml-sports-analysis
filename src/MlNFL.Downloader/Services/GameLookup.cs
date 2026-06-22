using MlNFL.Downloader.Models;

namespace MlNFL.Downloader.Services;

internal sealed class GameLookup
{
    private readonly Dictionary<ParticipantKey, string> _participantToGameId = new();
    private readonly Dictionary<string, NflGameSchedule> _schedules;

    public GameLookup(IReadOnlyDictionary<string, NflGameSchedule> schedules)
    {
        _schedules = schedules.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);

        foreach (var schedule in schedules.Values)
        {
            _participantToGameId[new ParticipantKey(
                schedule.Season,
                schedule.Week,
                schedule.GameType,
                schedule.AwayTeam)] = schedule.GameId;

            _participantToGameId[new ParticipantKey(
                schedule.Season,
                schedule.Week,
                schedule.GameType,
                schedule.HomeTeam)] = schedule.GameId;
        }
    }

    public string? ResolveGameId(
        int season,
        int week,
        string seasonType,
        string team,
        string opponentTeam)
    {
        if (!_participantToGameId.TryGetValue(
                new ParticipantKey(season, week, seasonType, team),
                out var gameId))
        {
            return null;
        }

        if (!_schedules.TryGetValue(gameId, out var schedule))
        {
            return null;
        }

        var opponentMatches =
            string.Equals(schedule.AwayTeam, opponentTeam, StringComparison.Ordinal) ||
            string.Equals(schedule.HomeTeam, opponentTeam, StringComparison.Ordinal);

        return opponentMatches ? gameId : null;
    }

    private readonly record struct ParticipantKey(
        int Season,
        int Week,
        string SeasonType,
        string Team);
}
