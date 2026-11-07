using MicroSaaS.API.Middlewares;
using MicroSaaS.Application.Common.Seeding;
using MicroSaaS.Application.Tenants.Commands.RegisterTenant;
using MicroSaaS.Core.Interfaces;
using MicroSaaS.Infrastructure;
using MicroSaaS.Infrastructure.Persistence;
using MicroSaaS.Infrastructure.RealTime;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();

// Configuración de CORS compatible con SignalR y WebSockets
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Registro de MediatR (capa Application) y Pipeline Behavior de Autorización
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(RegisterTenantCommand).Assembly);
    cfg.AddOpenBehavior(typeof(MicroSaaS.Application.Common.Behaviors.AuthorizationBehavior<,>));
});

// Registro de Infraestructura (ITenantProvider, IJwtProvider, SchemaInterceptor y MasterDbContext)
builder.Services.AddInfrastructure(builder.Configuration);

// Configuración de Autenticación y Autorización JWT (evitando strings vacíos en appsettings.json)
static string FirstNonEmpty(params string?[] values) =>
    values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))!.Trim();

var jwtSecretKey = FirstNonEmpty(
    builder.Configuration["JwtSettings:Secret"],
    builder.Configuration["Jwt:SecretKey"],
    "MicroSaaS_SuperSecretKey_For_Development_Min32Chars!");

var jwtIssuer = FirstNonEmpty(
    builder.Configuration["JwtSettings:Issuer"],
    builder.Configuration["Jwt:Issuer"],
    "MicroSaaS.API");

var jwtAudience = FirstNonEmpty(
    builder.Configuration["JwtSettings:Audience"],
    builder.Configuration["Jwt:Audience"],
    "MicroSaaS.Client");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
            ClockSkew = TimeSpan.Zero
        };

        // INTEGRACIÓN SIGNALR: Interceptar el token en la URL (Query String)
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/inventory"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Registro de SignalR y notificador en tiempo real
builder.Services.AddSignalR();
builder.Services.AddScoped<IInventoryNotifier, SignalRInventoryNotifier>();

var app = builder.Build();

// Aplicar migraciones del esquema maestro y ejecutar Data Seeding automáticamente al iniciar el contenedor
using (var scope = app.Services.CreateScope())
{
    var masterDb = scope.ServiceProvider.GetRequiredService<MasterDbContext>();

    try
    {
        await masterDb.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Advertencia al ejecutar MigrateAsync en MasterDbContext; continuando con Data Seeding.");
    }

    try
    {
        await DatabaseSeeder.SeedAsync(scope.ServiceProvider);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Ocurrió un error durante el Data Seeding inicial, continuando inicio de API.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference(options =>
    {
        options.Title = "MicroSaaS ERP - SII API";
    });

    app.MapGet("/docs", context =>
    {
        context.Response.Redirect("/scalar/v1");
        return Task.CompletedTask;
    });
}

// app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();

// Middleware de resolución de esquema Multi-Tenant
app.UseMiddleware<TenantResolutionMiddleware>();

// Middleware de validación de estado y vigencia de la suscripción del Tenant
app.UseMiddleware<SubscriptionValidationMiddleware>();

app.UseAuthorization();
app.MapControllers();
app.MapHub<InventoryHub>("/hubs/inventory");

await app.RunAsync();