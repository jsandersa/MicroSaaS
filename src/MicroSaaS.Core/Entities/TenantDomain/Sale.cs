using System.ComponentModel.DataAnnotations.Schema;
using MediatR;
using MicroSaaS.Core.Events;

namespace MicroSaaS.Core.Entities.TenantDomain;

public enum PaymentMethod
{
    Cash,
    Card,
    Mixed
}

/// <summary>
/// Entidad de dominio que representa un documento de venta emitido por el Tenant (ej. Factura Electrónica Tipo 33).
/// </summary>
public class Sale : IHasDomainEvents
{
    private readonly List<INotification> _domainEvents = new();

    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public int Folio { get; set; }
    public int TipoDte { get; set; }
    public DateTime FechaEmision { get; set; }

    // RUT y Razón Social ahora son opcionales (nullable) para ventas rápidas
    public string? RutCliente { get; set; }
    public string? RazonSocialCliente { get; set; }

    public decimal MontoNeto { get; set; }
    public decimal Iva { get; set; }
    public decimal MontoTotal { get; set; }

    // Campos de pago e integración con Mercado Pago
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public decimal CashAmount { get; set; }
    public decimal CardAmount { get; set; }

    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();

    [NotMapped]
    public IReadOnlyCollection<INotification> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(INotification domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    public void AddItem(SaleItem item, string? sku = null)
    {
        ArgumentNullException.ThrowIfNull(item);

        Items.Add(item);

        var resolvedSku = !string.IsNullOrWhiteSpace(sku)
            ? sku.Trim()
            : item.NombreItem.Trim();

        var quantity = (int)Math.Round(item.Cantidad, 0, MidpointRounding.AwayFromZero);

        AddDomainEvent(new ProductSoldDomainEvent(
            TenantId: TenantId.ToString(),
            Sku: resolvedSku,
            Quantity: quantity));
    }
}