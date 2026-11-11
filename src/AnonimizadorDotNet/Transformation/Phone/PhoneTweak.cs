using System.Text;
using AnonimizadorDotNet.Documents.Phone;

namespace AnonimizadorDotNet.Transformation.Phone;

/// <summary>O tweak do FF1 para telefones: a parte preservada que identifica a faixa do número.</summary>
/// <remarks>
/// <para>
/// O tweak é <c>DDD/início do assinante</c> em ASCII, por exemplo <c>11/98</c> para <c>(11) 98765-4321</c> e
/// <c>/3</c> para <c>3456-7890</c>, sem DDD. O início do assinante é o que o substituto preserva: o primeiro
/// dígito e, no celular de 9 dígitos, também o seguinte. O código do país e o prefixo de longa distância
/// não entram, pois não mudam o número.
/// </para>
/// <para>
/// Sem tweak, dois números com os mesmos 7 dígitos finais em DDDs ou faixas diferentes ganhariam o mesmo
/// final no substituto, revelando a coincidência. Com ele, cada DDD e faixa usa uma permutação própria.
/// Em troca, o mesmo número escrito com e sem DDD gera finais diferentes.
/// </para>
/// <para>O formato faz parte do determinismo: mudá-lo altera todos os telefones substitutos.</para>
/// </remarks>
internal static class PhoneTweak
{
    /// <summary>Tamanho máximo do tweak: DDD (2) + barra (1) + início do assinante (2).</summary>
    public const int MaxLength = 5;

    /// <summary>Escreve o tweak de um telefone.</summary>
    /// <param name="phone">O telefone interpretado.</param>
    /// <param name="destination">Destino com ao menos <see cref="MaxLength"/> bytes.</param>
    /// <returns>Quantidade de bytes escritos.</returns>
    public static int Write(ParsedPhone phone, Span<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(phone);
        int subscriberStart = phone.IsMobile ? 2 : 1;
        ReadOnlySpan<char> preserved = phone.Preserved;
        ReadOnlySpan<char> start = preserved[^subscriberStart..];

        int written = 0;
        if (phone.Ddd is { } ddd)
        {
            written += Encoding.ASCII.GetBytes(ddd, destination);
        }

        destination[written++] = (byte)'/';
        written += Encoding.ASCII.GetBytes(start, destination[written..]);
        return written;
    }
}
