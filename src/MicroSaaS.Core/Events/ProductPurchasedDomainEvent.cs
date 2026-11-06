using MediatR;

namespace MicroSaaS.Core.Events;

/// <summary>
/// Evento de dominio disparado cuando se adquiere un producto en una operación de compra (<c>Purchase</c>).
/// </summary>
public record ProductPurchasedDomainEvent(
    string TenantId,
    string Sku,
    int Quantity,
    decimal UnitCost = 0m) : INotification;
