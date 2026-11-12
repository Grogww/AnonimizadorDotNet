# AnonimizadorDotNet — Arquitetura

Decisões de estrutura da solução e da biblioteca. O *quê* e o *porquê* do produto estão em
[escopo.md](escopo.md); aqui fica o *como* está organizado.

## Pacotes

**Um único pacote NuGet na v1: `AnonimizadorDotNet`.**

- `System.Text.Json` faz parte da plataforma no .NET 8, então o suporte a JSON fica no núcleo sem
  acrescentar dependência.
- Um pacote novo só se justifica quando traz uma **dependência que o núcleo não pode ter**. Candidatos
  previstos no backlog:

| Pacote futuro | Motivo da separação |
|---|---|
| `AnonimizadorDotNet.Mcp` | Depende do SDK do MCP |
| `AnonimizadorDotNet.Ner` (v2) | Depende de runtime ONNX |
| `AnonimizadorDotNet.EntityFrameworkCore` | Depende do EF Core |

- Pacote separado de abstrações (`.Abstractions`) não está previsto: só faria sentido se terceiros
  precisassem estender a lib sem depender do núcleo.

## Plataforma

- A lib tem um único alvo, `net8.0`, e roda em .NET 8, 10 e 11.
- Multi-target (`net8.0;net10.0`) fica fora da v1. Os testes poderão rodar em mais de um runtime, o que
  dá cobertura sem complicar a lib.

## Projetos da solução

```
AnonimizadorDotNet/
├─ src/
│  └─ AnonimizadorDotNet/              ← a lib (único projeto publicado)
├─ tests/
│  └─ AnonimizadorDotNet.Tests/        ← unitários, vetores NIST, propriedades
├─ benchmarks/
│  └─ AnonimizadorDotNet.Benchmarks/   ← BenchmarkDotNet (console, não publicado)
├─ docs/
├─ Directory.Build.props               ← configurações comuns a todos os projetos
├─ Directory.Packages.props            ← versões centralizadas de pacotes
├─ .editorconfig
├─ global.json                         ← escolhe a plataforma de testes
└─ AnonimizadorDotNet.slnx
```

**Pastas físicas, sem pastas virtuais.** No disco, os projetos ficam em `src/`, `tests/` e `benchmarks/`,
porque `src/Directory.Build.props` aplica as configurações de pacote só ao que está em `src/`. Na solução
(`.slnx`), os projetos ficam direto na raiz, sem *solution folders*: com três projetos, as pastas virtuais
só acrescentariam um nível vazio na árvore do Visual Studio.

| Projeto | Papel | Publicado |
|---|---|---|
| `src/AnonimizadorDotNet` | A biblioteca | Sim |
| `tests/AnonimizadorDotNet.Tests` | Testes de unidade, vetores oficiais do FF1 e testes de propriedade; acessa partes internas via `InternalsVisibleTo`; roda em `net8.0` e `net10.0` | Não |
| `benchmarks/AnonimizadorDotNet.Benchmarks` | Medir o custo de FF1 e da varredura de JSON | Não |

Um projeto de exemplos (`samples/`), consumindo apenas a API pública, fica para depois da v1.

### Arquivos da raiz

- **`Directory.Build.props`**: herdado por todos os projetos: `nullable`, warnings como erro, versão da
  linguagem, build determinístico.
- **`src/Directory.Build.props`**: só para projetos publicados: `PublicApiAnalyzers`, documentação XML,
  metadados do pacote, símbolos para SourceLink.
- **`Directory.Packages.props`**: *Central Package Management*; nenhum `.csproj` declara versão de pacote.
- **`.editorconfig`**: estilo de código e severidade de analisadores.
- **`global.json`**: hoje só ativa a *Microsoft.Testing.Platform* no `dotnet test`, exigida pelo xUnit v3
  a partir do SDK 10. Não fixa a versão do SDK: a máquina de desenvolvimento só tem o SDK 11 RC; antes de
  publicar no NuGet, fixar ali um SDK estável.

## Testes

| Escolha | Motivo |
|---|---|
| **xUnit v3** | Padrão de fato em libs open source .NET; cada teste roda numa instância nova da classe; paralelismo ligado por padrão |
| **CsCheck** (linha 4.x) | Testes de propriedade em C# puro, com redução automática do caso que falha. A 5.x exige net10+, e os testes precisam rodar em `net8.0`, alvo da lib |
| **Microsoft.Testing.Platform** | Executor de testes atual da Microsoft; dispensa `Microsoft.NET.Test.Sdk` e `xunit.runner.visualstudio` |

No projeto de testes, `CA2007` e `CA1707` ficam desligadas: o xUnit pede que testes não usem
`ConfigureAwait(false)`, e os nomes de teste costumam usar sublinhado.

## Organização interna da biblioteca

Um único projeto, com camadas separadas por pasta/namespace. **A dependência só vai de cima para baixo**:
camadas de baixo não conhecem as de cima.

