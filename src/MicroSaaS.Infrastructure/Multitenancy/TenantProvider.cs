using System.Text.RegularExpressions;
using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.Infrastructure.Multitenancy;

/// <summary>
/// Implementación Scoped de <see cref="ITenantProvider"/> que almacena el esquema resuelto durante el ciclo de vida del request HTTP.
/// </summary>
public partial class TenantProvider : ITenantProvider
{
    private const string DefaultSchema = "public";
    private string _currentSchema = DefaultSchema;
    private Guid? _currentTenantId;

    [GeneratedRegex("^[a-z0-9_]+$", RegexOptions.Compiled)]
    private static partial Regex ValidSchemaRegex();

    public string GetCurrentSchema() => _currentSchema;

    public Guid? GetCurrentTenantId() => _currentTenantId;

    public void SetCurrentTenantId(Guid? tenantId)
    {
        _currentTenantId = tenantId;
    }

    public void SetCurrentSchema(string schema)
    {
        if (string.IsNullOrWhiteSpace(schema))
        {
            _currentSchema = DefaultSchema;
            return;
        }

        var normalized = schema.Trim().ToLowerInvariant();

        if (!ValidSchemaRegex().IsMatch(normalized))
        {
            throw new ArgumentException($"El nombre de esquema '{schema}' contiene caracteres no permitidos.", nameof(schema));
        }

        _currentSchema = normalized;
    }
}

