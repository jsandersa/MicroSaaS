using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MicroSaaS.Core.Interfaces;
using MicroSaaS.Infrastructure.Authentication;
using MicroSaaS.Infrastructure.Interceptors;
using MicroSaaS.Infrastructure.Multitenancy;
using MicroSaaS.Infrastructure.Persistence;
using MicroSaaS.Infrastructure.Security;
using MicroSaaS.Infrastructure.Sii;

namespace MicroSaaS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpClient(SiiAuthenticationService.HttpClientName);

        services.AddScoped<ITenantProvider, TenantProvider>();
        services.AddScoped<IJwtProvider, JwtProvider>();
        services.AddScoped<IEncryptionService, SymmetricEncryptionService>();
        services.AddScoped<IDocumentSignatureService, SiiSignatureService>();
        services.AddScoped<ISiiAuthenticationService, SiiAuthenticationService>();
        services.AddScoped<ISiiReceptionService, SiiReceptionService>();
        services.AddScoped<ITedGeneratorService, TedGeneratorService>();
        services.AddScoped<IDteBuilderService, SiiDteBuilderService>();
        services.AddScoped<SchemaInterceptor>();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // MasterDbContext opera siempre sobre el esquema fijo "public"
        services.AddDbContext<MasterDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        // TenantDbContext resuelve dinámicamente el esquema por petición mediante SchemaInterceptor
        services.AddDbContext<TenantDbContext>((serviceProvider, options) =>
        {
            var schemaInterceptor = serviceProvider.GetRequiredService<SchemaInterceptor>();

            options.UseNpgsql(connectionString)
                   .AddInterceptors(schemaInterceptor);
        });

        return services;
    }
}
