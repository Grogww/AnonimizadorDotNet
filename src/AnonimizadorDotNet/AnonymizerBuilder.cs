using System.Security.Cryptography;
using AnonimizadorDotNet.Classification;
using AnonimizadorDotNet.Classification.Rules;
using AnonimizadorDotNet.Cryptography.Keys;
using AnonimizadorDotNet.Data;
using AnonimizadorDotNet.Detection;
using AnonimizadorDotNet.Transformation;

namespace AnonimizadorDotNet;

/// <summary>Configura e cria um <see cref="Anonymizer"/>: chave secreta, regras por coluna, política, tipos e detectores próprios.</summary>
/// <remarks>
/// <para>
/// O builder é de uso único: <see cref="Build"/> zera a cópia da chave e, depois dele, qualquer método lança
/// <see cref="InvalidOperationException"/>. Para criar outro anonimizador, use outro builder com a mesma chave.
/// </para>
/// <para>
/// Colunas são indicadas como <c>coluna</c>, <c>tabela.coluna</c> ou <c>schema.tabela.coluna</c>, sem
/// diferenciar maiúsculas e minúsculas; cada parte pode vir entre <c>[ ]</c>, <c>" "</c> ou <c>` `</c>. A
/// regra mais específica vence. Quando o provedor informa a origem da coluna, ela vale mais que o alias da
/// consulta, e um alias nunca desliga a anonimização.
/// </para>
/// <para>Não é seguro para várias threads.</para>
/// </remarks>
/// <example>
/// <code>
/// using var anonymizer = new AnonymizerBuilder()
///     .UseKeyFromEnvironment("ANONIMIZADOR_CHAVE")
///     .TransformColumn("dbo.Clientes.doc", PersonalDataTypes.Cpf)
///     .IgnoreColumn("observacao_interna")
///     .Build();
///
/// using var reader = anonymizer.Wrap(command.ExecuteReader());
/// </code>
/// </example>
public sealed class AnonymizerBuilder
{
    private static readonly string[] BuiltInTypes =
        [PersonalDataTypes.Cpf, PersonalDataTypes.Cnpj, PersonalDataTypes.Phone, PersonalDataTypes.Email, PersonalDataTypes.Name];

    private readonly List<ColumnRule> _rules = [];
    private readonly HashSet<string> _targets = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IValueTransformer> _extensions = [];
    private readonly List<ITextDetector> _detectors = [];
    private readonly HashSet<string> _types = new(BuiltInTypes, StringComparer.OrdinalIgnoreCase);
    private byte[]? _key;
    private string? _keyVariable;
    private UnclassifiedPolicy _policy = UnclassifiedPolicy.Pass;
    private bool _built;

    /// <summary>Usa uma chave secreta informada diretamente.</summary>
    /// <param name="key">
    /// Ao menos 32 bytes aleatórios. É copiada para memória fixada, zerada em <see cref="Build"/>; o buffer do
    /// chamador pode ser zerado em seguida.
    /// </param>
    /// <returns>Este builder.</returns>
    /// <exception cref="ArgumentException">Chave com menos de 32 bytes.</exception>
    /// <exception cref="InvalidOperationException"><see cref="Build"/> já foi chamado.</exception>
    public AnonymizerBuilder UseKey(ReadOnlySpan<byte> key)
    {
        ThrowIfBuilt();
        if (key.Length < SubkeyDerivation.MinMasterKeySize)
        {
            throw new ArgumentException(
                $"A chave secreta deve ter ao menos {SubkeyDerivation.MinMasterKeySize} bytes (256 bits).", nameof(key));
        }

        ClearKey();
        _key = GC.AllocateUninitializedArray<byte>(key.Length, pinned: true);
        key.CopyTo(_key);
        _keyVariable = null;
        return this;
    }

    /// <summary>Usa a chave secreta de uma variável de ambiente, lida em <see cref="Build"/>.</summary>
    /// <param name="variableName">Nome da variável; o conteúdo deve ser Base64 de ao menos 32 bytes.</param>
    /// <returns>Este builder.</returns>
    /// <remarks>
    /// Os bytes decodificados ficam em memória fixada e são zerados depois do uso, mas o texto da variável é
    /// uma <see cref="string"/> do .NET e não pode ser zerado.
    /// </remarks>
    /// <exception cref="ArgumentException">Nome vazio.</exception>
    /// <exception cref="InvalidOperationException"><see cref="Build"/> já foi chamado.</exception>
    public AnonymizerBuilder UseKeyFromEnvironment(string variableName)
    {
        ThrowIfBuilt();
        ArgumentException.ThrowIfNullOrWhiteSpace(variableName);
        ClearKey();
        _keyVariable = variableName;
        return this;
    }

