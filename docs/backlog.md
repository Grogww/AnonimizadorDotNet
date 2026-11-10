# Backlog — melhorias futuras

Itens fora do escopo da v1. Cada item deve virar uma issue quando o repositório for publicado.

## v1.x — completar o uso estruturado

### Tipos

- CEP (prefixo regional preservado + HMAC no sufixo).
- Data de nascimento (generalização por ano/faixa ou deslocamento determinístico, configurável por campo).
- Cartão de crédito (FF1 com BIN preservado + Luhn recalculado).
- Endereço em campo (logradouro fictício via HMAC + dicionário).
- **Identificador genérico** (RG, matrícula, contrato, conta bancária): FF1 conforme máscara configurada;
  só se aplica a campos configurados explicitamente.

### Entrada e processamento

- CSV.
- `IDataReader` e abstração de linha/coluna (caso SQL direto, sem passar por JSON).
- Streaming: `Stream`, `Utf8JsonReader` e `IAsyncEnumerable<T>`, sem carregar tudo em memória;
  paralelismo configurável.

### Classificação e operação

- Amostragem de conteúdo na classificação (ex.: maioria das amostras é CPF com DV válido).
- Relatório de auditoria por execução (campos classificados, regra aplicada, contagem por tipo), sem
  valores originais.
- Provedores de chave adicionais: arquivo, user-secrets, provedor plugável.
- Chave mestra a partir de senha digitada (baixa entropia): derivar com PBKDF2 ou Argon2 e salt
  armazenado. Na v1 a chave mestra precisa ter ao menos 32 bytes aleatórios, e o HKDF usa salt vazio.

### Restauração

- Persistência cifrada do registro da sessão (restaurar em outro processo ou mais tarde).

## v2

- **Nomes e endereços em texto livre.** Dicionário de nomes brasileiros com heurísticas e/ou modelo NER
  local (ONNX) como pacote opcional, sem dependência no núcleo.
- **Placeholders semânticos para LLM** (`[PESSOA_1]`), com restauração tolerante a alterações do LLM no
  placeholder.
- **Coerência entre campos da mesma linha.** Gênero do nome ↔ e-mail; CEP ↔ cidade/UF; idade ↔ data de
  nascimento.

## Plataforma

- **Suporte a .NET legado (.NET Standard 2.0 / .NET Framework 4.6.2+).** Avaliar multi-target e o custo de
  abrir mão de `Span<T>`, regex gerado em compilação e AOT.

## Tipos dedicados adicionais

- CNH, PIS/NIS, título de eleitor, placa (antiga e Mercosul), chave PIX, IP.

## Formatos e integrações

- Mapeamento de objetos C# por atributos (`[Sensivel(Tipo.Cpf)]`) e configuração fluente.
- XML, Parquet, `DataTable`.
- Integração com EF Core.
- **Pacote proxy MCP:** servidor MCP que envolve um MCP SQL somente leitura e anonimiza os resultados.

## Privacidade

- **Modo irreversível.** Chave aleatória descartada ao fim da execução, para cenários que exigem
  anonimização (LGPD, art. 12). Mantém consistência dentro da execução, não entre execuções.
- k-anonimato / l-diversidade para dados tabulares (tratamento de quase-identificadores como CEP + data de
  nascimento + sexo).

## Fora de escopo por enquanto

- OCR, imagens, PDF e áudio.
