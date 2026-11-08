# AnonimizadorDotNet — Escopo

## Propósito

Biblioteca .NET que **detecta e substitui dados pessoais** em dados estruturados e semiestruturados,
**localmente e sem nenhuma chamada de rede**, gerando substitutos:

- **válidos no formato** (um CPF fictício passa na validação de dígito verificador);
- **determinísticos** (o mesmo CPF real resolve sempre para o mesmo CPF fictício — entre tabelas,
  consultas e execuções);
- **sem colisão** nos identificadores pessoais usados para relacionar registros (dois CPFs reais distintos
  nunca viram o mesmo CPF fictício);
- **restauráveis**, quando o uso pedir: anonimizar antes do LLM e devolver os dados originais na resposta.

O resultado preserva a lógica e os relacionamentos dos dados, permitindo usá-los como base para LLMs e
para processamento em massa sem expor dados pessoais.

## Caso de uso motivador

Consultar a estrutura e amostras de dados de um serviço por meio de um **MCP SQL somente leitura**, sem
que nenhum dado pessoal chegue ao LLM:

```
LLM  ⇄  MCP (proxy)  ⇄  AnonimizadorDotNet  ⇄  MCP SQL ReadOnly  ⇄  Banco
```

- Metadados (tabelas, colunas, tipos, chaves) passam sem alteração.
- Linhas retornadas são anonimizadas coluna a coluna antes de sair do ambiente local.
- Se a tabela `Clientes` e a tabela `Pedidos` trazem o mesmo CPF, ambas trazem o **mesmo CPF fictício**,
  e o LLM consegue relacionar os registros corretamente.

A biblioteca é o núcleo; o proxy MCP é um consumidor (pacote separado — ver backlog).

## Fases

A **v1 é um anonimizador simples, utilizável já** nos casos mais comuns. As fases seguintes completam o
produto para usos mais amplos e para a publicação externa. O que não está na v1 está em
[backlog.md](backlog.md).

### v1 — o que entra

| Área | Conteúdo |
|---|---|
| Tipos | CPF, CNPJ (numérico e alfanumérico), telefone, e-mail, nome (em coluna) |
| Transformação | FF1 para CPF/CNPJ/telefone; HMAC + dicionário para nome e e-mail |
| Modos | **Direto** (só anonimiza, nada é guardado) e **com restauração** (sessão) |
| Entrada | Valor individual e JSON (`System.Text.Json`, em memória) |
| Classificação | Configuração explícita + heurística por nome de campo + varredura de texto livre por padrão (CPF, CNPJ, e-mail, telefone) |
| Política para campos não classificados | `Passar` ou `Bloquear` |
| Chave secreta | Valor direto ou variável de ambiente |
| Qualidade | Cuidados de pacote público desde o início (ver seção própria) |

### Andamento da v1

| Etapa | Estado |
|---|---|
| Infraestrutura (solução, analisadores, CPM, testes, CI no GitHub Actions) | Concluída |
| 1. FF1 sobre AES (`Cryptography/Primitives`, `Cryptography/Fpe`) | Concluída: vetores oficiais do NIST, teste diferencial contra o BouncyCastle (só nos testes), bijeção exaustiva e testes de propriedade |
| 2. Subchaves por tipo, seleção por HMAC e *cycle-walking* | Próxima |
| 3–9. Documentos, transformação, detecção, classificação, JSON, sessão e API pública | Não iniciadas |

### Depois da v1

- **v1.x:** CEP, data de nascimento, cartão, endereço, identificador genérico; CSV e `IDataReader`;
  streaming; amostragem de conteúdo na classificação; relatório de auditoria; outros provedores de chave;
  persistência cifrada da sessão de restauração.
- **v2:** nomes em texto livre (NER), placeholders semânticos para LLM, coerência entre campos.

## Decisões tomadas

| Tema | Decisão |
|---|---|
| Plataforma | .NET 8+ (suporte a .NET Standard 2.0 vai para o backlog) |
| Foco da v1 | Anonimizador simples, determinístico, para dados estruturados |
| Restauração | Na v1, para dados estruturados e valores com formato reconhecível |
| Nomes em texto livre (NER) | v2 |
| Distribuição | Repositório público no GitHub desde o início; NuGet público após as fases de teste. Cuidados de pacote público desde o início |
| Licença | Apache 2.0 (inclui concessão de patentes dos contribuidores) |
| Idioma | Híbrido: API (tipos, métodos, namespaces) em inglês; termos brasileiros sem tradução (`Cpf`, `Cnpj`, `Cep`); documentação XML, README, mensagens de erro e docs do projeto em português |

## Princípios

1. **Local e offline.** A biblioteca nunca abre conexões de rede. É um princípio, não uma limitação.
2. **Determinismo.** Mesma chave secreta + mesmo valor ⇒ mesmo substituto, sempre.
3. **Bijeção em identificadores pessoais.** Zero colisões para CPF, CNPJ e afins.
4. **Formato preservado.** A saída mantém a representação da entrada (com ou sem máscara, texto ou número)
   e é válida segundo as regras do documento.
5. **Na dúvida, transforma.** Existe uma política em que o que não foi classificado como seguro não sai
   em claro.
6. **Valores originais nunca aparecem em logs, exceções ou relatórios.**

## Como o determinismo funciona

### Chaves internas do sistema

IDs substitutos — `IdUsuario`, chaves primárias e estrangeiras inteiras, GUIDs, sequências — **não são
transformados**. Eles não identificam uma pessoa fora do banco e são o que garante o funcionamento interno
e os joins. A heurística por nome de campo não os classifica como sensíveis, e a estratégia
"identificador genérico" (v1.x) só valerá para campos configurados explicitamente (ex.: matrícula, RG).

### Normalização

