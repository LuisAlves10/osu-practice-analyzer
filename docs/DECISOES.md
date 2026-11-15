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

## ADR-002 — Priorizar histórico por arquivos e manter tosu opcional, 2026-11-15

Começar a ingestão com pares de mapa e replay selecionados no perfil local, preservando SHA-256, checksum de vínculo e proveniência. A associação foi demonstrada no depósito do lazer sem consultar Realm. Para uso normal, priorizar arquivos exportados e referências a assets já recuperados. Catálogo integral e scores sem replay exigem prova separada de uma fonte segura. Eliminar duplicatas de importação pelo ID de origem e hash do asset, preservando tentativas distinguíveis.

O WebSocket tosu será um adaptador independente com observações temporais. Um estado de jogo, uma flag explícita, um recuo de relógio e uma tela de resultados são evidências distintas. Guardar a janela indeterminada se faltar confirmação. Uma classificação automática de pass, restart ou abort depende de captura real controlada. [Evidências e reprodução](PROVAS_TECNICAS.md).

O executável da Fase 2 usa a biblioteca padrão .NET. xUnit 2.9.3 e seu runner 3.1.4 (Apache-2.0), Microsoft.NET.Test.Sdk 17.14.1 e ASP.NET Core (MIT) são dependências de teste. Os códigos dos provedores e seus assets continuam externos. [xUnit](https://www.nuget.org/packages/xunit/2.9.3), [runner](https://www.nuget.org/packages/xunit.runner.visualstudio/3.1.4), [Test SDK](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/17.14.1) e [licença ASP.NET Core](https://github.com/dotnet/aspnetcore/blob/main/LICENSE.txt).

## ADR-003 — SQLite e classificação por evidência

Usar Microsoft.Data.Sqlite 10.0.15 com migrações transacionais, chaves estrangeiras e perfis explícitos. Guardar bytes de mapas e replays junto da normalização no mesmo banco para preservar atomicidade e permitir reprocessamento mesmo se a fonte desaparecer. O custo é espaço local adicional. A identidade da origem de arquivo e a de observações são independentes. Vínculo de mapa e classificação são projeções derivadas. [Dependência SQLite](https://www.nuget.org/packages/Microsoft.Data.Sqlite/10.0.15) e [contrato analítico](ANALYTICS.md).
