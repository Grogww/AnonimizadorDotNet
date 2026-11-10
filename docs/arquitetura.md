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
| Raiz | `AnonymizerBuilder`, anonimizador direto, sessão, provedor de chave, `UnclassifiedPolicy` | **Pública** |
| `Json` | Percorre o documento, aplica a classificação por caminho e monta a saída | `internal` |
| `Sessions` | Registro `substituto → original` e restauração | Sessão pública, registro interno |
| `Classification` | Regras em ordem de precedência (explícita → nome → texto livre → política) | Configuração pública, motor interno |
| `Detection` | Detectores por padrão para varredura de texto livre | `internal` + interface pública de extensão |
| `Transformation` | Liga documento e criptografia: gera o substituto de um valor | `internal` + interface pública de extensão |
| `Documents` | Por tipo (CPF, CNPJ, telefone, e-mail, nome): normalizar, validar, calcular DV, reaplicar formato | `internal` |
| `Cryptography` | FF1 sobre AES, seleção por HMAC, derivação de subchaves | `internal` |
| `Resources` | Dicionários de nomes e domínios como recursos embutidos | — |

Consequências:

- Novos formatos de entrada da v1.x (CSV, `IDataReader`) entram como mais um "percorredor" ao lado de
  `Json`, sem alterar as camadas de baixo.
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

A cifra e a seleção por HMAC não entram aqui: a camada `Transformation` (etapa 4) junta as duas pontas.
