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

- **JSON** (`System.Text.Json`, em memória): percorrer `JsonNode`/`JsonDocument` montando o caminho de cada
  campo, aplicar a mesma classificação da v1 por caminho e manter o tipo JSON (string continua string,
  número continua número). Saiu da v1 quando o foco passou para `IDataReader`.
- CSV.
- Streaming: `Stream`, `Utf8JsonReader` e `IAsyncEnumerable<T>`, sem carregar tudo em memória;
  paralelismo configurável.
- Escrita assistida no banco (gerar os `UPDATE`s, ordem por constraints, CPF como chave primária): por
  enquanto fica no script do consumidor.

### Gravação no banco (pendências da etapa 6)

Descobertas ao planejar o `AnonymizingDataReader`; afetam o script que grava os substitutos com `UPDATE`.
Na v1 só são documentadas.

- **Prioridade alta — substituto maior que a coluna.** Nome e e-mail substitutos podem passar do
  `varchar(n)` (`ColumnSize` do esquema). Na v1 a lib não trunca nem bloqueia, e o `UPDATE` falha. Analisar
  com o uso real: respeitar o `ColumnSize` na seleção por HMAC (sortear só itens que cabem), truncar de
  forma determinística ou avisar antes de gravar. Qualquer opção que mude os substitutos exige decisão.
- **E-mail em coluna `UNIQUE`.** A seleção por HMAC admite colisões, e dois e-mails reais podem virar o
  mesmo substituto, o que derruba o `UPDATE`. Possível tratativa: sufixo derivado por FF1 na parte local.
  Muda todos os substitutos de e-mail, então só com decisão (e, se for depois da 1.0, nova versão).

### Classificação e operação

- Amostragem de conteúdo na classificação (ex.: maioria das amostras é CPF com DV válido).
- Relatório de auditoria por execução (campos classificados, regra aplicada, contagem por tipo), sem
  valores originais.
- Provedores de chave adicionais: arquivo, user-secrets, provedor plugável.
- Chave mestra a partir de senha digitada (baixa entropia): derivar com PBKDF2 ou Argon2 e salt
  armazenado. Na v1 a chave mestra precisa ter ao menos 32 bytes aleatórios, e o HKDF usa salt vazio.

### Restauração

- **Sessão com restauração** (saiu da v1: nem o MCP somente leitura nem o script de banco precisam dela).
  Registro `substituto → original` em memória, por tipo; restaurar valor individual, JSON e texto livre
  (detectores tolerando mudança de máscara; nomes por correspondência exata com os emitidos); só restaurar
  o que a sessão emitiu; contrato de concorrência definido e testado; testes de anonimizar → restaurar =
  identidade e de que um CPF válido inventado não é restaurado.
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
- **Pacote MCP:** servidor MCP que abre a conexão, só permite leitura e entrega as linhas anonimizadas
  pelo `AnonymizingDataReader` da v1. Começa como projeto próprio do usuário, consumindo o pacote; vira
  `AnonimizadorDotNet.Mcp` se fizer sentido publicar.

## Privacidade

- **Modo irreversível.** Chave aleatória descartada ao fim da execução, para cenários que exigem
  anonimização (LGPD, art. 12). Mantém consistência dentro da execução, não entre execuções.
- k-anonimato / l-diversidade para dados tabulares (tratamento de quase-identificadores como CEP + data de
  nascimento + sexo).

## Fora de escopo por enquanto

- OCR, imagens, PDF e áudio.
