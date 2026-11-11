using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using AnonimizadorDotNet.Cryptography.Keys;
using AnonimizadorDotNet.Transformation.Cnpj;
using AnonimizadorDotNet.Transformation.Cpf;
using AnonimizadorDotNet.Transformation.Email;
using AnonimizadorDotNet.Transformation.Name;
using AnonimizadorDotNet.Transformation.Phone;

namespace AnonimizadorDotNet.Transformation;

/// <summary>Os transformadores de uma chave mestra: os embutidos e os registrados pelo consumidor.</summary>
/// <remarks>
/// <para>
/// A chave mestra só é usada na construção, para derivar as subchaves dos transformadores embutidos; depois
/// disso a cópia dela é zerada e só ficam as subchaves, que <see cref="Dispose"/> zera.
/// </para>
/// <para>
/// O conjunto não muda depois de criado e pode ser usado por várias threads ao mesmo tempo. Transformadores
/// próprios não são descartados aqui: quem os criou é dono deles.
/// </para>
/// </remarks>
internal sealed class TransformerSet : IDisposable
{
    private readonly IDisposable[] _owned;
    private readonly IValueTransformer[] _builtIn;
    private readonly FrozenDictionary<string, IValueTransformer> _byType;

    /// <summary>Cria os transformadores embutidos e registra os próprios.</summary>
    /// <param name="masterKey">Chave mestra aleatória, com ao menos 32 bytes. Não fica retida.</param>
    /// <param name="extensions">Transformadores próprios; o nome de tipo de cada um precisa ser único.</param>
    /// <exception cref="ArgumentException">
    /// Chave mestra curta demais, transformador nulo, nome de tipo vazio ou repetido.
    /// </exception>
    public TransformerSet(ReadOnlySpan<byte> masterKey, IEnumerable<IValueTransformer>? extensions = null)
    {
        var owned = new List<IDisposable>();
        try
        {
            using (var keys = new KeyRing(masterKey))
            {
                _builtIn = new IValueTransformer[]
                {
                    Own(new CpfTransformer(keys)),
                    Own(new CnpjTransformer(keys)),
                    Own(new PhoneTransformer(keys)),
                    Own(new EmailTransformer(keys)),
                    Own(new NameTransformer(keys)),
                };
            }

            var byType = new Dictionary<string, IValueTransformer>(StringComparer.OrdinalIgnoreCase);
            foreach (IValueTransformer transformer in _builtIn.Concat(extensions ?? []))
            {
                if (transformer is null)
                {
                    throw new ArgumentException("A lista de transformadores contém um item nulo.", nameof(extensions));
                }

                string type = transformer.Type;
                if (string.IsNullOrWhiteSpace(type))
                {
                    throw new ArgumentException("Todo transformador precisa de um nome de tipo.", nameof(extensions));
                }

                if (!byType.TryAdd(type, transformer))
                {
                    throw new ArgumentException($"Já existe um transformador para o tipo '{type}'.", nameof(extensions));
                }
            }

            _byType = byType.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
            _owned = [.. owned];
        }
        catch
        {
            owned.ForEach(item => item.Dispose());
            throw;
        }

        T Own<T>(T item)
            where T : IDisposable
        {
            owned.Add(item);
            return item;
        }
    }

    /// <summary>Os nomes de tipo registrados, embutidos e próprios.</summary>
    public IEnumerable<string> Types => _byType.Keys;

    /// <summary>O transformador embutido de um tipo.</summary>
    public IValueTransformer this[DocumentType type] => _builtIn[(int)type];

    /// <summary>Procura o transformador de um nome de tipo, sem diferenciar maiúsculas e minúsculas.</summary>
    public bool TryGet(string type, [NotNullWhen(true)] out IValueTransformer? transformer)
    {
        ArgumentNullException.ThrowIfNull(type);
        return _byType.TryGetValue(type, out transformer);
    }

    /// <summary>Zera as subchaves dos transformadores embutidos.</summary>
    public void Dispose()
    {
        foreach (IDisposable item in _owned)
        {
            item.Dispose();
        }
    }
}
