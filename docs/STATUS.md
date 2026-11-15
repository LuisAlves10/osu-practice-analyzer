# Status

## 2026-11-15

- Fase 0 concluída. AGENTS, licença MIT para material original e documentação inicial publicados no [repositório público](https://github.com/LuisAlves10/osu-practice-analyzer), branch `main`, commit `6147d10`. `git diff --cached --check`, auditoria de arquivos/histórico e verificação de visibilidade passaram.
- Fase 1 concluída. Referência interna inspecionada via GitHub autenticado. [Matriz](FONTES_E_LIMITACOES.md), [arquitetura](ARQUITETURA.md), [ADR de stack](DECISOES.md) e [modelo inicial](MODELO_DE_DADOS.md) registrados. Lazer 2026.1005.0 encontrado em caminho padrão. HTTP loopback na rota correta do tosu v2 indisponível e sem listener na porta padrão durante a verificação.

**Próxima ação** — Fase 2. Implementar e executar os dois spikes isolados. Buscar um par mapa/replay no depósito local por SHA-256 e MD5. Testar a projeção de observações v2 e as regras conservadoras de transição usando o exemplo documentado, sequências sintéticas rotuladas e o transporte loopback.
