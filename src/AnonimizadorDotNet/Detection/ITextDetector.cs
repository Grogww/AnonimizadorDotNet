namespace AnonimizadorDotNet.Detection;

/// <summary>Encontra, num texto livre, os trechos de um tipo de dado pessoal.</summary>
/// <remarks>
/// <para>
/// O detector só aponta onde estão os trechos; quem gera o substituto é o transformador do mesmo
/// <see cref="Type"/>, que também valida o trecho. Um trecho que o transformador recusa fica como está. Assim
/// o mesmo valor gera o mesmo substituto no texto e numa coluna classificada.
/// </para>
/// <para>
/// A biblioteca traz detectores para CPF, CNPJ, e-mail e telefone; esta interface permite acrescentar outros,
/// que se somam aos embutidos. Uma implementação deve:
/// </para>
/// <list type="bullet">
/// <item><description>ser determinística e poder ser chamada por várias threads ao mesmo tempo;</description></item>
/// <item><description>devolver só trechos dentro do texto, com tamanho maior que zero;</description></item>
/// <item><description>nunca incluir o texto em exceções, logs ou <see cref="object.ToString"/>.</description></item>
/// </list>
/// <para>
/// Quando dois trechos se sobrepõem, vence o que começa antes; no empate, o mais longo; no empate, o dos
/// detectores embutidos (CNPJ, CPF, e-mail, telefone) e depois o dos próprios, na ordem de registro.
/// </para>
/// </remarks>
public interface ITextDetector
{
    /// <summary>O tipo dos trechos encontrados: o de um transformador registrado (por exemplo, <c>cpf</c>).</summary>
    /// <remarks>A comparação entre nomes de tipo ignora maiúsculas e minúsculas.</remarks>
    string Type { get; }

    /// <summary>Encontra os trechos do tipo num texto.</summary>
    /// <param name="text">O texto livre.</param>
    /// <returns>Os trechos candidatos, em qualquer ordem; podem se sobrepor.</returns>
    IEnumerable<TextMatch> Detect(string text);
}
