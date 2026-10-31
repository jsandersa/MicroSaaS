using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.Infrastructure.Interceptors;

/// <summary>
/// Interceptor de EF Core que ajusta dinámicamente el <c>search_path</c> de PostgreSQL
/// al esquema del Tenant actual en la conexión activa antes de ejecutar el comando original,
/// sin alterar el <c>CommandText</c> del lote de EF Core.
/// </summary>
public class SchemaInterceptor : DbCommandInterceptor
{
    private readonly ITenantProvider _tenantProvider;

    public SchemaInterceptor(ITenantProvider tenantProvider)
    {
        _tenantProvider = tenantProvider ?? throw new ArgumentNullException(nameof(tenantProvider));
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        await ApplyTenantSchemaAsync(command, cancellationToken);
        return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await ApplyTenantSchemaAsync(command, cancellationToken);
        return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        await ApplyTenantSchemaAsync(command, cancellationToken);
        return await base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    private async Task ApplyTenantSchemaAsync(DbCommand command, CancellationToken cancellationToken)
    {
        var schema = _tenantProvider.GetCurrentSchema();

        if (string.IsNullOrWhiteSpace(schema) || command.Connection is null)
        {
            return;
        }

        if (command.Connection.State != ConnectionState.Open)
        {
            await command.Connection.OpenAsync(cancellationToken);
        }

        await using var setSchemaCommand = command.Connection.CreateCommand();
        setSchemaCommand.Transaction = command.Transaction;
        setSchemaCommand.CommandText = $"SET search_path TO \"{schema}\", public;";
        await setSchemaCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