Antes de transformar, o valor é levado a uma forma canônica (`123.456.789-09`, `12345678909` e o inteiro
`12345678909` são o mesmo CPF; zeros à esquerda perdidos em campos numéricos são recompostos). Depois da
transformação, a **representação original é reaplicada**.

### Identificadores pessoais: cifra de preservação de formato (FPE)

Aqui "identificador" significa um dado pessoal que também é usado para relacionar registros (CPF, CNPJ),
não as chaves internas do sistema.

Um hash truncado não serve: o espaço de CPFs tem 10⁹ bases; com 1 milhão de CPFs distintos, a
expectativa é de **~500 colisões** (n²/2N), o que fundiria pessoas diferentes num mesmo registro.

A biblioteca usa **FF1 (NIST SP 800-38G) sobre AES**, com chave secreta. FF1 é uma permutação do domínio,
então é determinística, não tem colisões e é reversível com a chave.

| Documento | Parte cifrada | Parte preservada / recalculada |
|---|---|---|
| CPF | 9 dígitos da base | dígitos verificadores recalculados |
| CNPJ numérico | raiz (8 dígitos) | ordem (`0001` = matriz) preservada; DVs recalculados |
| CNPJ alfanumérico (vigente desde jul/2026) | raiz (8 posições, base 36) | ordem preservada; DVs recalculados (valor do caractere = ASCII − 48) |
| Telefone | 8 dígitos finais | DDD e o 9 inicial de celular preservados |

Notas:

- Cifrar a raiz do CNPJ separadamente faz com que matriz e filiais da mesma empresa continuem
  relacionadas.
- A ordem do CNPJ (4 dígitos) fica abaixo do domínio mínimo recomendado para FF1 (10⁶), por isso é
  preservada em vez de cifrada.
- CNPJ numérico gera CNPJ numérico (não quebra campos `bigint`/`numeric`). Para CNPJ alfanumérico,
  aplica-se *cycle-walking* para garantir que a saída contenha ao menos uma letra e não colida com o
  domínio numérico.
- Um CPF fictício válido pode coincidir com o CPF de alguma pessoa real. Isso é inerente a gerar documentos
  válidos e deve constar na documentação.

### Atributos que não relacionam registros

Nome e e-mail usam seleção determinística derivada de **HMAC-SHA256** (chave secreta + valor normalizado)
sobre dicionários embutidos. Colisões aqui são toleráveis.

- **Nome:** prenome + sobrenomes escolhidos do dicionário pelo HMAC.
- **E-mail:** parte local substituída; domínio de provedor público (gmail.com, outlook.com…) preservado;
  domínio corporativo substituído deterministicamente.

### Chave secreta

- Na v1, fornecida como valor direto ou por variável de ambiente.
- Trocar a chave muda todos os substitutos (rotação = nova "versão" da base anonimizada).
- Quem tem a chave consegue reverter a FPE. Portanto o resultado é **pseudonimização** (LGPD, art. 13),
  não anonimização (art. 12). A documentação deve dizer isso explicitamente.

## Modos de uso

### Direto

Anonimiza e não guarda nada. É o modo para gerar bases, alimentar o proxy MCP ou processar em massa.

### Com restauração (sessão)

Para o fluxo **anonimizar → LLM → restaurar**:

```
dados originais ──► Sessao.Anonimizar ──► dados seguros ──► LLM
                          │                                  │
                  [registro da sessão]                    resposta
                          │                                  │
                          └─────────► Sessao.Restaurar ◄─────┘
                                            │
                              resposta com os dados originais
```

- **Registro da sessão:** mapa `substituto → original` em memória, mantido apenas enquanto a sessão existir.
  Na v1 não é persistido.
- **Só se restaura o que a sessão emitiu.** Se o LLM inventar um CPF válido, decifrá-lo devolveria um CPF
  real sem relação com os dados; por isso valores fora do registro permanecem como estão.
- **O que se restaura:** JSON (campos e textos), valores individuais e texto livre da resposta do LLM. No
  texto, CPF, CNPJ, telefone e e-mail são encontrados pelos detectores de padrão (tolerando mudança de
  máscara) e nomes por correspondência exata com os nomes emitidos.

## Classificação de campos

Para cada campo, a primeira regra que se aplicar vence:

1. **Configuração explícita:** `caminho → tipo + estratégia` ou `caminho → ignorar`.
2. **Heurística por nome de campo:** `cpf`, `nr_cpf`, `cnpj`, `email`, `telefone`, `celular`, `nome`, …
3. **Texto livre não classificado:** varredura com detectores por padrão (CPF, CNPJ, e-mail, telefone)
   dentro do texto, como em campos `observacao`.
4. **Política padrão para o restante:** `Passar` ou `Bloquear` (recomendado para o caso MCP).

Detectores e transformadores são **extensíveis**: o consumidor registra tipos próprios.

## Cuidados de pacote público (desde já)

- API pública mínima: tudo `internal` por padrão; `PublicApiAnalyzers` para controlar mudanças.
- SemVer; `nullable` habilitado; `TreatWarningsAsErrors`; documentação XML em toda a API pública.
- Build determinístico e SourceLink.
- **Sem dependências de runtime além da BCL** (FF1 implementado sobre `System.Security.Cryptography.Aes`).
- Testes: vetores oficiais do NIST para FF1; testes de propriedade (bijeção, determinismo entre execuções,
  validade de DV, preservação de formato, restauração = identidade); benchmarks com BenchmarkDotNet.
- Aviso na documentação: a biblioteca reduz risco, mas não substitui avaliação jurídica sobre LGPD.

## Decisões em aberto

- **Nome do pacote NuGet:** preferência por manter `AnonimizadorDotNet`; verificar disponibilidade.
- **Política padrão para campos não classificados:** `Passar` ou `Bloquear`.
