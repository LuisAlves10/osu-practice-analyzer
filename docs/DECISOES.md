# Decisões

## ADR-000 — Licença do bootstrap, 2026-11-15

MIT para a documentação e código original. O bootstrap não incorpora dependências, trechos de terceiros ou assets do jogo. A revisão por componente antecede qualquer incorporação ou distribuição. [Texto e identificador da licença](https://opensource.org/license/mit).

## ADR-001 — C#/.NET 10, WPF e SQLite para V1, 2026-11-15

**Decisão** — usar .NET 10 LTS, C# e WPF para a aplicação Windows 11. Preferir Microsoft.Data.Sqlite na Fase 3, com migração explícita e integridade referencial. Os spikes são CLI com a BCL do .NET. O runtime de desktop, portabilidade do pacote e desempenho em jogo serão medidos em seus gates.

| Opção | Adequação e custo |
| --- | --- |
| .NET 10 + WPF + SQLite | Mesmo ecossistema do lazer para verificar contratos e reaproveitar bibliotecas compatíveis no futuro. BCL cobre WebSocket, JSON, streams e cancelamento. WPF atende a plataforma V1 e a distribuição pode usar publicação self-contained. Microsoft.Data.Sqlite tem integração documentada. Avaliar dependências de replay e eventual leitor de snapshot isoladamente. |
| Python + PySide6 + SQLite | Bom para prototipagem e análises. A inspeção do logger mostrou viabilidade de app e pacote. PySide6 adiciona distribuição de Python/Qt e revisão de licenças por módulo. A cobertura do parser Python revisado para o bloco estendido do lazer ainda precisaria de prova. |
| Tauri 2 + React/TS + Rust + SQLite | Bom suporte a UI web e instaladores Windows. Adiciona WebView2, C++ Build Tools, Rust e fronteira entre UI e serviços. Os recursos de replay revisados concentram-se em casos específicos; compatibilidade com armazenamento do lazer continua uma investigação. |

Escolhemos .NET pelo escopo Windows fixo e pela afinidade com as fontes que exigem validação. Nenhuma estimativa de desempenho comparativo foi usada. A integração de Realm fica fora da resposta de stack. O pipeline versionado e as provas de schema/segurança é que determinam se ela é viável.

Fontes consultadas em 2026-11-15. [Suporte .NET](https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-and-net-core), [WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/), [publicação .NET](https://learn.microsoft.com/en-us/dotnet/core/deploying/), [Microsoft.Data.Sqlite](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/), [deploy PySide6](https://doc.qt.io/qtforpython-6/deployment/deployment-pyside6-deploy.html), [licenças Qt for Python](https://doc.qt.io/qtforpython-6/licenses.html), [pré-requisitos Tauri](https://v2.tauri.app/start/prerequisites/) e [instaladores Tauri](https://v2.tauri.app/distribute/windows-installer/).
