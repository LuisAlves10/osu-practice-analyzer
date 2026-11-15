# Analytics

## Contrato de contabilização

| Resultado | Evidência exigida | Estatísticas principais |
| --- | --- | --- |
| PASS | Conclusão bem-sucedida confirmada pela fonte | Incluído |
| FAIL | Falha durante a execução confirmada pela fonte | Incluído |
| RESTART | Reinício voluntário confirmado pela fonte | Consulta complementar |
| QUIT | Abandono voluntário confirmado pela fonte | Consulta complementar |
| UNKNOWN | Evidência ausente, ambígua ou conflitante | Consulta complementar |

Replay, tela de resultado, recuo de relógio e perda da fonte são evidências insuficientes para classificar sozinhos. A classificação v1 é derivada das evidências preservadas. Um único resultado explícito converge para aquele resultado. Resultados explícitos conflitantes retornam UNKNOWN. O contrato exige que o adaptador verifique a semântica da sua fonte antes de registrar um fato.

A visão SQLite `performance_attempt` é a base comum para contagem, taxa de conclusão, accuracy, consistência, tendências, combo e misses. Contagem = PASS + FAIL. Taxa de conclusão = PASS / (PASS + FAIL), indisponível sem amostra. Cada métrica usa apenas tentativas elegíveis com os campos exigidos, e expõe o tamanho de amostra e o total elegível. Accuracy média usa as tentativas elegíveis com um valor inequívoco, na escala 0–1, e retorna nulo se nenhuma tiver esse dado. As demais métricas mecânicas e séries serão implementadas nos gates pertinentes.

As tentativas RESTART, QUIT e UNKNOWN continuam consultáveis para análise complementar, inclusive prática parcial e padrões de interrupção. A futura interface vai apresentar as duas populações em áreas distintas. Cada métrica mecânica terá fórmula, unidade, versão, entradas e requisitos de comparabilidade. Um score isolado é insuficiente para diagnosticar aim, leitura, tapping ou fadiga.
