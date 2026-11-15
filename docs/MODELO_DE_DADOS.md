# Modelo de dados

Na Fase 3, SQLite persiste perfis, versões de mapa, replays, tentativas, scores identificados por origem e observações RAW. A [persistência](PERSISTENCIA.md) descreve as migrações e a importação. Os demais contratos abaixo orientam os próximos gates.

| Entidade | Identidade e contrato inicial |
| --- | --- |
| LocalProfile / OnlineAccount | UUID local. Vínculo explícito ao ID numérico do provedor após confirmação. Nome é atributo observável e pode mudar. Origens ambíguas permanecem isoladas até revisão. |
| BeatmapSet / BeatmapDifficulty | IDs locais persistentes com namespace da origem e IDs oficiais opcionais. Títulos e IDs declarados num arquivo não confirmam identidade pública. Agrupar mapas locais pelo contêiner de origem quando disponível. |
| BeatmapVersion | Ruleset nativo + SHA-256 dos bytes, MD5 para vínculo legado e metadados de origem. Identificar múltiplos candidatos MD5 como ambíguos. Ruleset efetivo da tentativa fica separado do nativo do mapa. |
| PlayAttempt | UUID local. Perfil, dificuldade/versão quando verificadas, ruleset efetivo, cliente, janela de tempo UTC, cobertura, evidência de início/fim e categoria. Mods parametrizados e scoring carregam sua origem. |
| SavedScore / ReplayAsset | IDs independentes. Score depende da origem e pode não ter replay. Replay tem SHA-256 de asset e MD5 da versão de mapa. Apenas vincular ao score ou à tentativa por evidência rastreável. |
| RawObservation / SyncCheckpoint | Namespace, versão, instante UTC de recebimento, referência de origem, hash do payload, sequência local quando aplicável, forma de sanitização, checkpoint e janela de cobertura. |
| AnalysisResult / Evidence / TrainingEvaluation | Tipo de dado, versão de método, inputs, amostra, filtros, comparabilidade, gaps e referência a reteste. Checkpoints posteriores podem recalcular sobre os mesmos inputs. |
| OnlineProfileSnapshot / Session | Snapshot temporal por conta, ruleset e fonte; sessão é agrupamento derivado por tempo e perfil. |

Repetir importação usa chave de observação estável e identificador de origem dentro do perfil. O hash do payload comprova integridade e pode detectar duplicata de arquivo. Respostas idênticas em instantes distintos e partidas com score igual ainda podem representar tentativas distintas. Aproximação de horário, score e mapa só cria candidatos de vínculo. Preservar timezone quando conhecido e registrar chegada separadamente do relógio de origem. Campos ausentes são nulos com motivo.

O domínio separa o observado (FACT), a amostra temporal externa (FACT_SNAPSHOT), a medida calculada (DERIVED) e a interpretação (INFERENCE). UNAVAILABLE carrega motivo. A cobertura deve dizer o que foi observado no período, por fonte e módulo, inclusive janelas offline e tentativas que ficaram abertas.

Resultado é uma projeção versionada das evidências. PASS, FAIL, RESTART e QUIT dependem de fatos explícitos. Ausência ou conflito gera UNKNOWN. Elegibilidade e cobertura das métricas são definidas no [contrato analítico](ANALYTICS.md).