    /// <summary>Transforma uma coluna com o transformador de um tipo.</summary>
    /// <param name="column"><c>coluna</c>, <c>tabela.coluna</c> ou <c>schema.tabela.coluna</c>.</param>
    /// <param name="type">Um de <see cref="PersonalDataTypes"/> ou o de um transformador próprio (ver <see cref="AddTransformer"/>).</param>
    /// <returns>Este builder.</returns>
    /// <exception cref="ArgumentException">Coluna inválida, já configurada, ou tipo vazio.</exception>
    /// <exception cref="InvalidOperationException"><see cref="Build"/> já foi chamado.</exception>
    public AnonymizerBuilder TransformColumn(string column, string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        return AddRule(column, target => ColumnRule.Transform(target, type));
    }

    /// <summary>Passa uma coluna intacta (dado não pessoal, ou chave que a heurística não reconhece).</summary>
    /// <param name="column"><c>coluna</c>, <c>tabela.coluna</c> ou <c>schema.tabela.coluna</c>.</param>
    /// <returns>Este builder.</returns>
    /// <remarks>Quando o provedor informa a origem da coluna, a regra só se aplica pela origem, nunca pelo alias.</remarks>
    /// <exception cref="ArgumentException">Coluna inválida ou já configurada.</exception>
    /// <exception cref="InvalidOperationException"><see cref="Build"/> já foi chamado.</exception>
    public AnonymizerBuilder IgnoreColumn(string column) => AddRule(column, ColumnRule.Ignore);

    /// <summary>Varre uma coluna como texto livre, mesmo com a política <see cref="UnclassifiedPolicy.Block"/>.</summary>
    /// <param name="column"><c>coluna</c>, <c>tabela.coluna</c> ou <c>schema.tabela.coluna</c>.</param>
    /// <returns>Este builder.</returns>
    /// <exception cref="ArgumentException">Coluna inválida ou já configurada.</exception>
    /// <exception cref="InvalidOperationException"><see cref="Build"/> já foi chamado.</exception>
    public AnonymizerBuilder ScanColumn(string column) => AddRule(column, ColumnRule.Scan);

    /// <summary>Define o que fazer com as colunas não classificadas; o padrão é <see cref="UnclassifiedPolicy.Pass"/>.</summary>
    /// <param name="policy">A política.</param>
    /// <returns>Este builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Política desconhecida.</exception>
    /// <exception cref="InvalidOperationException"><see cref="Build"/> já foi chamado.</exception>
    public AnonymizerBuilder WithUnclassifiedPolicy(UnclassifiedPolicy policy)
    {
        ThrowIfBuilt();
        if (!Enum.IsDefined(policy))
        {
            throw new ArgumentOutOfRangeException(nameof(policy), "Política desconhecida para colunas não classificadas.");
        }

        _policy = policy;
        return this;
    }

    /// <summary>Registra um transformador para um tipo próprio.</summary>
    /// <param name="transformer">O transformador; o anonimizador não o descarta.</param>
    /// <returns>Este builder.</returns>
    /// <exception cref="ArgumentException">Nome de tipo vazio ou já registrado (inclusive os embutidos).</exception>
    /// <exception cref="InvalidOperationException"><see cref="Build"/> já foi chamado.</exception>
    public AnonymizerBuilder AddTransformer(IValueTransformer transformer)
    {
        ThrowIfBuilt();
        ArgumentNullException.ThrowIfNull(transformer);
        string type = transformer.Type;
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Todo transformador precisa de um nome de tipo.", nameof(transformer));
        }

        if (!_types.Add(type))
        {
            throw new ArgumentException($"Já existe um transformador para o tipo '{type}'.", nameof(transformer));
        }

