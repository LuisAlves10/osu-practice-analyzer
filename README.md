# osu-practice-analyzer

An early-stage, local-first project for players practicing osu!standard in osu!lazer on Windows 11. The goal is to find repeatable difficulties across maps, choose targeted practice, and measure whether improvements transfer.

Independent project. It is unaffiliated with osu! or its developers.

## Development

Use the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). From the repository root:

```powershell
dotnet restore tests/SourceSpikes.Tests
dotnet build spikes/SourceSpikes -c Release
dotnet test tests/SourceSpikes.Tests -c Release
```

Phase 2 provides two isolated command-line probes. History reads the lazer content store and reports SHA-256 verification and replay-to-map matches. The optional capture probe connects to the local tosu v2 WebSocket. Follow the [probe guide](docs/PROVAS_TECNICAS.md) for commands and expected results. Current evidence and limitations are in [docs/STATUS.md](docs/STATUS.md). The [product scope](docs/PRODUTO.md) and [roadmap](docs/ROADMAP.md) are available in Brazilian Portuguese.

The project source is under the [MIT License](LICENSE).
