using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MicroSaaS.API.Middlewares;
using MicroSaaS.Application.Common.Seeding;
using MicroSaaS.Application.Tenants.Commands.RegisterTenant;
using MicroSaaS.Infrastructure;
using MicroSaaS.Infrastructure.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();

// Configuración de CORS para el portal Angular
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Registro de MediatR (capa Application)
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(RegisterTenantCommand).Assembly));

// Registro de Infraestructura (ITenantProvider, IJwtProvider, SchemaInterceptor y MasterDbContext)
builder.Services.AddInfrastructure(builder.Configuration);

// Configuración de Autenticación y Autorización JWT
var jwtSecretKey = builder.Configuration["JwtSettings:Secret"]
    ?? builder.Configuration["Jwt:SecretKey"]
    ?? "MicroSaaS_SuperSecretKey_For_Development_Min32Chars!";
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"]
    ?? builder.Configuration["Jwt:Issuer"]
    ?? "MicroSaaS.API";
var jwtAudience = builder.Configuration["JwtSettings:Audience"]
    ?? builder.Configuration["Jwt:Audience"]
    ?? "MicroSaaS.Client";

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
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Aplicar migraciones del esquema maestro y ejecutar Data Seeding automáticamente al iniciar el contenedor
using (var scope = app.Services.CreateScope())
{
    var masterDb = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
    await masterDb.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // 1. Registramos la interfaz gráfica sin invocar propiedades inexistentes
    app.MapScalarApiReference(options =>
    {
        options.Title = "MicroSaaS ERP - SII API";
    });

    // 2. Mapeamos de forma nativa la ruta personalizada para asegurar la resolución
    app.MapGet("/docs", context =>
    {
        context.Response.Redirect("/scalar/v1");
        return Task.CompletedTask;
    });
}

//app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

app.UseAuthentication();

// Middleware de resolución de esquema Multi-Tenant (se ejecuta después de UseAuthentication para leer los Claims del JWT)
app.UseMiddleware<TenantResolutionMiddleware>();

// Middleware de validación de estado y vigencia de la suscripción del Tenant (retorna 402 Payment Required si está vencida/morosa)
app.UseMiddleware<SubscriptionValidationMiddleware>();

app.UseAuthorization();

app.MapControllers();

await app.RunAsync();
