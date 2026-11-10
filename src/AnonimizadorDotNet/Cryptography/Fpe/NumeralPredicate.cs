namespace AnonimizadorDotNet.Cryptography.Fpe;

/// <summary>Diz se uma cadeia de numerais pertence ao subconjunto permitido de um domínio.</summary>
/// <param name="numerals">Numerais, cada um entre 0 e radix − 1.</param>
/// <returns><see langword="true"/> se o valor é permitido.</returns>
internal delegate bool NumeralPredicate(ReadOnlySpan<byte> numerals);
