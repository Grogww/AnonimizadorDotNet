namespace AnonimizadorDotNet.Documents.CheckDigits;

/// <summary>Dígito verificador por módulo 11, como no CPF e no CNPJ.</summary>
internal static class Modulo11
{
    /// <summary>
    /// Calcula o dígito: soma ponderada dos valores; resto menor que 2 dá 0, senão 11 − resto.
    /// </summary>
    /// <param name="values">Valores de cada posição (no CNPJ alfanumérico, o código ASCII − 48).</param>
    /// <param name="weights">Peso de cada posição; mesmo tamanho de <paramref name="values"/>.</param>
    /// <exception cref="ArgumentException">Os tamanhos não conferem.</exception>
    public static byte Compute(ReadOnlySpan<int> values, ReadOnlySpan<int> weights)
    {
        if (values.Length != weights.Length)
        {
            throw new ArgumentException("Valores e pesos precisam ter o mesmo tamanho.", nameof(weights));
        }

        int sum = 0;
        for (int i = 0; i < values.Length; i++)
        {
            sum += values[i] * weights[i];
        }

        int remainder = sum % 11;
        return (byte)(remainder < 2 ? 0 : 11 - remainder);
    }
}
