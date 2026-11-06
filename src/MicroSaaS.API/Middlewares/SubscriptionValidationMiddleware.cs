using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MicroSaaS.Core.Entities;
using MicroSaaS.Core.Interfaces;
using MicroSaaS.Infrastructure.Authentication;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.API.Middlewares;

/// <summary>
/// Middleware que se ejecuta inmediatamente después de <see cref="TenantResolutionMiddleware"/>
/// y verifica en el esquema <c>public</c> que la suscripción del Tenant esté activa y vigente.
/// Si se encuentra inactiva, vencida o morosa, aborta la petición con <c>402 Payment Required</c>.
/// </summary>
public class SubscriptionValidationMiddleware
{
    private readonly RequestDelegate _next;

    public SubscriptionValidationMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantProvider tenantProvider,
        MasterDbContext masterDbContext)
    {
        // Permitir siempre peticiones preflight CORS (OPTIONS) y rutas excluidas (/api/Auth/login, etc.)
        if (HttpMethods.IsOptions(context.Request.Method) || IsExemptPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var currentSchema = tenantProvider.GetCurrentSchema();
        var tenantIdClaim = context.User.FindFirstValue(JwtProvider.TenantIdClaimName);
        var tenantSchemaClaim = context.User.FindFirstValue(JwtProvider.TenantSchemaClaimName);

        // Si la petición pertenece a un Tenant (por esquema resuelto o Claim tenant_id), validar su suscripción
        if ((!string.IsNullOrWhiteSpace(currentSchema) &&
             !string.Equals(currentSchema, "public", StringComparison.OrdinalIgnoreCase)) ||
            Guid.TryParse(tenantIdClaim, out _))
        {
            Tenant? tenant = null;

            if (Guid.TryParse(tenantIdClaim, out var tenantId))
            {
                tenant = await masterDbContext.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == tenantId, context.RequestAborted);
            }

            // Fallback por SchemaName (útil cuando la BD fue recreada y el esquema determinístico t_<rut> se mantiene)
            if (tenant is null)
            {
                var lookupSchema = !string.IsNullOrWhiteSpace(tenantSchemaClaim)
                    ? tenantSchemaClaim
                    : currentSchema;

                if (!string.IsNullOrWhiteSpace(lookupSchema) &&
                    !string.Equals(lookupSchema, "public", StringComparison.OrdinalIgnoreCase))
                {
                    tenant = await masterDbContext.Tenants
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.SchemaName == lookupSchema, context.RequestAborted);
                }
            }

            // Si el Tenant no existe en absoluto en la BD maestra, el token JWT es huérfano (401 Unauthorized)
            if (tenant is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    status = StatusCodes.Status401Unauthorized,
                    error = "Unauthorized",
                    message = "La sesión actual pertenece a un Tenant que ya no existe. Inicia sesión nuevamente."
                });
                return;
            }

            if (!tenant.IsActive)
            {
                await WritePaymentRequiredResponseAsync(
                    context,
                    "El Tenant se encuentra inactivo. Debe regularizar su suscripción.");
                return;
            }

            var subscription = await masterDbContext.Subscriptions
                .AsNoTracking()
                .Where(s => s.TenantId == tenant.Id)
                .OrderByDescending(s => s.NextDueDate)
                .FirstOrDefaultAsync(context.RequestAborted);

            var isInvalidOrExpired = subscription is null
                || subscription.Status != SubscriptionStatus.Active
                || subscription.NextDueDate <= DateTime.UtcNow;

            if (isInvalidOrExpired)
            {
                await WritePaymentRequiredResponseAsync(
                    context,
                    "Suscripción inactiva, vencida o morosa. Se requiere el pago para acceder al servicio.");
                return;
            }
        }

        await _next(context);
    }

    private static bool IsExemptPath(PathString requestPath)
    {
        var path = requestPath.Value ?? string.Empty;

        return path.StartsWith("/api/Auth/login", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/Auth", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/Tenant/register", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/tenants/register", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/Subscriptions/pay", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/Subscriptions/create", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/scalar", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WritePaymentRequiredResponseAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(new
        {
            status = StatusCodes.Status402PaymentRequired,
            error = "Payment Required",
            message
        });
    }
}
