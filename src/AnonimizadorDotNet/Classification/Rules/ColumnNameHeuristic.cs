using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using AnonimizadorDotNet.Classification.Columns;
using AnonimizadorDotNet.Transformation;

namespace AnonimizadorDotNet.Classification.Rules;

/// <summary>Deduz o tipo de dado pessoal pelo nome da coluna.</summary>
/// <remarks>
/// <para>O nome é comparado parte a parte (<see cref="ColumnNameTokens"/>):</para>
/// <list type="bullet">
/// <item><description>CPF: alguma parte <c>cpf</c>;</description></item>
/// <item><description>CNPJ: alguma parte <c>cnpj</c>;</description></item>
/// <item><description>
/// CPF ou CNPJ: <c>cpf</c> e <c>cnpj</c> juntos, <c>cpfcnpj</c>, ou <c>documento</c>/<c>doc</c> sozinho ou
/// com qualificadores (<c>nr_documento</c>, <c>documento_cliente</c>); o transformador de CPF é tentado
/// antes, e o tamanho do valor desambigua;
/// </description></item>
/// <item><description>e-mail: <c>email</c>, <c>e_mail</c> ou <c>mail</c> sozinho;</description></item>
/// <item><description>telefone: <c>telefone</c>, <c>fone</c>, <c>tel</c>, <c>celular</c>, <c>whatsapp</c>, <c>phone</c>…;</description></item>
/// <item><description>
/// nome: <c>nome</c>/<c>name</c>/<c>nm</c> sozinho ou com qualificadores de pessoa (<c>nome_cliente</c>,
/// <c>nome_da_mae</c>, <c>first_name</c>), além de <c>prenome</c> e <c>sobrenome</c>. <c>nome_produto</c>,
/// <c>nome_fantasia</c> e <c>razao_social</c> não são nomes de pessoa.
/// </description></item>
/// </list>
/// <para>
/// Partes como <c>tipo</c>, <c>status</c>, <c>flag</c>, <c>data</c> ou <c>qtd</c> indicam um atributo do
/// dado, não o dado (<c>tipo_telefone</c>, <c>email_status</c>), e impedem a classificação. CPF, CNPJ e
/// telefone exigem coluna de texto ou inteira; e-mail e nome, de texto.
/// </para>
/// </remarks>
internal static class ColumnNameHeuristic
{
    private static readonly IReadOnlyList<DocumentType> Cpf = [DocumentType.Cpf];
    private static readonly IReadOnlyList<DocumentType> Cnpj = [DocumentType.Cnpj];
    private static readonly IReadOnlyList<DocumentType> CpfOrCnpj = [DocumentType.Cpf, DocumentType.Cnpj];
    private static readonly IReadOnlyList<DocumentType> Email = [DocumentType.Email];
    private static readonly IReadOnlyList<DocumentType> Phone = [DocumentType.Phone];
    private static readonly IReadOnlyList<DocumentType> Name = [DocumentType.Name];

    private static readonly FrozenSet<string> Negators = Set(
        "tipo", "tp", "flag", "fl", "ind", "indicador", "possui", "tem", "has", "is", "valido", "validado",
        "verificado", "confirmado", "verified", "data", "dt", "date", "qtd", "quantidade", "count", "status",
        "situacao", "ddd", "ramal", "mascara", "formato", "type");

    private static readonly FrozenSet<string> PhoneTokens = Set(
        "telefone", "telefones", "fone", "tel", "celular", "cel", "whatsapp", "whats", "phone", "mobile", "fax");

    private static readonly FrozenSet<string> NameTokens = Set("nome", "name", "nm");

    private static readonly FrozenSet<string> Connectors = Set("de", "da", "do", "das", "dos", "of");

    private static readonly FrozenSet<string> PersonQualifiers = Set(
        "completo", "cliente", "pessoa", "mae", "pai", "responsavel", "contato", "titular", "social", "civil",
        "paciente", "aluno", "funcionario", "colaborador", "socio", "beneficiario", "dependente", "conjuge",
        "motorista", "proprietario", "tutor", "medico", "segurado", "devedor", "fiador", "avalista",
        "full", "first", "last", "middle", "customer", "person", "contact", "mother", "father", "client");

    private static readonly FrozenSet<string> DocumentQualifiers = Set(
        "nr", "num", "numero", "no", "n", "cliente", "pessoa", "titular", "responsavel", "fornecedor",
        "empresa", "emitente", "destinatario", "tomador", "prestador");

    /// <summary>Os tipos candidatos, na ordem em que os transformadores devem ser tentados.</summary>
    /// <param name="name">O nome da coluna.</param>
    /// <param name="storage">Como a coluna guarda os valores.</param>
    /// <param name="candidates">Os tipos, quando o nome indica um dado pessoal compatível com o armazenamento.</param>
    public static bool TryMatch(
        string name, ColumnStorage storage, [NotNullWhen(true)] out IReadOnlyList<DocumentType>? candidates)
    {
        ArgumentNullException.ThrowIfNull(name);
        candidates = Classify(ColumnNameTokens.Split(name));

        bool compatible = candidates switch
        {
            null => false,
            _ when candidates == Email || candidates == Name => storage == ColumnStorage.Text,
            _ => storage is ColumnStorage.Text or ColumnStorage.Integer,
        };

        if (!compatible)
        {
            candidates = null;
        }

        return compatible;
    }

    private static IReadOnlyList<DocumentType>? Classify(string[] tokens)
    {
        if (tokens.Length == 0 || tokens.Any(Negators.Contains))
        {
            return null;
        }

        bool cpf = tokens.Any(t => t is "cpf" or "cpfs");
        bool cnpj = tokens.Any(t => t is "cnpj" or "cnpjs");
        if ((cpf && cnpj) || tokens.Any(t => t is "cpfcnpj" or "cnpjcpf"))
        {
            return CpfOrCnpj;
        }

        if (cpf)
        {
            return Cpf;
        }

        if (cnpj)
        {
            return Cnpj;
        }

        if (OnlyQualified(tokens, t => t is "documento" or "doc", DocumentQualifiers))
        {
            return CpfOrCnpj;
        }

        if (tokens.Any(t => t is "email" or "emails") || HasAdjacent(tokens, "e", "mail") || tokens is ["mail"])
        {
            return Email;
        }

        if (tokens.Any(PhoneTokens.Contains))
        {
            return Phone;
        }

        if (tokens is ["prenome"] or ["sobrenome"] || OnlyQualified(tokens, NameTokens.Contains, PersonQualifiers))
        {
            return Name;
        }

        return null;
    }

    /// <summary>
    /// Uma parte principal, e todas as outras são qualificadores, conectivos (<c>de</c>, <c>da</c>…) ou dígitos.
    /// </summary>
    private static bool OnlyQualified(string[] tokens, Func<string, bool> isMain, FrozenSet<string> qualifiers)
    {
        int main = Array.FindIndex(tokens, t => isMain(t));
        if (main < 0)
        {
            return false;
        }

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (i != main && !qualifiers.Contains(token) && !Connectors.Contains(token) && !char.IsAsciiDigit(token[0]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasAdjacent(string[] tokens, string first, string second)
    {
        for (int i = 0; i + 1 < tokens.Length; i++)
        {
            if (tokens[i] == first && tokens[i + 1] == second)
            {
                return true;
            }
        }

        return false;
    }

    private static FrozenSet<string> Set(params string[] items) => items.ToFrozenSet(StringComparer.Ordinal);
}
