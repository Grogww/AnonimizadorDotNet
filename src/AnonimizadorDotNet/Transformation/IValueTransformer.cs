using System.Diagnostics.CodeAnalysis;

namespace AnonimizadorDotNet.Transformation;

/// <summary>Gera o substituto de um valor de um tipo de dado pessoal.</summary>
/// <remarks>
/// <para>
/// A biblioteca traz transformadores para CPF, CNPJ, telefone, e-mail e nome; esta interface permite
/// acrescentar tipos próprios. Uma implementação deve:
/// </para>
/// <list type="bullet">
/// <item><description>ser determinística: o mesmo valor gera sempre o mesmo substituto;</description></item>
/// <item><description>poder ser chamada por várias threads ao mesmo tempo;</description></item>
/// <item><description>devolver <see langword="false"/>, sem lançar exceção, quando o valor não é do seu tipo;</description></item>
/// <item><description>nunca incluir o valor original em exceções, logs ou <see cref="object.ToString"/>.</description></item>
/// </list>
/// </remarks>
public interface IValueTransformer
{
    /// <summary>Nome do tipo tratado (por exemplo, <c>cpf</c>), único entre os transformadores registrados.</summary>
    /// <remarks>A comparação entre nomes de tipo ignora maiúsculas e minúsculas.</remarks>
    string Type { get; }

    /// <summary>Gera o substituto de um valor.</summary>
    /// <param name="value">O valor como chegou.</param>
    /// <param name="kind">Se o valor chegou como texto ou como número.</param>
    /// <param name="substitute">O substituto, na mesma representação da entrada.</param>
    /// <returns><see langword="false"/> se o valor não for deste tipo.</returns>
    bool TryTransform(ReadOnlySpan<char> value, ValueKind kind, [NotNullWhen(true)] out string? substitute);
}