        _extensions.Add(transformer);
        return this;
    }

    /// <summary>Registra um detector para a varredura de texto livre, somado aos embutidos (CPF, CNPJ, e-mail, telefone).</summary>
    /// <param name="detector">
    /// O detector; o tipo dele precisa ter transformador (embutido ou registrado por <see cref="AddTransformer"/>),
    /// que gera o substituto e valida cada trecho. O anonimizador não o descarta.
    /// </param>
    /// <returns>Este builder.</returns>
    /// <remarks>Os detectores embutidos não podem ser desligados: um detector próprio só acrescenta trechos.</remarks>
    /// <exception cref="ArgumentException">Nome de tipo vazio, ou o mesmo detector registrado duas vezes.</exception>
    /// <exception cref="InvalidOperationException"><see cref="Build"/> já foi chamado.</exception>
    public AnonymizerBuilder AddDetector(ITextDetector detector)
    {
        ThrowIfBuilt();
        ArgumentNullException.ThrowIfNull(detector);
        if (string.IsNullOrWhiteSpace(detector.Type))
        {
            throw new ArgumentException("Todo detector precisa de um nome de tipo.", nameof(detector));
        }

        if (_detectors.Exists(registered => ReferenceEquals(registered, detector)))
        {
            throw new ArgumentException("Este detector já foi registrado.", nameof(detector));
        }

        _detectors.Add(detector);
        return this;
    }

    /// <summary>Cria o anonimizador e zera a cópia da chave guardada pelo builder.</summary>
    /// <returns>O anonimizador, que deve ser descartado ao fim do uso.</returns>
    /// <exception cref="InvalidOperationException">
    /// <see cref="Build"/> já foi chamado; nenhuma chave informada; variável de ambiente ausente, fora de
    /// Base64 ou com menos de 32 bytes; ou uma regra ou um detector pede um tipo sem transformador.
    /// </exception>
    public Anonymizer Build()
    {
        ThrowIfBuilt();
        _built = true;
        try
        {
            ValidateRuleTypes();
            TransformerSet transformers = CreateTransformers();
            try
            {
                var classifier = new ColumnClassifier(transformers, new ColumnRuleSet(_rules), ToBehavior(_policy));
                var scanner = new TextScanner(transformers, _detectors);
                return new Anonymizer(transformers, new ReaderContext(classifier, scanner));
            }
            catch
            {
                transformers.Dispose();
                throw;
            }
        }
        finally
        {
            ClearKey();
        }
    }

    private AnonymizerBuilder AddRule(string column, Func<string, ColumnRule> create)
    {
        ThrowIfBuilt();
        ArgumentNullException.ThrowIfNull(column);
        ColumnRule rule;
        try
        {
            rule = create(column);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException(
                "A coluna deve ser 'coluna', 'tabela.coluna' ou 'schema.tabela.coluna', sem partes vazias e com os delimitadores fechados.",
                nameof(column),
                ex);
        }

        if (!_targets.Add(rule.Target.Key))
        {
            throw new ArgumentException($"A coluna '{rule.Target}' já tem uma regra.", nameof(column));
        }

        _rules.Add(rule);
        return this;
    }

    private void ValidateRuleTypes()
    {
        foreach (ITextDetector detector in _detectors)
        {
            if (!_types.Contains(detector.Type))
            {
                throw new InvalidOperationException($"Não há transformador para o tipo '{detector.Type}' de um detector registrado.");
            }
        }

        foreach (ColumnRule rule in _rules)
        {
            if (rule.Kind == ColumnRuleKind.Transform && !_types.Contains(rule.Type!))
            {
                throw new InvalidOperationException(
                    $"Não há transformador para o tipo '{rule.Type}', pedido pela regra da coluna '{rule.Target}'.");
            }
        }
    }

    private TransformerSet CreateTransformers()
    {
        if (_key is not null)
        {
            return new TransformerSet(_key, _extensions);
        }

        if (_keyVariable is null)
        {
            throw new InvalidOperationException("Nenhuma chave secreta informada: use UseKey ou UseKeyFromEnvironment.");
        }

        string? encoded = Environment.GetEnvironmentVariable(_keyVariable);
        if (string.IsNullOrWhiteSpace(encoded))
        {
            throw new InvalidOperationException($"A variável de ambiente '{_keyVariable}' não está definida.");
        }

        byte[] buffer = GC.AllocateArray<byte>((encoded.Length / 4 * 3) + 3, pinned: true);
        try
        {
            if (!Convert.TryFromBase64String(encoded.Trim(), buffer, out int written))
            {
                throw new InvalidOperationException($"A variável de ambiente '{_keyVariable}' não contém Base64 válido.");
            }

            if (written < SubkeyDerivation.MinMasterKeySize)
            {
                throw new InvalidOperationException(
                    $"A chave da variável de ambiente '{_keyVariable}' deve ter ao menos {SubkeyDerivation.MinMasterKeySize} bytes (256 bits).");
            }

            return new TransformerSet(buffer.AsSpan(0, written), _extensions);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    private void ClearKey()
    {
        if (_key is not null)
        {
            CryptographicOperations.ZeroMemory(_key);
            _key = null;
        }
    }

    private static UnclassifiedBehavior ToBehavior(UnclassifiedPolicy policy) =>
        policy == UnclassifiedPolicy.Block ? UnclassifiedBehavior.Block : UnclassifiedBehavior.Pass;

    private void ThrowIfBuilt()
    {
        if (_built)
        {
            throw new InvalidOperationException("O builder já criou um anonimizador; use outro builder.");
        }
    }
}
