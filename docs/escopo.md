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
LLM  ⇄  MCP SQL ReadOnly  ──(IDataReader)──►  AnonymizingDataReader  ──►  linhas anonimizadas
              │
            Banco
```

- O MCP abre a conexão e só permite leitura; a biblioteca não faz chamada de rede, só envolve o reader.
- Metadados (tabelas, colunas, tipos, chaves) passam sem alteração.
- Linhas retornadas são anonimizadas coluna a coluna antes de sair do ambiente local.
- Se a tabela `Clientes` e a tabela `Pedidos` trazem o mesmo CPF, ambas trazem o **mesmo CPF fictício**,
  e o LLM consegue relacionar os registros corretamente.
- O LLM não filtra por valores fictícios (`WHERE cpf = '<fictício>'`); navega pelos IDs internos, que
  passam intactos.

Segundo uso-alvo da v1: um **script que anonimiza um banco de teste**, lendo cada tabela pelo
`AnonymizingDataReader` e gravando os substitutos com `UPDATE`. Como os substitutos são determinísticos e
o FF1 é bijetor, as junções por CPF/CNPJ continuam funcionando no banco gravado.

A biblioteca é o núcleo; o MCP e o script são consumidores (o MCP pode virar pacote separado — ver backlog).

## Fases

A **v1 é um anonimizador simples, utilizável já** nos casos mais comuns. As fases seguintes completam o
produto para usos mais amplos e para a publicação externa. O que não está na v1 está em
[backlog.md](backlog.md).

### v1 — o que entra

| Área | Conteúdo |
|---|---|
| Tipos | CPF, CNPJ (numérico e alfanumérico), telefone, e-mail, nome (em coluna) |
| Transformação | FF1 para CPF/CNPJ/telefone; HMAC + dicionário para nome e e-mail |
| Modos | **Direto** (só anonimiza, nada é guardado); a sessão com restauração foi para a v1.x |
| Entrada | Valor individual e `IDataReader` (`AnonymizingDataReader`, que envolve o reader original); JSON foi para a v1.x |
| Classificação | Por coluna, uma vez por resultado: configuração explícita + heurística por nome de coluna + varredura de texto livre por padrão (CPF, CNPJ, e-mail, telefone) |
| Usos-alvo | MCP SQL somente leitura e script que anonimiza um banco de teste via `UPDATE` |
| Distribuição | `0.x` prerelease no NuGet assim que o reader funcionar; 1.0 depois do uso real |
| Política para campos não classificados | `Passar` ou `Bloquear` |
| Chave secreta | Valor direto ou variável de ambiente |
| Qualidade | Cuidados de pacote público desde o início (ver seção própria) |

### Andamento da v1

| Etapa | Estado |
|---|---|
| Infraestrutura (solução, analisadores, CPM, testes, CI no GitHub Actions) | Concluída |
| 1. FF1 sobre AES (`Cryptography/Primitives`, `Cryptography/Fpe`) | Concluída: vetores oficiais do NIST, teste diferencial contra o BouncyCastle (só nos testes), bijeção exaustiva e testes de propriedade |
| 2. Subchaves por tipo, seleção por HMAC e *cycle-walking* (`Cryptography/Keys`, `Cryptography/Selection`, `Cryptography/Fpe`) | Concluída: HKDF-SHA256 com rótulos versionados e valores dourados, seleção sem viés de módulo, *cycle-walking* com bijeção exaustiva no subconjunto permitido |
| 3. Documentos (`Documents`, `Resources`) | Concluída: CPF, CNPJ numérico e alfanumérico, telefone, nome e e-mail, com representação original, valores proibidos e dicionários embutidos |
| 4. Transformação (`Transformation`, `Cryptography/Keys/KeyRing`, `Cryptography/Fpe/Ff1Pool`) | Concluída: um transformador por tipo ("valor + tipo → substituto"), interface pública `IValueTransformer`, chave mestra descartada após derivar as subchaves, pool de instâncias do FF1 entre threads, valores dourados conferidos contra o BouncyCastle |
| 5. Classificação por coluna (`Classification`) | Concluída: regras explícitas por `schema.tabela.coluna`, proteção de chaves internas, heurística por nome, varredura de texto e política, decididas uma vez por resultado a partir do esquema |
| 6–8. `IDataReader` + API mínima, detecção em texto livre, documentação e `0.x` | Não iniciadas |

### Depois da v1

- **v1.x:** JSON; sessão com restauração e sua persistência cifrada; CEP, data de nascimento, cartão,
  endereço, identificador genérico; CSV; streaming; amostragem de conteúdo na classificação; relatório de
  auditoria; outros provedores de chave.
- **v2:** nomes em texto livre (NER), placeholders semânticos para LLM, coerência entre campos.

## Decisões tomadas

| Tema | Decisão |
|---|---|
| Plataforma | .NET 8+ (suporte a .NET Standard 2.0 vai para o backlog) |
| Foco da v1 | Anonimizador simples, determinístico, para linhas de banco (`IDataReader`), voltado ao MCP somente leitura e ao script de banco de teste |
| Restauração | v1.x (nenhum dos usos-alvo da v1 precisa dela) |
| Nomes em texto livre (NER) | v2 |
| Distribuição | Repositório público no GitHub desde o início; NuGet público após as fases de teste. Cuidados de pacote público desde o início |
| Licença | Apache 2.0 (inclui concessão de patentes dos contribuidores) |
| Política para colunas não classificadas | `Passar` por padrão, com varredura das colunas de texto; `Bloquear` bloqueia também o texto não classificado |
| Coluna `cpf_cnpj`/`documento` | Tenta CPF e depois CNPJ; o tamanho do valor desambigua |
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
| CPF | 9 dígitos da base | dígitos verificadores recalculados; dígitos todos iguais proibidos |
| CNPJ numérico | raiz (8 dígitos) | ordem (`0001` = matriz) preservada; DVs recalculados; raiz com dígitos todos iguais proibida |
| CNPJ alfanumérico (vigente desde jul/2026) | raiz (8 posições, base 36) | ordem preservada; DVs recalculados (valor do caractere = ASCII − 48); raiz sem letra proibida |
| Telefone | 7 dígitos finais | código do país, DDD, o 9 do celular e o primeiro dígito do assinante preservados |

Valores proibidos também são recusados na entrada (um `111.111.111-11` não é tratado como CPF).

Notas:

- Cifrar a raiz do CNPJ separadamente faz com que matriz e filiais da mesma empresa continuem
  relacionadas.
- A ordem do CNPJ (4 dígitos) fica abaixo do domínio mínimo recomendado para FF1 (10⁶), por isso é
  preservada em vez de cifrada.
- CNPJ numérico gera CNPJ numérico (não quebra campos `bigint`/`numeric`). Para CNPJ alfanumérico,
  aplica-se *cycle-walking* para garantir que a saída contenha ao menos uma letra e não colida com o
  domínio numérico.
- No telefone, o primeiro dígito do assinante distingue fixo (2 a 5) de celular; preservá-lo mantém o
  substituto na mesma faixa sem *cycle-walking*, inclusive para celulares antigos de 8 dígitos (6 a 9).
  Os 7 dígitos restantes (10⁷) ficam acima do domínio mínimo do FF1. O DDD precisa ser um dos 67 em uso.
- O tweak do FF1 do telefone é o DDD + o início preservado do assinante (`11/98`): DDD e início não mudam,
  só os 7 dígitos finais são substituídos, com uma permutação própria por DDD e faixa. Assim, dois números
  com o mesmo final em DDDs diferentes não ganham o mesmo final substituto. O código do país não entra no
  tweak; o mesmo número escrito sem DDD, sim, gera outro final.
- Um CPF fictício válido pode coincidir com o CPF de alguma pessoa real. Isso é inerente a gerar documentos
  válidos e deve constar na documentação.

### Atributos que não relacionam registros

Nome e e-mail usam seleção determinística derivada de **HMAC-SHA256** (chave secreta + valor normalizado)
sobre dicionários embutidos. Colisões aqui são toleráveis.

- **Nome:** prenome + sobrenomes escolhidos do dicionário pelo HMAC, mantendo o número de partes, as
  partículas ("da", "dos"…) e sufixos ("Filho", "Júnior"…), a caixa, o uso de acentos e o sexo do prenome
  quando ele está no dicionário. A chave do HMAC é o nome completo normalizado (sem acentos, em
  maiúsculas): o mesmo nome gera sempre o mesmo substituto, mas dois nomes que só compartilham o sobrenome
  recebem, em geral, sobrenomes substitutos diferentes — o substituto não revela parentesco.
- **E-mail:** parte local substituída; domínio de provedor público (gmail.com, outlook.com…) preservado;
  domínio corporativo substituído deterministicamente por um nome fictício, mantendo o sufixo (`com.br`,
  `gov.br`…). A parte local substituta segue o estilo da original (separador e dígitos no fim) e a
  etiqueta depois do `+` é descartada: endereços que só diferem por ela (`joao+promo@…` e `joao@…`)
  recebem o mesmo substituto. O domínio corporativo é sorteado pelo domínio inteiro, então todos os
  endereços de uma empresa ficam com o mesmo domínio fictício. Um domínio fictício pode coincidir com um
  real, como no CPF.
- **Dicionários:** seleção própria do projeto, guiada pelas frequências de nomes do IBGE (ver `NOTICE`);
  nenhum arquivo de terceiros é copiado. A ordem dos itens faz parte do contrato: itens novos vão no fim.

### Chave secreta

- Na v1, fornecida como valor direto ou por variável de ambiente.
- Trocar a chave muda todos os substitutos (rotação = nova "versão" da base anonimizada).
- Quem tem a chave consegue reverter a FPE. Portanto o resultado é **pseudonimização** (LGPD, art. 13),
  não anonimização (art. 12). A documentação deve dizer isso explicitamente.

## Modos de uso

### Direto

Anonimiza e não guarda nada. É o modo para gerar bases, alimentar o proxy MCP ou processar em massa.

### Com restauração (sessão) — v1.x

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

## Classificação de colunas

Decidida uma vez por resultado, só a partir do esquema. Para cada coluna, a primeira regra que se aplicar
vence:

1. **Configuração explícita:** `schema.tabela.coluna`, `tabela.coluna` ou `coluna` → tipo, `ignorar` ou
   `varrer`; a mais específica vence.
2. **Chaves internas:** identity/autoincremento, GUID e nomes de ID (`id_usuario`, `IdTelefone`,
   `cliente_id`) passam intactos. Ser chave primária não basta: um CPF pode ser chave natural.
3. **Heurística por nome de coluna:** `cpf`, `nr_cpf`, `cnpj`, `cpf_cnpj`, `documento`, `email`,
   `telefone`, `celular`, `nome`, `nome_da_mae`, …; atributos do dado (`tipo_telefone`, `email_status`) e
   nomes que não são de pessoa (`nome_produto`, `razao_social`) ficam de fora.
4. **Texto livre não classificado:** varredura com detectores por padrão (CPF, CNPJ, e-mail, telefone)
   dentro do texto, como em colunas `observacao`.
5. **Política para o restante:** `Passar` (padrão) ou `Bloquear`.

A origem informada pelo provedor (`BaseTableName`/`BaseColumnName`) vale mais que o alias. Pelo alias, só
uma regra de transformação se aplica: um alias escrito na consulta nunca desliga a anonimização.

Com `Bloquear`, nada que não tenha sido classificado como seguro sai: só passam as chaves internas e as
colunas ignoradas por configuração, e o texto não classificado é bloqueado em vez de varrido (a varredura
não pega nomes; NER é v2).

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
