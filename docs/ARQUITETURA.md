# Arquitetura

Adaptadores de fonte → observações RAW → normalização → domínio. Replay, analytics e recomendações consomem contratos versionados. Sincronização online e exportações são bordas independentes. A UI consulta o domínio de forma assíncrona. A Fase 1 escolhe stack e caminhos de ingestão.