| Área | Responsabilidade | Visibilidade |
|---|---|---|
| Raiz | `AnonymizerBuilder`, `Anonymizer` (valor individual e reader), `UnclassifiedPolicy`, `PersonalDataTypes`; sessão na v1.x | **Pública** |
| `Data` | Envolve o `DbDataReader`, aplica a decisão de cada coluna aos valores | `internal` |
| `Json` (v1.x) | Percorre o documento, aplica a classificação por caminho e monta a saída | `internal` |
| `Sessions` (v1.x) | Registro `substituto → original` e restauração | Sessão pública, registro interno |
| `Classification` | Regras em ordem de precedência (explícita → nome → texto livre → política) | Configuração pública, motor interno |
| `Detection` | Detectores por padrão para varredura de texto livre | `internal` + interface pública de extensão |
| `Transformation` | Liga documento e criptografia: gera o substituto de um valor | `internal` + interface pública de extensão |
| `Documents` | Por tipo (CPF, CNPJ, telefone, e-mail, nome): normalizar, validar, calcular DV, reaplicar formato | `internal` |
| `Cryptography` | FF1 sobre AES, seleção por HMAC, derivação de subchaves | `internal` |
| `Resources` | Dicionários de nomes e domínios como recursos embutidos | — |

Consequências:

- Novos formatos de entrada da v1.x (JSON, CSV) entram como mais um "percorredor" ao lado de `Data`, sem
  alterar as camadas de baixo.
- **Subchave por tipo:** a chave mestra é derivada em uma subchave por tipo de documento, para que o mesmo
  número cifrado como CPF e como telefone use permutações independentes.

### `Documents`

Uma subpasta por tipo, cada uma com um `XxxFormat` estático (`TryParse` e `Format`) e um `ParsedXxx` com o
valor normalizado. `TryParse` nunca lança exceção por causa do conteúdo e recusa valores proibidos; `Format`
recalcula os verificadores e reaplica a representação original. Nenhum `ParsedXxx` sobrescreve `ToString()`.

| Subpasta | Conteúdo |
|---|---|
| `Formatting` | `TextLayout` (máscara, texto ou número, zeros à esquerda; guarda só os separadores), `Numerals` (caracteres ↔ numerais em base 10 e 36), `LetterCasing`, `TextNormalization` (chave sem acentos e em maiúsculas) |
| `CheckDigits` | `Modulo11`, comum a CPF e CNPJ |
| `Cpf` | Base de 9 numerais; proíbe dígitos todos iguais |
| `Cnpj` | Raiz de 8 posições em base 10 ou 36, ordem preservada, verificadores com valor ASCII − 48 |
| `Phone` | DDD validado; 7 dígitos finais cifrados, o resto preservado |
| `Name` | Partes (prenome, sobrenome, inicial, partícula, sufixo), caixa, acentos e sexo do prenome |
| `Email` | Parte local, domínio e sufixo; provedores públicos preservados; estilo da parte local |

A cifra e a seleção por HMAC não entram aqui: a camada `Transformation` junta as duas pontas.

### `Transformation`

Um transformador por tipo, na subpasta do tipo (`Cpf`, `Cnpj`, `Phone`, `Name`, `Email`): `TryParse` do
documento → cifra ou seleção → `Format`. Todos implementam a interface pública `IValueTransformer`
(`Type` + `TryTransform(valor, ValueKind, out substituto)`), que também serve para tipos próprios do
consumidor. `TryTransform` devolve falso, sem lançar exceção, quando o valor não é do tipo; o que fazer
nesse caso é decisão das camadas de cima (classificação e política).

| Componente | Papel |
|---|---|
| `TransformerSet` | Recebe a chave mestra, cria os transformadores embutidos e registra os próprios por nome de tipo (sem diferenciar caixa). A chave mestra é zerada ao fim da construção; ficam só as subchaves |
| `HmacContexts` | Contextos de cada sorteio por HMAC (prenome, sobrenome por posição, dígitos, domínio). Fixados por valores dourados, como os rótulos de `KeyLabels` |
| `Phone/PhoneTweak` | Tweak do telefone: `DDD/início do assinante` em ASCII |
| `Cryptography/Keys/KeyRing`, `SecretKey` | Guarda a chave mestra e as subchaves de HMAC em arrays fixados na memória, zerados no descarte |
| `Cryptography/Fpe/Ff1Pool` | Instâncias de `Ff1` reutilizadas entre threads: cada operação pega uma livre (ou cria), usa e devolve |

Chaves da seleção: CPF, CNPJ (só a raiz, tweak vazio) e telefone usam FF1, que é bijetor; nome usa o nome
completo normalizado; e-mail usa a parte local inteira (sem a etiqueta) e, para o domínio corporativo, o
domínio inteiro.

Concorrência: todos os transformadores embutidos são seguros para várias threads (o FF1 via pool; o HMAC
não tem estado). O contrato foi escolhido na etapa 4 e será revisto com o uso real (sessão, lotes).

### `Classification`

Decide, só a partir do esquema e uma vez por resultado, o que fazer com cada coluna: transformar, varrer,
passar ou bloquear. Nenhum valor de linha é lido aqui; aplicar a decisão é papel do reader.

