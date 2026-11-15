# Persistência e importação

O CLI funciona localmente com o SDK .NET 10. Execute na raiz do projeto. Use um UUID próprio para cada perfil e reutilize o mesmo UUID ao reimportar. O UUID é uma seleção explícita do usuário. O importador não deduz a conta proprietária dos replays.

```powershell
dotnet build src/HistoryCli -c Release
$profile = [guid]::NewGuid()
dotnet run --no-build -c Release --project src/HistoryCli -- import $profile (Join-Path $env:APPDATA 'osu')
dotnet run --no-build -c Release --project src/HistoryCli -- summary $profile
```

Passe o diretório de dados confirmado em configurações se o lazer usa um caminho personalizado. Execute fora de uma partida. O banco local fica em `scratch/history.sqlite` e contém bytes reais de mapas e replays. Preserve esse arquivo para manter o histórico. O CLI aceita Ctrl+C durante a importação para cancelar o lote.

## Integridade

- O adaptador lê somente candidatos do depósito SHA-256. Os formatos são os do [spike](PROVAS_TECNICAS.md). Realm, mídias e outros arquivos ficam fora da ingestão.
- O banco versiona duas migrações. V1 cria entidades e chaves compostas por perfil. V2 cria índices e projeções derivadas. A abertura aplica migrações pendentes numa transação e rejeita versões futuras.
- A importação relê o depósito e grava incrementalmente por perfil e origem. Bytes já conhecidos e as identidades normalizadas reaproveitam os registros. Cada importação é atômica. Uma nova leitura pode trazer arquivos adicionados durante ou após a anterior. A varredura continua limitada conforme o spike.
- Para a origem histórica, um contêiner distinto origina um registro de tentativa recuperada com resultado UNKNOWN. O SHA-256 é a identidade da origem de **arquivo**. Observações de outras fontes usam chaves próprias e ficam distintas mesmo se o payload for igual. O histórico por arquivo tem a granularidade do que foi recuperado.
- O vínculo de replay exige um único candidato MD5 no perfil. O SHA-256 identifica a versão do mapa e verifica a integridade do conteúdo lido. Vínculos ausentes ou ambíguos continuam visíveis e são reavaliados conforme chegam versões adicionais. IDs declarados pelo mapa ficam como metadados.
- Tentativa, score salvo, replay e observação RAW têm chaves separadas. O importador de replay preserva a evidência em bytes e não cria um score salvo. A API aceita um score sem tentativa verificada e permite vincular nova evidência a uma tentativa existente quando o adaptador fornecer um ID de vínculo verificado no mesmo perfil. Reutilizar a mesma chave de observação com dados divergentes rejeita a transação.
- O contrato opcional do tosu guarda uma projeção tipada por UUID de captura e sequência, inclusive amostras iguais com sequências distintas e amostras indisponíveis. O consumidor futuro pode retomar usando chaves já persistidas. O CLI ainda não liga a captura ao banco. Essas amostras, sozinhas, são insuficientes para classificar o resultado.
