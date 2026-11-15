# Arquitetura

.NET 10 e C# para a V1. WPF para a futura camada desktop e SQLite para a persistência local. [ADR de stack](DECISOES.md) e [contratos de domínio](MODELO_DE_DADOS.md).

| Limite | Responsabilidade |
| --- | --- |
| Adaptadores | Lazer e tosu com contratos independentes, relógio de recebimento e versão. As provas usam leitura de arquivo e loopback. |
| RAW e normalização | Preservar origem e integridade. Identificar formato/capacidades e transformar deterministicamente para o domínio. Sanitizar telemetria já na entrada. |
| Domínio e persistência | Perfis isolados, versões de mapa, tentativas/score/replay, consistência SQLite, reprocessamento e checkpoints. |
| Replay, analytics e treinamento | Motor isolado da fonte, reconciliação com resultados, regras de comparabilidade e justificativa de exercícios. |
| Online, export e UI | Contratos próprios. SQLite e tarefas pesadas fora da thread da UI. Login e tosu opcionais. Exportação manual e ciência do conteúdo. |

## Referência de engenharia

Foi consultado, via GitHub autenticado em modo leitura, o logger de partidas usado como referência privada. A inspeção cobriu arquitetura, schema, armazenamento, reprocessamento, recuperação, exportações, testes e auditoria visual. O padrão reaproveitado é manter observações originais reprocessáveis, usar SQLite para consultas históricas e produzir um JSON completo associado a um Markdown compacto. Idempotência, diagnósticos sanitizados e cancelamento com progresso persistido passam aos requisitos de implementação.

A auditoria do logger descreve uma ação primária de exportação e status de fonte visível, além de cenários de gravação cuja sessão ao vivo ainda exigia validação. Aqui a navegação morará em beatmapset/dificuldade, e evidência de widget ou fixture será reportada separadamente de captura ao vivo. O repositório consultado não foi alterado e nenhum código ou conteúdo privado foi incorporado.

## Planos e riscos da prova

- A — ler apenas arquivos de conteúdo no depósito SHA-256 ou arquivos exportados. Testar identidade de mapa e vínculo MD5 de replay com regraset consistente. Realm ao vivo fica fora devido a migração, arquivos auxiliares e concorrência. Uma etapa posterior de histórico amplo precisa validar snapshot e catálogo.
- B — observar v2 via WebSocket loopback, com limite de tamanho e janela. Log temporal de projeção dos campos recebidos necessários ao teste, sem nomes, caminhos ou IDs de pessoas. Testar lacunas, recuo de relógio e tela de resultados. Sem amostras reais do provedor, manter capacidades experimentais.
- Checkpoint, pareamento offline/online, scoring por versão, classificação por objeto, recursos do jogo e custo durante gameplay precisam de gates próprios. A API autenticada fica bloqueada até existir uma solução verificável sem segredo distribuído.
