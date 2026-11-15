# Status

## 2026-11-15

- **Fase 0 concluída.** AGENTS, licença MIT para material original e documentação inicial publicados no [repositório público](https://github.com/LuisAlves10/osu-practice-analyzer), branch `main`, commit `6147d10`. Visibilidade, índice e histórico auditados.
- **Fase 1 concluída.** Referência de engenharia privada inspecionada via GitHub autenticado em modo leitura. [Matriz](FONTES_E_LIMITACOES.md), [arquitetura](ARQUITETURA.md), [ADRs](DECISOES.md) e [modelo inicial](MODELO_DE_DADOS.md) documentados, commit `e7564f1`. Stack escolhida: C#/.NET 10, WPF e SQLite.
- **Fase 2 concluída no escopo experimental.** O [guia de provas](PROVAS_TECNICAS.md) descreve comandos e critérios. SQLite, interface e analytics pertencem aos gates seguintes.

## Verificação executada

| Verificação | Resultado |
| --- | --- |
| `dotnet build spikes/SourceSpikes -c Release` | .NET SDK 10.0.404, runtime 10.0.15. 0 erros e 0 warnings. SDK estável extraído no scratch após conferir SHA-512 oficial. |
| `dotnet test tests/SourceSpikes.Tests -c Release` | 13 passaram. Inclui edição com mesmo ID, checksum incorreto, versões de replay fora do contrato, contêiner truncado, seleção correta da versão, amostra upstream de menu, transições sintéticas e WebSocket de teste fragmentado com perda de janela e sanitização. O teste de truncamento inicialmente revelou uma exceção sem tratamento. Foi corrigida e a suíte passou. |
| `dotnet run --no-build -c Release --project spikes/SourceSpikes -- history` | **Teste real** no lazer 2026.1005.0 com o cliente aberto. SHA-256 dos blobs candidatos verificado. Vínculos únicos pelo MD5 foram confirmados em múltiplas dificuldades, inclusive dois ou mais replays distintos na mesma versão. Houve replays sem correspondente. O depósito recebeu novos arquivos entre duas execuções, então cada resultado descreve uma amostra do armazenamento durante a varredura. |
| `dotnet run --no-build -c Release --project spikes/SourceSpikes -- tosu 24050 3` | Loopback indisponível, código 1, `provider_unavailable`, zero mensagens e zero resultados inferidos. O teste integrado em servidor sintético detectou uma janela interrompida e registrou `unknown`. |

As saídas agregadas exatas do teste real, o JSONL de diagnóstico e os logs de teste foram salvos no `scratch/` local e ignorado. Para reproduzir as provas, use o SDK padrão ou o executável `scratch/dotnet/dotnet.exe` na estação em que ele já estiver disponível.

**Limitações abertas** — validação do tosu em partidas reais e com mudança de perfil, descoberta automática de pasta personalizada, snapshots seguros de Realm, importação de scores sem replay, mods e frames descompactados, julgamento, custo em jogo e OAuth desktop.

## Gate da Fase 3

Implementados SQLite com duas migrações, isolamento de perfis, ingestão incremental de assets, RAW reprocessável e projeções de vínculo, classificação e desempenho. A captura opcional tem um contrato de persistência por UUID de captura e sequência. [Persistência](PERSISTENCIA.md) · [Regra de contabilização](ANALYTICS.md)

- **Teste real** — importação, reabertura em processo independente e reimportação no mesmo perfil preservaram as contagens. Dois replays distintos na mesma versão foram conferidos novamente lendo os bytes persistidos e comparando SHA-256 e MD5. `PRAGMA integrity_check` retornou `ok`; `foreign_key_check` não retornou violações. Os resultados ficaram UNKNOWN e fora das estatísticas principais. As contagens e o banco privados ficaram em `scratch/`.
- **Testes automatizados** — 20 passaram em Release. Cobrem persistência após reabertura, migração V1–V2, rollback em escrita interrompida, cancelamento, arquivos corrompidos, mapa ausente e vínculo ambíguo (colisão simulada), deduplicação por origem, isolamento, atualização de resultado com novas evidências, conflito de resultados e cobertura das estatísticas. O build Release do CLI passou com zero erros e warnings.
- **Limitações** — PASS, FAIL, RESTART e QUIT foram validados com evidências sintéticas. O lazer real forneceu arquivos de replay, sem classificação de resultado. Scores históricos sem replay, captura real do tosu e reconexão do consumidor continuam pendentes. A varredura descobre arquivos relendo o depósito a cada importação. O perfil é selecionado explicitamente e a origem de arquivo não carrega identidade confirmada do jogador.

**Próxima ação** — na Fase 4, carregar a biblioteca por beatmapset, dificuldade e versão e abrir a comparação de dois registros reais da mesma dificuldade, com classificação e cobertura visíveis. Fase 4 ainda não iniciada.
