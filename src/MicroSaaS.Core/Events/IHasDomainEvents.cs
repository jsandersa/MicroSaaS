using MediatR;

namespace MicroSaaS.Core.Events;

/// <summary>
/// Contrato para entidades de dominio que registran eventos de dominio (<see cref="INotification"/>).
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<INotification> DomainEvents { get; }
    void AddDomainEvent(INotification domainEvent);
    void ClearDomainEvents();
}