| Componente | Papel |
|---|---|
| `Columns/ColumnInfo` | Metadados de uma coluna (nome no resultado, origem, tipo CLR, identity, tamanho); nunca valores |
| `Columns/ColumnStorage` | Texto, inteiro (`ValueKind.Number`) ou outro, a partir do tipo CLR |
| `Columns/ColumnNameTokens` | Quebra nomes em partes: `snake_case`, `camelCase`, siglas, dígitos, sem acentos |
| `Rules/ColumnTarget`, `ColumnRule`, `ColumnRuleSet` | Regras explícitas por `schema.tabela.coluna`, `tabela.coluna` ou `coluna`; a mais específica vence |
| `Rules/InternalKeyGuard` | Identity, GUID e nomes de ID nunca são classificados pela heurística |
| `Rules/ColumnNameHeuristic` | Tipo pelo nome da coluna, exigindo armazenamento compatível |
| `ColumnClassifier` | Aplica a precedência e devolve um `ColumnDecision` por posição |

Precedência: regra explícita → chave interna → nome (origem, depois alias) → varredura de texto → política
(`Pass`/`Block`, interna; vira `UnclassifiedPolicy` na API pública).

Origem e alias: a origem informada pelo provedor identifica a coluna, para resistir a aliases. O alias só
pode acrescentar proteção (uma regra de transformação ou a heurística), nunca retirá-la: `Ignorar` e
`Varrer` por alias não se aplicam quando há origem, e a proteção de chaves olha a origem, para que
`telefone AS id` continue anonimizado.

Uma decisão não guarda valores nem nomes de coluna, e o classificador pode ser usado por várias threads.

### `Data`

Aplica aos valores a decisão que `Classification` tomou a partir do esquema. É a fronteira por onde o dado
sai, e por isso é **fechada por padrão**: todo membro de `DbDataReader` que lê valor é sobrescrito, e um
teste de reflexão falha se o .NET acrescentar um novo.

| Componente | Papel |
|---|---|
| `ColumnSchema` | `GetColumnSchema()` → `ColumnInfo[]`; sem esquema do provedor, só nome e tipo CLR (a classificação ainda vale pelo nome) |
| `ValueCodec` | Valor CLR ↔ texto para os transformadores: texto como `ValueKind.Text`, inteiros e `decimal` sem casas como `ValueKind.Number`; volta pelo tipo original, com verificação de faixa |
| `ITextScanner` | Varredura de texto livre; até a etapa 7, `PassThroughScanner` devolve o texto intacto |
| `ReaderContext` | Classificador e varredura compartilhados pelos readers de um anonimizador; encerrado no descarte dele |
| `AnonymizingDataReader` | Classifica a cada resultado e calcula cada valor uma vez por linha |

Por ação: `Pass` vai direto ao reader original (inclusive os getters tipados); `Block` devolve `DBNull` sem
ler o original; `Transform` usa o primeiro transformador que aceitar o valor; `Scan` passa pelo
`ITextScanner`. Metadados e tipos CLR passam intactos. Um valor que nenhum transformador aceita, ou cujo
substituto não cabe no tipo da coluna, passa como está (decisão provisória, num único ponto:
`AnonymizingDataReader.Resolve`).

### API pública (raiz)

```csharp
using var anonymizer = new AnonymizerBuilder()
    .UseKeyFromEnvironment("ANONIMIZADOR_CHAVE")       // Base64 de ao menos 32 bytes; ou UseKey(bytes)
    .TransformColumn("dbo.Clientes.doc", PersonalDataTypes.Cpf)
    .IgnoreColumn("observacao_interna")
    .ScanColumn("Pedidos.comentario")
    .WithUnclassifiedPolicy(UnclassifiedPolicy.Pass)
    .AddTransformer(meuTransformador)                  // IValueTransformer próprio
    .Build();

anonymizer.TryAnonymize("123.456.789-09", PersonalDataTypes.Cpf, out string? cpf);
using DbDataReader reader = anonymizer.Wrap(command.ExecuteReader());
```

| Tipo | Contrato |
|---|---|
| `AnonymizerBuilder` | Uso único: `Build` zera a cópia da chave (em memória fixada) e encerra o builder, mesmo se falhar. Erros de coluna e de tipo próprio aparecem na hora (`ArgumentException`); os que dependem do conjunto (chave, tipo pedido por regra) no `Build` (`InvalidOperationException`) |
| `Anonymizer` | Seguro para várias threads; `Dispose` zera as subchaves e faz os readers criados por ele lançarem `ObjectDisposedException` |
| `Wrap` | Devolve um `DbDataReader` (o tipo concreto é interno) dono do reader original |
| `UnclassifiedPolicy` | `Pass` (padrão) ou `Block`, mapeada para o `UnclassifiedBehavior` interno |
| `PersonalDataTypes` | Nomes dos tipos embutidos: `cpf`, `cnpj`, `phone`, `email`, `name` |

Mensagens de exceção e `ToString()` nunca mostram valores nem a chave. Exceções do próprio provedor não
passam pela biblioteca.
