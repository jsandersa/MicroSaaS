using MediatR;

namespace MicroSaaS.Core.Events;

/// <summary>
/// Evento de dominio disparado cuando se vende un producto en una operación comercial (<c>Sale</c>).
/// </summary>
public record ProductSoldDomainEvent(
    string TenantId,
    string Sku,
    int Quantity) : INotification;

