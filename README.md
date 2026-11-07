# AnonimizadorDotNet

Biblioteca .NET que substitui dados pessoais brasileiros (CPF, CNPJ, telefone, e-mail, nome) em dados
estruturados por valores fictícios, **localmente e sem nenhuma chamada de rede**.

> **Estado: em desenvolvimento.** Ainda não há pacote publicado nem API estável.

## O que a biblioteca faz

Os substitutos gerados são:

- **Válidos no formato:** um CPF fictício passa na validação de dígito verificador.
- **Determinísticos:** com a mesma chave secreta, o mesmo CPF real vira sempre o mesmo CPF fictício,
  entre tabelas, consultas e execuções.
- **Sem colisão** nos identificadores usados para relacionar registros: dois CPFs reais diferentes nunca
  viram o mesmo CPF fictício.
- **Restauráveis**, quando necessário: é possível anonimizar dados antes de enviá-los a um LLM e devolver os
  valores originais na resposta.

Assim, relacionamentos entre registros continuam funcionando sem expor dados pessoais. O caso de uso
motivador é ficar entre um servidor MCP SQL somente leitura e um LLM, para que nenhum dado pessoal saia do
ambiente local.

## Pseudonimização, não anonimização

Quem tem a chave secreta consegue reverter as substituições. O resultado é, portanto, **pseudonimização**
(LGPD, art. 13), e não anonimização (art. 12). A biblioteca reduz riscos, mas não substitui uma avaliação
jurídica sobre o tratamento de dados pessoais.

## Requisitos

.NET 8 ou superior.

## Documentação do projeto

- [Escopo](docs/escopo.md): propósito, decisões e funcionamento.
- [Arquitetura](docs/arquitetura.md): organização da solução e das camadas.
- [Backlog](docs/backlog.md): melhorias previstas após a v1.

## Licença

Distribuído sob a [Apache License 2.0](LICENSE).
