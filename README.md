# ml-sports-analysis

Machine-learning sports analysis tooling. Currently includes an NFL data pipeline powered by [nflverse](https://github.com/nflverse/nflverse-data) public datasets.

## Projects

| Project | Path | Description |
|---------|------|-------------|
| **MlNFL.Downloader** | `src/MlNFL.Downloader/` | Downloads play-by-play and player stats, split into per-game CSVs |

## Prerequisites

- [.NET SDK 9+](https://dotnet.microsoft.com/download)

## Download NFL data

From the repository root:

```powershell
dotnet run --project src/MlNFL.Downloader -- download --season 2024
```

Multiple seasons:

```powershell
dotnet run --project src/MlNFL.Downloader -- download -s 2023 -s 2024
```

Output layout:

```text
data/games/<year>/<month>/<day>/<game_id>/play-by-play.csv
data/games/<year>/<month>/<day>/<game_id>/stats.csv
```

Game folders use the nflverse schedule `gameday` (calendar date), not the season year embedded in `game_id`.

## Build

```powershell
dotnet build
```

## Data sources

No API key required. Data is fetched from nflverse GitHub releases:

- Schedules: `nflverse/nfldata`
- Play-by-play: `nflverse/nflverse-data` (1999–present)
- Player stats: `nflverse/nflverse-data`

Downloaded CSVs under `data/` are gitignored; run the downloader locally to populate them.
