using System.Reflection;
using MediatR;
using MicroSaaS.Application.Common.Security;
using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.Application.Common.Behaviors;

/// <summary>
/// Pipeline Behavior de MediatR que intercepta cada <typeparamref name="TRequest"/> decorado con <see cref="AuthorizeRoleAttribute"/>
/// y verifica que el usuario autenticado posea alguno de los roles requeridos antes de invocar al Handler.
/// </summary>
public sealed class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUserService _currentUserService;

    public AuthorizationBehavior(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var authorizeAttributes = request
            .GetType()
            .GetCustomAttributes<AuthorizeRoleAttribute>(inherit: true)
            .ToArray();

        if (authorizeAttributes.Length == 0)
        {
            return await next();
        }

        if (!_currentUserService.IsAuthenticated ||
            _currentUserService.UserId == Guid.Empty ||
            _currentUserService.TenantId == Guid.Empty)
        {
            throw new UnauthorizedAccessException(
                "El usuario no se encuentra autenticado o el token JWT no contiene un TenantId y UserId válidos.");
        }

        var userRole = _currentUserService.Role?.Trim() ?? string.Empty;

        foreach (var attribute in authorizeAttributes)
        {
            var isAuthorized = attribute.Roles.Any(requiredRole =>
                string.Equals(requiredRole, userRole, StringComparison.OrdinalIgnoreCase));

            if (!isAuthorized)
            {
                var allowedRolesList = string.Join(", ", attribute.Roles);
                throw new UnauthorizedAccessException(
                    $"Acceso denegado a '{typeof(TRequest).Name}'. Se requiere uno de los siguientes roles: [{allowedRolesList}]. Rol actual: '{userRole}'.");
            }
        }

        return await next();
    }
}

