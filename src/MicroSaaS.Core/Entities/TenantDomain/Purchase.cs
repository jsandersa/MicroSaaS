using System.ComponentModel.DataAnnotations.Schema;
using MediatR;
using MicroSaaS.Core.Events;

namespace MicroSaaS.Core.Entities.TenantDomain;

/// <summary>
/// Entidad de dominio que representa un documento de compra recibido por el Tenant.
/// </summary>
public class Purchase : IHasDomainEvents
{
    private readonly List<INotification> _domainEvents = new();

    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public int Folio { get; set; }
    public int TipoDte { get; set; }
    public DateTime FechaEmision { get; set; }
    public string RutProveedor { get; set; } = string.Empty;
    public string RazonSocialProveedor { get; set; } = string.Empty;
    public decimal MontoNeto { get; set; }
    public decimal Iva { get; set; }
    public decimal MontoTotal { get; set; }

    public ICollection<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();

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

    public void AddItem(PurchaseItem item, string? sku = null)
    {
        ArgumentNullException.ThrowIfNull(item);

        Items.Add(item);

        var resolvedSku = !string.IsNullOrWhiteSpace(sku)
            ? sku.Trim()
            : item.NombreItem.Trim();

        var quantity = (int)Math.Round(item.Cantidad, 0, MidpointRounding.AwayFromZero);

        AddDomainEvent(new ProductPurchasedDomainEvent(
            TenantId: TenantId.ToString(),
            Sku: resolvedSku,
            Quantity: quantity,
            UnitCost: item.PrecioUnitario));
    }
}
