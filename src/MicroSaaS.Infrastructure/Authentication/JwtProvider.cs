using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MicroSaaS.Core.Entities;
using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.Infrastructure.Authentication;

/// <summary>
/// Implementación de <see cref="IJwtProvider"/> encargada de emitir tokens JWT firmados
/// con los claims de identidad (<c>sub</c>, <c>tenant_id</c>, <c>email</c>, <c>role</c>) y el esquema Multi-Tenant (<c>tenant_schema</c>).
/// </summary>
public class JwtProvider : IJwtProvider
{
    public const string TenantIdClaimName = "tenant_id";
    public const string TenantSchemaClaimName = "tenant_schema";

    private readonly IConfiguration _configuration;

    public JwtProvider(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public string Generate(User user, string schemaName)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);

        var secretKey = FirstNonEmpty(
            _configuration["JwtSettings:Secret"],
            _configuration["Jwt:SecretKey"],
            "MicroSaaS_SuperSecretKey_For_Development_Min32Chars!");

        var issuer = FirstNonEmpty(
            _configuration["JwtSettings:Issuer"],
            _configuration["Jwt:Issuer"],
            "MicroSaaS.API");

        var audience = FirstNonEmpty(
            _configuration["JwtSettings:Audience"],
            _configuration["Jwt:Audience"],
            "MicroSaaS.Client");

        var expiryMinutes = int.TryParse(_configuration["JwtSettings:ExpiryMinutes"], out var minutes) && minutes > 0
            ? minutes
            : 1440;

        var resolvedRole = !string.IsNullOrWhiteSpace(user.Role) ? user.Role : "Owner";
        var resolvedCashRegisterId = !string.IsNullOrWhiteSpace(user.CashRegisterId)
            ? user.CashRegisterId
            : (resolvedRole == "Accountant" ? "" : "CAJA-01");
        var resolvedPaymentDeviceId = !string.IsNullOrWhiteSpace(user.PaymentDeviceId)
            ? user.PaymentDeviceId
            : (resolvedRole == "Accountant" ? "" : "MP-POINT-01");

        var claims = new Claim[]
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(TenantIdClaimName, user.TenantId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(TenantSchemaClaimName, schemaName),
            new("role", resolvedRole),
            new(ClaimTypes.Role, resolvedRole),
            new("cash_register_id", resolvedCashRegisterId),
            new("payment_device_id", resolvedPaymentDeviceId)
        };

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string FirstNonEmpty(params string?[] candidates) =>
        candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))!.Trim();
}
