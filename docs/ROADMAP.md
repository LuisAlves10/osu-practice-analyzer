# Roadmap

| Fase | Entrega e aceite |
| --- | --- |
| 0 | Governança, licença do código próprio, Git auditado e publicação verificada ou bloqueio registrado. |
| 1 | Evidência primária versionada, matriz de fontes, ADR de stack e identidade das entidades. |
| 2 | Provas isoladas de leitura histórica e observação tosu, incluindo resultado desconhecido perante uma lacuna. Encerrar a primeira execução aqui. |
| 3 | SQLite, origens RAW e normalizadas, deduplicação e perfis. Preservar duas tentativas distintas da mesma dificuldade após reinício e demonstrar reimportação idempotente. |
| 4 | Biblioteca por mapa e comparação de duas tentativas reais. |
| 5 | Replay e timeline. Validar contagens contra referências e tornar divergências visíveis. |
| 6 | Diagnóstico global com amostra comparável em múltiplos mapas e teste negativo de causalidade. |
| 7 | Exercício com justificativa e avaliação de transferência em mapas comparáveis. |
| 8 | Perfil online opcional e exportações reproduzíveis com tempos, proveniência e consentimento. |
| 9 | Instalação e restauração testadas, operação offline, desempenho e distribuição Windows. |
| 10 | Avaliar outros modos, clientes e plataformas em decisão posterior à V1. |

Registre o resultado de cada gate em [STATUS.md](STATUS.md). Cada fase depende dos gates anteriores e das capacidades demonstradas.
