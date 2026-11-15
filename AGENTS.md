# Development guide

## Mission and scope

Build an offline-first, open-source Windows 11 desktop application that helps osu!lazer standard players identify training priorities and measure improvement and transfer. The library starts at the beatmapset, then difficulty and file version, then attempts. Keep the ruleset explicit in all source and domain contracts.

## Source of truth and safety

- Start each run with this file, [status](docs/STATUS.md), [roadmap](docs/ROADMAP.md), and the owning documents for the current phase. Resolve contract changes in their owning documents and log durable decisions in [ADRs](docs/DECISOES.md).
- Local observations are primary. Preserve source, observation time, completeness and version. Distinguish FACT, FACT_SNAPSHOT, DERIVED, INFERENCE and UNAVAILABLE. Keep unavailable fields nullable with a reason. Treat attempt, score and replay as distinct entities.
- Use exported files or verified read-only source operations. Never modify game files, touch live Realm without a validated safe strategy, read game memory directly, inject code, automate input, or assist during gameplay. tosu is a separate optional loopback source.
- Keep the private master specification, real replays/maps, player data, logs, credentials, and raw fixtures out of Git and public artifacts. Keep the app local by default. Tests may use labeled synthetic inputs. Production features must use real data and fail honestly.

## Structure and commands

- docs/ owns product scope, architecture, source evidence, data contracts, phase gates and accepted decisions.
- Keep phase-2 probes under spikes/ and tests under tests/. Production code will live under src/ with independent source adapters, raw and normalized storage, domain, replay, analytics, training, exports, online sync, and desktop app boundaries.
- Install a stable .NET 10 SDK. Run `dotnet restore tests/SourceSpikes.Tests`, `dotnet build spikes/SourceSpikes -c Release`, `dotnet test tests/SourceSpikes.Tests -c Release`, and `git diff --check`. Reproduce source checks with [the probe guide](docs/PROVAS_TECNICAS.md).
- Minimum probe tests cover content identity, a mismatched map, absent or invalid provider data, and a lost connection ending in unknown. Start persistence tests with repeat import, profile isolation, and two distinct attempts on the same map.

## Phase and Git protocol

Finish each gate before starting the next. The initial run ends at phase 2. Record commands and actual outcomes, source versions, gaps and the exact next action in docs/STATUS.md at each gate. Distinguish live evidence from fixture tests. Preserve unrelated files and projects.

Use small commits with reviewed paths. Before each commit, read the workstation Git writing reference when available, inspect status, staged diff, tracked files and reachable history for private material and secrets, then stage only intended files. Repeat the privacy audit before pushing. Verify remote owner and visibility before publication.
