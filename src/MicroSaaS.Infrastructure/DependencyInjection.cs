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
        services.AddScoped<ITenantProvisioner, TenantProvisioner>();
        services.AddScoped<IJwtProvider, JwtProvider>();
        services.AddScoped<IEncryptionService, SymmetricEncryptionService>();
        services.AddScoped<IDocumentSignatureService, SiiSignatureService>();
        services.AddScoped<ISiiAuthenticationService, SiiAuthenticationService>();
        services.AddScoped<ISiiReceptionService, SiiReceptionService>();
        services.AddScoped<ITedGeneratorService, TedGeneratorService>();
        services.AddScoped<IDteBuilderService, SiiDteBuilderService>();
        services.AddScoped<SchemaInterceptor>();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // MasterDbContext: Seguro usar DbContextPool. Opera siempre sobre "public".
        // Ahorra RAM al reciclar las instancias de C#
        services.AddDbContextPool<MasterDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(2),
                    errorCodesToAdd: null);
            });
        }, poolSize: 60);

        // TenantDbContext: DEBE mantenerse sin Pool (AddDbContext)
        // Garantiza que el SchemaInterceptor resuelva el Tenant correcto por cada request
        services.AddDbContext<TenantDbContext>((serviceProvider, options) =>
        {
            var schemaInterceptor = serviceProvider.GetRequiredService<SchemaInterceptor>();

            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(2),
                    errorCodesToAdd: null);
            })
            .AddInterceptors(schemaInterceptor);
        });

        return services;
    }
}