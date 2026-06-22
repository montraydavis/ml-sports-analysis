---
name: nfl-data-downloader
description: >-
  Run the MlNFL.Downloader console app to fetch nflverse NFL play-by-play and
  player stats into per-game CSVs. Use when the user asks to download NFL data,
  sync game stats, backfill seasons, run MlNFL.Downloader, refresh data/games,
  or populate play-by-play/stats CSV files.
---

# NFL Data Downloader

Run **`src/MlNFL.Downloader`** from the **repository root**. It downloads public nflverse data (no API key), splits by game, and writes CSVs.

## Prerequisites

- **.NET SDK 9+** (`dotnet --version`)
- **Network access** to GitHub (`nflverse/nfldata`, `nflverse/nflverse-data`)
- Run commands from the repo root (where `MlNFL.sln` lives)

## Command shape

```text
dotnet run --project src/MlNFL.Downloader -- download [options]
```

Everything after `--` is passed to the app. The first app argument must be the subcommand **`download`**.

### Required

| Flag | Short | Value | Notes |
|------|-------|-------|-------|
| `--season` | `-s` | 4-digit year | Repeat for multiple seasons. **Minimum: 1999**. |

At least one `--season` is required. Duplicate seasons are deduplicated.

### Optional

| Flag | Short | Value | Default | Notes |
|------|-------|-------|---------|-------|
| `--output` | `-o` | directory path | `data/games` | Root for all output CSVs |
| `--force` | `-f` | (none) | off | Overwrite existing per-game files |
| `--help` | `-h` | (none) | — | Print usage and exit |

### Positional shorthand

A bare integer after `download` is treated as a season:

```text
dotnet run --project src/MlNFL.Downloader -- download 2024
```

Equivalent to `--season 2024`.

## Copy-paste examples

**Single season (default output):**

```powershell
dotnet run --project src/MlNFL.Downloader -- download --season 2024
```

**Multiple seasons:**

```powershell
dotnet run --project src/MlNFL.Downloader -- download -s 2023 -s 2024
```

**Custom output directory:**

```powershell
dotnet run --project src/MlNFL.Downloader -- download --season 2024 -o data/games
```

**Re-download and overwrite existing files:**

```powershell
dotnet run --project src/MlNFL.Downloader -- download --season 2024 --force
```

**Show built-in help:**

```powershell
dotnet run --project src/MlNFL.Downloader -- --help
```

Note the extra `--` before `--help` so the flag is passed to the app, not `dotnet run`.

**Run compiled binary (after `dotnet build`):**

```powershell
dotnet build
.\src\MlNFL.Downloader\bin\Debug\net9.0\MlNFL.Downloader.exe download --season 2024
```

On Linux/macOS, drop `.exe`.

## Output layout

Files are written under `<output>/<calendar-year>/<month>/<day>/<game_id>/`:

```text
data/games/2024/09/08/2024_01_PIT_ATL/play-by-play.csv
data/games/2024/09/08/2024_01_PIT_ATL/stats.csv
```

- **Calendar folders** come from nflverse schedule `gameday` (not the `season` year in `game_id`).
- **`<game_id>` subfolder** avoids collisions when multiple games share a date (typical Sunday slates).
- **`play-by-play.csv`**: one row per play; includes `game_id` column.
- **`stats.csv`**: player box-score rows for that game (resolved via season/week/team/opponent).

## What the agent should do

1. **Confirm repo root** — `MlNFL.sln` and `src/MlNFL.Downloader/` exist.
2. **Infer seasons** from the user request; if unclear, ask or default to the most recent completed season.
3. **Run the downloader** via Shell (do not only tell the user to run it).
4. **Use `--force`** only when the user wants a full refresh or files are corrupt/incomplete.
5. **Allow time** — each season downloads large gzip CSVs (~1–2 min per season on a typical connection). Set a long shell timeout (e.g. 5+ minutes per season).
6. **Report results** from console output: games written/skipped, row counts, and any "not found" for unpublished seasons.

## Expected console output

```text
Loading schedules...
Loaded 285 games for season(s): 2024.

Season 2024 (285 games)
  Downloading play-by-play.csv... wrote 285 game file(s), skipped 0 existing game(s), 49492 row(s) (0 row(s) skipped).
  Downloading stats.csv... wrote 272 game file(s), skipped 0 existing game(s), 18128 row(s) (853 row(s) skipped).
Done.
```

### Incremental runs (no `--force`)

If files already exist, the app **skips** those games and reports `skipped N existing game(s)`. This is normal for idempotent syncs.

## Exit codes

| Code | Meaning |
|------|---------|
| `0` | Success |
| `1` | Invalid args, unknown command, or missing `--season` |
| `130` | Cancelled (Ctrl+C) |

## Data sources (read-only, no key)

| File | URL |
|------|-----|
| Schedules | `https://github.com/nflverse/nfldata/raw/master/data/games.csv` |
| Play-by-play | `https://github.com/nflverse/nflverse-data/releases/download/pbp/play_by_play_{year}.csv.gz` |
| Player stats | `https://github.com/nflverse/nflverse-data/releases/download/stats_player/stats_player_week_{year}.csv.gz` |

## Troubleshooting

| Symptom | Action |
|---------|--------|
| `Invalid season(s): ... nflverse play-by-play starts in 1999` | Use `--season 1999` or later |
| `not found (season may not be published yet)` | nflverse has not released that season's file; try prior year or retry later |
| Stats game count < PBP game count | Expected — some games have no player-stat rows in nflverse source |
| `At least one --season value is required` | Add `-s <year>` or positional year after `download` |
| Build fails | Run `dotnet build` from repo root; requires .NET 9 SDK |
| Slow or timeout | Re-run with fewer seasons; increase shell `block_until_ms` |

## Do not

- Commit `data/games/` unless the user explicitly asks (it is gitignored).
- Use `api.nfl.com` or paid APIs for this workflow — this app uses nflverse only.
- Flatten output to a single `stats.csv` per day without `game_id` — Sunday slates overwrite each other.

## Verify after download

Spot-check one game directory:

```powershell
Get-ChildItem data/games/2024/09/08/2024_01_PIT_ATL
```

Expect both `play-by-play.csv` and `stats.csv`.
