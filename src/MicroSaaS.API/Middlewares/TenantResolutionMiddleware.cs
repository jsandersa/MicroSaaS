using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.API.Middlewares;

/// <summary>
/// Middleware encargado de resolver el esquema del Tenant actual a partir de los Claims del JWT
/// o del header HTTP personalizado <c>x-tenant-schema</c> e inyectarlo en <see cref="ITenantProvider"/>.
/// </summary>
public class TenantResolutionMiddleware
{
    public const string TenantHeaderName = "x-tenant-schema";
    public const string TenantClaimType = "tenant_schema";
    public const string TenantIdClaimType = "tenant_id";

    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext context, ITenantProvider tenantProvider)
    {
        // 1. Priorizar el Claim del usuario autenticado (JWT)
        var schema = context.User.FindFirst(TenantClaimType)?.Value
                     ?? context.User.FindFirst(TenantHeaderName)?.Value;

        var tenantIdClaim = context.User.FindFirst(TenantIdClaimType)?.Value;
        if (Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            tenantProvider.SetCurrentTenantId(tenantId);
        }

        // 2. Si no viene en los Claims, intentar resolver desde el header HTTP "x-tenant-schema"
        if (string.IsNullOrWhiteSpace(schema) &&
            context.Request.Headers.TryGetValue(TenantHeaderName, out var headerValues))
        {
            schema = headerValues.FirstOrDefault();
        }

        if (!string.IsNullOrWhiteSpace(schema))
        {
            tenantProvider.SetCurrentSchema(schema);
        }

        await _next(context);
    }
}

