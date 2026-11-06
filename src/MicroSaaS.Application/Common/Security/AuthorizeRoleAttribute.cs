namespace MicroSaaS.Application.Common.Security;

/// <summary>
/// Atributo declarativo para restringir la ejecución de Commands/Queries de MediatR
/// únicamente a usuarios autenticados que posean al menos uno de los roles permitidos.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class AuthorizeRoleAttribute : Attribute
{
    public IReadOnlyList<string> Roles { get; }

    public AuthorizeRoleAttribute(params string[] roles)
    {
        if (roles is null || roles.Length == 0)
        {
            throw new ArgumentException(
                "Se debe especificar al menos un rol para el atributo AuthorizeRole.",
                nameof(roles));
        }

        Roles = roles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .ToArray();
    }
}

