namespace AnonimizadorDotNet.Cryptography.Fpe;

/// <summary>
/// <i>Cycle-walking</i> sobre o FF1: restringe a permutação a um subconjunto permitido do domínio,
/// cifrando de novo enquanto o resultado cair num valor proibido.
/// </summary>
/// <remarks>
/// <para>
/// O FF1 decompõe o domínio em ciclos. Partindo de um valor permitido e seguindo o ciclo até o próximo
/// valor permitido, cada entrada permitida recebe uma saída permitida única: o resultado continua sendo
/// uma bijeção, agora só no subconjunto. A decifra percorre o ciclo no sentido contrário com o mesmo
/// predicado. O processo sempre termina, pois o próprio valor de entrada está no ciclo.
/// </para>
/// <para>
/// O número esperado de cifras é |domínio| / |permitidos|. O limite de iterações é só uma proteção contra
/// predicados que permitem quase nada; atingi-lo lança exceção, já que devolver um valor proibido
/// quebraria a bijeção.
/// </para>
/// <para>Herda do <see cref="Ff1"/> a falta de segurança para uso simultâneo por várias threads.</para>
/// </remarks>
internal static class CycleWalking
{
    /// <summary>Limite padrão de cifras por chamada.</summary>
    public const int DefaultMaxIterations = 1_000;

    /// <summary>Cifra um valor permitido, devolvendo outro valor permitido.</summary>
    /// <param name="ff1">Instância do FF1 com a chave e o radix do domínio.</param>
    /// <param name="input">Numerais da entrada; devem satisfazer <paramref name="isAllowed"/>.</param>
    /// <param name="tweak">Tweak público; pode ser vazio.</param>
    /// <param name="output">Destino, com o mesmo tamanho da entrada. Pode ser o mesmo buffer da entrada.</param>
    /// <param name="isAllowed">Predicado do subconjunto permitido.</param>
    /// <param name="maxIterations">Quantidade máxima de cifras; ao menos 1.</param>
    /// <exception cref="ArgumentException">A entrada não é permitida ou é inválida para o FF1.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxIterations"/> menor que 1.</exception>
    /// <exception cref="InvalidOperationException">O limite de iterações foi atingido.</exception>
    public static void Encrypt(
        Ff1 ff1,
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> tweak,
        Span<byte> output,
        NumeralPredicate isAllowed,
        int maxIterations = DefaultMaxIterations) =>
        Walk(ff1, input, tweak, output, isAllowed, maxIterations, encrypt: true);

    /// <summary>Decifra um valor produzido por <see cref="Encrypt"/>.</summary>
    /// <param name="ff1">Instância do FF1 com a mesma chave e o mesmo radix da cifra.</param>
    /// <param name="input">Numerais cifrados; devem satisfazer <paramref name="isAllowed"/>.</param>
    /// <param name="tweak">O mesmo tweak usado na cifra.</param>
    /// <param name="output">Destino, com o mesmo tamanho da entrada. Pode ser o mesmo buffer da entrada.</param>
    /// <param name="isAllowed">O mesmo predicado usado na cifra.</param>
    /// <param name="maxIterations">Quantidade máxima de decifras; ao menos 1.</param>
    /// <exception cref="ArgumentException">A entrada não é permitida ou é inválida para o FF1.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxIterations"/> menor que 1.</exception>
    /// <exception cref="InvalidOperationException">O limite de iterações foi atingido.</exception>
    public static void Decrypt(
        Ff1 ff1,
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> tweak,
        Span<byte> output,
        NumeralPredicate isAllowed,
        int maxIterations = DefaultMaxIterations) =>
        Walk(ff1, input, tweak, output, isAllowed, maxIterations, encrypt: false);

    private static void Walk(
        Ff1 ff1,
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> tweak,
        Span<byte> output,
        NumeralPredicate isAllowed,
        int maxIterations,
        bool encrypt)
    {
        ArgumentNullException.ThrowIfNull(ff1);
        ArgumentNullException.ThrowIfNull(isAllowed);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxIterations, 1);

        if (!isAllowed(input))
        {
            // A mensagem não traz o valor: a entrada pode ser um dado pessoal.
            throw new ArgumentException("A entrada não pertence ao subconjunto permitido do domínio.", nameof(input));
        }

        Step(ff1, input, tweak, output, encrypt);
        for (int iteration = 1; !isAllowed(output); iteration++)
        {
            if (iteration == maxIterations)
            {
                throw new InvalidOperationException(
                    $"O cycle-walking atingiu o limite de {maxIterations} iterações sem chegar a um valor permitido.");
            }

            Step(ff1, output, tweak, output, encrypt);
        }
    }

    private static void Step(Ff1 ff1, ReadOnlySpan<byte> input, ReadOnlySpan<byte> tweak, Span<byte> output, bool encrypt)
    {
        if (encrypt)
        {
            ff1.Encrypt(input, tweak, output);
        }
        else
        {
            ff1.Decrypt(input, tweak, output);
        }
    }
}
