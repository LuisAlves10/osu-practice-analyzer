# Fontes e limitações

Consulta em 2026-11-15. A matriz usa CONFIRMADO_EM_TESTE_REAL, DOCUMENTADO_NAO_TESTADO, EXPERIMENTAL, NAO_SUPORTADO e DESCONHECIDO. Veja [STATUS.md](STATUS.md) para a execução local e os comandos da Fase 2.

| Capacidade | Estado ao fim da Fase 2 | Evidência e critério de confirmação |
| --- | --- | --- |
| Encontrar dados do lazer no caminho padrão Windows | CONFIRMADO_EM_TESTE_REAL | osu!lazer 2026.1005.0, diretório de dados padrão com índice e blobs SHA-256. |
| Selecionar diretório personalizado | DOCUMENTADO_NAO_TESTADO | O lazer lê `storage.ini` via `StorageConfig.FullPath`; testar um caminho explicitamente escolhido e validar o depósito. [OsuStorage] e [StorageConfig]. |
| Obter identidade de conta e isolar perfis | DOCUMENTADO_NAO_TESTADO | tosu expõe perfil; lazer pode carregar ID no bloco estendido de replay. Confirmar troca de perfil, conta offline e nomes alterados antes de associar. [Tosu v2] e [Decoder]. |
| Identificar versões de dificuldade e ler IDs declarados | CONFIRMADO_EM_TESTE_REAL | O spike leu mapas presentes no lazer 2026.1005.0 e conferiu os bytes por SHA-256. Edições homônimas com os mesmos IDs foram testadas com fixture sintética. Catálogo e vínculo de set por origem seguem DOCUMENTADO_NAO_TESTADO. [File store], [BeatmapInfo] e [Formato .osu]. |
| Histórico de scores e vínculo ao mapa | DOCUMENTADO_NAO_TESTADO | Score local possui vínculo por hash e jogo exporta replay quando há asset. Enumerar scores depende de estratégia de snapshot consistente e schema versionado. [BeatmapInfo] e [Exportador]. |
| Localizar replay e identificar a dificuldade | CONFIRMADO_EM_TESTE_REAL | No lazer 2026.1005.0, o spike identificou contêineres estendidos standard e vinculou replays reais a versões por MD5 com integridade SHA-256. Rejeição de checksum divergente exercitada com fixture. Decode de frames segue pendente. [Exportador], [Encoder] e [Formato .osr]. |
| .osr stable e versões fora da faixa revisada | NAO_SUPORTADO | Este spike aceita versões de contêiner estendido 30000001–30000019 no ruleset standard. [Encoder]. |
| Observar playing / resultado / flag de fail | DOCUMENTADO_NAO_TESTADO | tosu v4.26.3 expõe estados e `play.failed`. A flag pertence ao estado de jogo e pode ser resetada. Resultado confirma tela observada. Validar sequência e significado em uma sessão com versão conhecida. [Enum tosu], [Tosu v2] e [LazerInstance]. |
| Distinguir retry explícito, abort, pass e replay assistido | DESCONHECIDO | A implementação reseta telemetria ao recuar o relógio, também observável com seek. O contrato revisado não fornece ID de tentativa nem sinal inequívoco para cada transição. Tratar recuo como lacuna experimental e guardar a incerteza. [Tosu v2] e [LazerInstance]. |
| Mods, rate, OD/AR/CS/HP e scoring efetivos | DOCUMENTADO_NAO_TESTADO | tosu expõe atributos e mods/rate. Lazer lê mods parametrizados, estatísticas e scoring no bloco estendido. Confirmar semântica por modalidade e versão com controles no jogo. [Tosu v2] e [Decoder]. |
| Timing, teclas e cursor | DOCUMENTADO_NAO_TESTADO | v2/precise pode expor hit errors e teclas por amostras. O replay tem deltas temporais, coordenadas e máscara de botões. Confirmar cobertura e escala de tempo com fixtures reais. [Tosu precise], [Encoder] e [Formato .osr]. |
| Julgamento por objeto, padrões e trechos | EXPERIMENTAL | Os projetos especialistas demonstram abordagens com limites. Validar contra contagens gravadas, com mods configuráveis, slider ticks/tails, spinners e score system. [osu-critique] e [OSRA]. |
| Perfil, top/recent, mapas e snapshots online | DOCUMENTADO_NAO_TESTADO | A API v2 documenta perfil, estatísticas, histórico de rank e pontuações; `include_fails` é para recentes. Usar timestamps de observação, cache e backoff. [osu! API]. |
| OAuth público de desktop sem secret embutido | DESCONHECIDO | API v2 documenta authorization code e client credentials com secret. PKCE não aparece no contrato consultado. A integração autenticada depende de uma estratégia pública validada. [OAuth]. |
| Cruzar score local/online, deduplicar e retomar checkpoint | DESCONHECIDO | Usar IDs verificados e perfis isolados. Testar reimportação, candidatos ambíguos e reinício na Fase 3. |
| Operar sem tosu; lidar com queda ou reconectar | EXPERIMENTAL | Captura em loopback indisponível no ambiente e fechamento com resultado desconhecido no servidor sintético. Reconexão e persistência transacional seguem para a Fase 3. |
| CPU, memória e disco durante uma partida | DESCONHECIDO | Medir baseline e execução com janela, hardware e versões fixos, antes de habilitar coleta contínua. |

