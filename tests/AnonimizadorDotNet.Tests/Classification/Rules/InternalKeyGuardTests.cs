using AnonimizadorDotNet.Classification.Columns;
using AnonimizadorDotNet.Classification.Rules;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Classification.Rules;

public sealed class InternalKeyGuardTests
{
    [Theory]
    [InlineData("id")]
    [InlineData("ID")]
    [InlineData("id_usuario")]
    [InlineData("IdUsuario")]
    [InlineData("cliente_id")]
    [InlineData("ClienteId")]
    [InlineData("ClienteID")]
    [InlineData("IdTelefone")]
    [InlineData("telefone_id")]
    [InlineData("id_email")]
    [InlineData("IdCpf")]
    [InlineData("fk_cliente")]
    [InlineData("pk_pessoa")]
    [InlineData("pessoa_uuid")]
    [InlineData("cliente_id_2")]
    public void NomeDeId_EhChaveInterna(string name) =>
        Assert.True(InternalKeyGuard.IsInternalKey(new ColumnInfo(0, name, typeof(long))));

    [Theory]
    [InlineData("cpf")]
    [InlineData("identidade")]
    [InlineData("idade")]
    [InlineData("cpf_identificador")]
    [InlineData("telefone")]
    [InlineData("valido")]
    [InlineData("")]
    public void OutrosNomes_NaoSaoChaveInterna(string name) =>
        Assert.False(InternalKeyGuard.IsInternalKey(new ColumnInfo(0, name, typeof(string))));

    [Fact]
    public void Identity_EhChaveInterna() =>
        Assert.True(InternalKeyGuard.IsInternalKey(new ColumnInfo(0, "numero", typeof(int)) { IsIdentity = true }));

    [Theory]
    [InlineData(typeof(Guid))]
    [InlineData(typeof(Guid?))]
    public void Guid_EhChaveInterna(Type type) =>
        Assert.True(InternalKeyGuard.IsInternalKey(new ColumnInfo(0, "codigo", type)));

    [Theory]
    [InlineData("uniqueidentifier")]
    [InlineData("UUID")]
    [InlineData("rowversion")]
    public void TipoDeChaveNoBanco_EhChaveInterna(string typeName) =>
        Assert.True(InternalKeyGuard.IsInternalKey(new ColumnInfo(0, "codigo", typeof(string)) { DataTypeName = typeName }));

    [Fact]
    public void ComOrigem_UsaONomeDeOrigem_NaoOAlias()
    {
        var aliasedPhone = new ColumnInfo(0, "id", typeof(string)) { BaseColumnName = "telefone" };
        var aliasedKey = new ColumnInfo(0, "telefone", typeof(long)) { BaseColumnName = "IdTelefone" };

        Assert.False(InternalKeyGuard.IsInternalKey(aliasedPhone));
        Assert.True(InternalKeyGuard.IsInternalKey(aliasedKey));
    }

    [Fact]
    public void NomeDeIdComQualquerPalavra_EhSempreChaveInterna_EAHeuristicaNaoPrecisaDecidir()
    {
        string[] words = ["cpf", "cnpj", "telefone", "celular", "email", "nome", "documento", "cliente"];
        Gen.Select(Gen.OneOfConst(words), Gen.Int[0, 5], Gen.Int[0, 3]).Sample((word, form, digits) =>
        {
            string pascal = char.ToUpperInvariant(word[0]) + word[1..];
            string suffix = digits == 0 ? string.Empty : $"_{digits}";
            string name = form switch
            {
                0 => $"id_{word}{suffix}",
                1 => $"{word}_id{suffix}",
                2 => $"Id{pascal}",
                3 => $"{pascal}Id",
                4 => $"FK_{word.ToUpperInvariant()}",
                _ => $"{pascal}ID{suffix}",
            };

            Assert.True(InternalKeyGuard.IsInternalKey(new ColumnInfo(0, name, typeof(long))), name);
        });
    }
}
