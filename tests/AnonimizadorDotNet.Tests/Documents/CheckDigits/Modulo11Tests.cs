using AnonimizadorDotNet.Documents.CheckDigits;

namespace AnonimizadorDotNet.Tests.Documents.CheckDigits;

public sealed class Modulo11Tests
{
    [Theory]
    [InlineData(new[] { 0 }, new[] { 2 }, 0)] // resto 0
    [InlineData(new[] { 6 }, new[] { 2 }, 0)] // resto 1
    [InlineData(new[] { 1 }, new[] { 2 }, 9)] // resto 2
    [InlineData(new[] { 5 }, new[] { 2 }, 1)] // resto 10
    [InlineData(new[] { 1, 1 }, new[] { 3, 2 }, 6)] // soma 5
    public void Compute_SegueARegraDoModulo11(int[] values, int[] weights, int expected) =>
        Assert.Equal(expected, Modulo11.Compute(values, weights));

    [Fact]
    public void Compute_TamanhosDiferentes_Lanca() =>
        Assert.Throws<ArgumentException>(() => Modulo11.Compute([1, 2], [3]));
}