## Revisões, licenças e cautelas

- osu!lazer tem código MIT. A revisão pública mais recente consultada foi `2026.1115.0-lazer` (`169c0208248cf562a7c75a7a431e51c0e3dac0a1`). As referências de arquivos abaixo fixam `2026.1005.0-lazer`, versão encontrada localmente. A camada de Realm nessa versão declara schema 52 e prepara migrações na abertura pelo cliente. `client.realm` é o nome-base e pode haver arquivo versionado. Conteúdo dos beatmaps, replays e músicas tem direitos separados da licença do código. [Releases], [Licença lazer], [RealmAccess] e [Wiki de armazenamento].
- tosu v4.26.3 (`1d811dd90af00c7f0d8551a0735d9ce779a6e773`) é LGPL-3.0. O README apresenta loopback 24050 e endpoints `/json/v2`, `/websocket/v2` e `/websocket/v2/precise`. A versão dentro de `settings.client.version` é do jogo. Registrar versão do processo externo de forma separada e validar forma do payload. As regras oficiais condicionam uso de utilitários à ausência de vantagem. O projeto consome apenas loopback pós-análise e não fornece assistência durante gameplay. Avaliar compatibilidade da ferramenta externa com fair play antes de recomendar seu uso. [README tosu], [Licença tosu], [Rotas HTTP], [Rotas WS] e [Regras].
- A wiki v2 do tosu tem exemplo público de menu, revisão da wiki `b49e8db2b5580be4e8b5dffbed851f6e96314b93`. O exemplo antecede a versão avaliada e omite campos atuais. Usar um recorte de contrato sem perfil, caminhos ou configuração pessoal para regressão. O exemplo não cobre uma partida. [Exemplo v2].
- API oficial datada de 2026-11-13. Os termos orientam cache, backoff e coleta moderada, com exemplos de abuso por polling repetido e coleta em massa. Não foi estabelecida uma cota geral adequada ao app. Nenhuma chamada autenticada foi executada. [Termos] e [osu! API].
- `osu-critique` MIT, revisão `7a87aca1b3f4c4f532a8a890d6e7b93968d1453e`, documenta comparação de contagens com corpus e limites de sliders. `NeekoVN/osra` MIT, revisão `e1c417f94aeba5ea2901183c12cc5052cdd7de12`, é o OSRA identificado, com telemetria browser, associação por MD5 e motor de dificuldade via WASM. O README do `osrparse` MIT, revisão `dc47136bb3742b792e944b1be602a2fa525ff7d0`, descreve estrutura legada, insuficiente como evidência de paridade do bloco estendido. O código de decode/julgamento oficial é a referência técnica para compatibilidade futura. Nenhum desses componentes foi incorporado. [osu-critique], [OSRA] e [osrparse].

