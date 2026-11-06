using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MicroSaaS.Core.Interfaces;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.Users.Commands.Login;

/// <summary>
/// Manejador del comando <see cref="LoginCommand"/> que verifica las credenciales del usuario,
/// resuelve el <c>SchemaName</c> de su Tenant activo y emite el JWT firmado.
/// </summary>
public class LoginCommandHandler : IRequestHandler<LoginCommand, string>
{
    private readonly MasterDbContext _context;
    private readonly IJwtProvider _jwtProvider;

    public LoginCommandHandler(MasterDbContext context, IJwtProvider jwtProvider)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _jwtProvider = jwtProvider ?? throw new ArgumentNullException(nameof(jwtProvider));
    }

    public async Task<string> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Email);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Password);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user is null || !VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Credenciales inválidas.");
        }

        var tenant = await _context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == user.TenantId, cancellationToken);

        if (tenant is null)
        {
            throw new UnauthorizedAccessException("El Tenant asociado al usuario no existe.");
        }

        return _jwtProvider.Generate(user, tenant.SchemaName);
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        var computedHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
        var computedBytes = Encoding.UTF8.GetBytes(computedHash);
        var storedBytes = Encoding.UTF8.GetBytes(storedHash);

        return computedBytes.Length == storedBytes.Length
               && CryptographicOperations.FixedTimeEquals(computedBytes, storedBytes);
    }
}
