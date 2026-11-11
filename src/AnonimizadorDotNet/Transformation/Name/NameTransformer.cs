using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using AnonimizadorDotNet.Cryptography.Keys;
using AnonimizadorDotNet.Cryptography.Selection;
using AnonimizadorDotNet.Documents.Name;
using AnonimizadorDotNet.Resources;

namespace AnonimizadorDotNet.Transformation.Name;

/// <summary>Substitui um nome de pessoa por um nome fictício escolhido nos dicionários por HMAC.</summary>
/// <remarks>
/// <para>
/// A chave da seleção é o <b>nome completo</b> normalizado (sem acentos, em maiúsculas, partes separadas por
/// um espaço): o mesmo nome gera sempre o mesmo substituto, qualquer que seja a caixa, os acentos ou os
/// espaços da entrada. Cada parte substituível é sorteada com um contexto próprio, a sua posição no nome.
/// Por isso dois nomes que só compartilham o sobrenome ("Maria Silva" e "João Silva") recebem, em geral,
/// sobrenomes substitutos diferentes: o substituto não revela parentesco.
/// </para>
/// <para>
/// Prenomes saem da lista do sexo do primeiro prenome; se ele não está no dicionário, o sexo é sorteado.
/// Sobrenomes saem da lista de sobrenomes. Uma inicial na primeira posição vira a inicial de um prenome; nas
/// demais, de um sobrenome. Partículas, sufixos, espaços, caixa e acentos seguem o original
/// (<see cref="PersonNameFormat.Format"/>). Colisões são possíveis e aceitas: o nome não relaciona registros.
/// </para>
/// <para>Seguro para uso por várias threads ao mesmo tempo.</para>
/// </remarks>
internal sealed class NameTransformer : IDisposable
{
    private readonly SecretKey _key;

    /// <summary>Cria o transformador com a subchave de nome do chaveiro.</summary>
    public NameTransformer(KeyRing keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        _key = keys.CreateSubkey(KeyLabels.HmacName);
    }

    /// <summary>Gera o nome substituto.</summary>
    /// <param name="value">O nome como chegou.</param>
    /// <param name="substitute">O nome substituto, no estilo do original.</param>
    /// <returns>Falso se o valor não for um nome de pessoa. Nunca lança exceção por causa do conteúdo.</returns>
    /// <exception cref="ObjectDisposedException">O transformador já foi descartado.</exception>
    public bool TryTransform(ReadOnlySpan<char> value, [NotNullWhen(true)] out string? substitute)
    {
        substitute = null;
        ReadOnlySpan<byte> key = _key.Span;
        if (!PersonNameFormat.TryParse(value, out ParsedPersonName? name))
        {
            return false;
        }

        byte[] fullName = Encoding.UTF8.GetBytes(string.Join(' ', name.Parts.Select(part => part.Key)));
        ImmutableArray<string> givenNames = name.Gender switch
        {
            Gender.Male => EmbeddedLists.MaleGivenNames,
            Gender.Female => EmbeddedLists.FemaleGivenNames,
            _ => HmacSelector.SelectIndex(key, HmacContexts.NameGender, fullName, 2) == 0
                ? EmbeddedLists.MaleGivenNames
                : EmbeddedLists.FemaleGivenNames,
        };

        var replacements = new List<string>(name.ReplaceableCount);
        for (int position = 0; position < name.Parts.Length; position++)
        {
            NamePart part = name.Parts[position];
            if (!part.IsReplaceable)
            {
                continue;
            }

            bool isGivenName = part.Kind == NamePartKind.GivenName || (part.Kind == NamePartKind.Initial && position == 0);
            ImmutableArray<string> list = isGivenName ? givenNames : EmbeddedLists.Surnames;
            int index = HmacSelector.SelectIndex(key, HmacContexts.NamePart(position), fullName, list.Length);
            replacements.Add(list[index]);
        }

        substitute = PersonNameFormat.Format(name, replacements);
        return true;
    }

    /// <summary>Zera a subchave.</summary>
    public void Dispose() => _key.Dispose();
}