[Releases]: https://github.com/ppy/osu/releases/tag/2026.1115.0-lazer
[Licença lazer]: https://github.com/ppy/osu/blob/2026.1005.0-lazer/LICENCE
[Wiki de armazenamento]: https://osu.ppy.sh/wiki/en/Client/Release_stream/Lazer/File_storage
[Formato .osu]: https://osu.ppy.sh/wiki/en/Client/File_formats/osu_%28file_format%29
[Formato .osr]: https://osu.ppy.sh/wiki/en/Client/File_formats/osr_%28file_format%29
[File store]: https://github.com/ppy/osu/blob/2026.1005.0-lazer/osu.Game/Database/RealmFileStore.cs
[OsuStorage]: https://github.com/ppy/osu/blob/2026.1005.0-lazer/osu.Game/IO/OsuStorage.cs
[StorageConfig]: https://github.com/ppy/osu/blob/2026.1005.0-lazer/osu.Game/Configuration/StorageConfigManager.cs
[RealmAccess]: https://github.com/ppy/osu/blob/2026.1005.0-lazer/osu.Game/Database/RealmAccess.cs
[BeatmapInfo]: https://github.com/ppy/osu/blob/2026.1005.0-lazer/osu.Game/Beatmaps/BeatmapInfo.cs
[Exportador]: https://github.com/ppy/osu/blob/2026.1005.0-lazer/osu.Game/Database/LegacyScoreExporter.cs
[Encoder]: https://github.com/ppy/osu/blob/2026.1005.0-lazer/osu.Game/Scoring/Legacy/LegacyScoreEncoder.cs
[Decoder]: https://github.com/ppy/osu/blob/2026.1005.0-lazer/osu.Game/Scoring/Legacy/LegacyScoreDecoder.cs
[README tosu]: https://github.com/tosuapp/tosu/blob/v4.26.3/README.md
[Licença tosu]: https://github.com/tosuapp/tosu/blob/v4.26.3/LICENSE
[Enum tosu]: https://github.com/tosuapp/tosu/blob/v4.26.3/packages/common/enums/osu.ts
[Tosu v2]: https://github.com/tosuapp/tosu/blob/v4.26.3/packages/tosu/src/api/utils/buildResultV2.ts
[LazerInstance]: https://github.com/tosuapp/tosu/blob/v4.26.3/packages/tosu/src/instances/lazerInstance.ts
[Rotas HTTP]: https://github.com/tosuapp/tosu/blob/v4.26.3/packages/server/router/v2.ts
[Rotas WS]: https://github.com/tosuapp/tosu/blob/v4.26.3/packages/server/router/socket.ts
[Exemplo v2]: https://github.com/tosuapp/tosu/wiki/v2-websocket-api-response
[Tosu precise]: https://github.com/tosuapp/tosu/wiki/v2-precise-websocket-api-response
[Regras]: https://osu.ppy.sh/wiki/en/Rules
[osu! API]: https://osu.ppy.sh/docs/
[OAuth]: https://osu.ppy.sh/docs/#authentication
[Termos]: https://osu.ppy.sh/docs/#terms-of-use
[osu-critique]: https://github.com/ranelb/osu-critique/tree/7a87aca1b3f4c4f532a8a890d6e7b93968d1453e
[OSRA]: https://github.com/NeekoVN/osra/tree/e1c417f94aeba5ea2901183c12cc5052cdd7de12
[osrparse]: https://github.com/kszlim/osu-replay-parser/tree/dc47136bb3742b792e944b1be602a2fa525ff7d0
