# Provas técnicas da Fase 2

## A — Leitura de arquivo real

Após o build Release, na raiz do projeto:

```powershell
dotnet run --no-build -c Release --project spikes/SourceSpikes -- history
dotnet run --no-build -c Release --project spikes/SourceSpikes -- history "D:\osu-data"
```

Sem argumento, a prova usa `%APPDATA%\osu`. Para uma instalação personalizada, copie o caminho confirmado em **Configurações → Abrir pasta do osu!**. Use um caminho com o subdiretório `files`. O segundo comando usa um caminho ilustrativo. Execute fora de uma partida para evitar concorrência de I/O.

A prova procura conteúdo no padrão `files/<1>/<2>/<sha256>` e verifica SHA-256 antes de aceitar um candidato. A dificuldade fornece hash de conteúdo, ruleset e IDs declarados quando presentes. O vínculo exige que o MD5 do cabeçalho do replay aponte para uma única versão standard encontrada na varredura. O formato aceito nesta prova é o contêiner estendido de lazer standard com versão entre 30000001 e 30000019. Inspeção de contêiner valida cabeçalho e limites de blocos, sem descompactar frames.

A saída é agregada. Código 0 indica ao menos um vínculo, 1 indica nenhum vínculo no conjunto examinado e 2 indica erro na fonte ou na execução. A execução examina até 10.000 blobs no layout esperado, com limite de 16 MiB por blob candidato. Formato não reconhecido, replay fora da faixa e mapa ausente reduzem a cobertura. Um ID declarado positivo continua sendo uma alegação do arquivo. A prova não abre Realm e não cobre restauração de histórico nem classificação de resultados.

## B — WebSocket tosu opcional

Use tosu já disponível e configurado para escutar em loopback. Registre você mesmo as versões do tosu e do lazer para poder confrontá-las com a [matriz](FONTES_E_LIMITACOES.md). Após o build Release, na raiz do projeto:

```powershell
dotnet run --no-build -c Release --project spikes/SourceSpikes -- tosu 24050 90
```

A captura conecta em `ws://127.0.0.1:24050/websocket/v2` por até 90 s neste exemplo. Em uma sessão de teste, passe pelo menu, inicie uma tentativa standard, abra resultados e repita com fail, retry e saída para o menu. Teste também uma reprodução observada e uma queda de conexão durante playing. Anote a ação e o horário para comparação com o JSONL. O app só lê o endpoint. Encerre com Ctrl+C ou aguarde a janela.

O log local `scratch/tosu-*.jsonl` guarda apenas estado numérico, família de cliente reconhecida, ruleset, relógio do mapa e flag de fail, com sequência, timestamp UTC e evento derivado. Mensagens inválidas, perda da fonte e saída de playing fecham uma janela aberta com `unknown`. Tela de resultados informa tela observada, e o relógio voltando informa recuo. A flag explícita é uma observação isolada. Verifique que partida assistida, retry, saída e queda não viram falsos fails/aborts/passes. O spike encerra após 1.000 mensagens ou ao atingir o prazo, aceita até 1 MiB por mensagem e registra o motivo de encerramento. Código 1 e `provider_unavailable` indicam que não houve captura naquele endereço.

O teste automatizado da wiki usa o recorte numérico de uma amostra upstream de menu de 2024. As demais sequências e o servidor WebSocket de teste são sintéticos. Para confirmar coleta em jogo, ainda falta executar a sequência manual com uma versão funcional do tosu. A API oficial e replay por objeto têm validação própria em fases posteriores.
