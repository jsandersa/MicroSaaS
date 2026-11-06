using System.ComponentModel.DataAnnotations.Schema;
using MediatR;
using MicroSaaS.Core.Events;

namespace MicroSaaS.Core.Entities.TenantDomain;

public enum PaymentMethod
{
    Cash = 0,
    Card = 1,
    Mixed = 2
}

/// <summary>
/// Entidad de dominio inmutable que representa una venta en Punto de Venta (POS) vinculada a una sesión de caja abierta.
/// </summary>
public class Sale : IHasDomainEvents
{
    private readonly List<INotification> _domainEvents = new();

    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid CashRegisterSessionId { get; set; }
    public decimal TotalAmount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public DateTime CreatedAt { get; set; }

    // Propiedades tributarias y de desglose POS compatibles con consultas DTE y Dashboard
    public int Folio { get; set; }
    public int TipoDte { get; set; } = 39;
    public DateTime FechaEmision { get; set; }
    public string? RutCliente { get; set; }
    public string? RazonSocialCliente { get; set; }
    public decimal MontoNeto { get; set; }
    public decimal Iva { get; set; }
    public decimal MontoTotal { get; set; }
    public decimal CashAmount { get; set; }
    public decimal CardAmount { get; set; }

    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();

    [NotMapped]
    public IReadOnlyCollection<INotification> DomainEvents => _domainEvents.AsReadOnly();

    public Sale()
    {
    }

    private Sale(
        Guid tenantId,
        Guid userId,
        Guid cashRegisterSessionId,
        PaymentMethod paymentMethod,
        DateTime createdAtUtc)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("El identificador del Tenant no puede ser vacío.", nameof(tenantId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("El identificador del usuario cajero no puede ser vacío.", nameof(userId));
        }

        if (cashRegisterSessionId == Guid.Empty)
        {
            throw new ArgumentException("La venta debe asociarse a una sesión de caja abierta válida.", nameof(cashRegisterSessionId));
        }

        Id = Guid.NewGuid();
        TenantId = tenantId;
        UserId = userId;
        CashRegisterSessionId = cashRegisterSessionId;
        PaymentMethod = paymentMethod;
        CreatedAt = createdAtUtc;
        FechaEmision = createdAtUtc;
        TipoDte = 39;
    }

    public static Sale Create(
        Guid tenantId,
        Guid userId,
        Guid cashRegisterSessionId,
        PaymentMethod paymentMethod)
    {
        return new Sale(
            tenantId: tenantId,
            userId: userId,
            cashRegisterSessionId: cashRegisterSessionId,
            paymentMethod: paymentMethod,
            createdAtUtc: DateTime.UtcNow);
    }

    public SaleItem AddSaleItem(
        Guid productId,
        int quantity,
        decimal unitPrice,
        string? nombreItem = null)
    {
        var lineNumber = Items.Count + 1;
        var item = SaleItem.Create(
            saleId: Id,
            productId: productId,
            quantity: quantity,
            unitPrice: unitPrice,
            numeroLinea: lineNumber,
            nombreItem: nombreItem);

        Items.Add(item);
        RecalculateTotals();
        return item;
    }

    public void AddItem(SaleItem item, string? sku = null)
    {
        ArgumentNullException.ThrowIfNull(item);

        Items.Add(item);
        RecalculateTotals();

        var resolvedSku = !string.IsNullOrWhiteSpace(sku)
            ? sku.Trim()
            : item.NombreItem.Trim();

        if (!string.IsNullOrWhiteSpace(resolvedSku))
        {
            var quantity = item.Quantity > 0
                ? item.Quantity
                : (int)Math.Round(item.Cantidad, 0, MidpointRounding.AwayFromZero);

            AddDomainEvent(new ProductSoldDomainEvent(
                TenantId: TenantId.ToString(),
                Sku: resolvedSku,
                Quantity: quantity));
        }
    }

    public void RecalculateTotals()
    {
        var netSum = Items.Sum(i => i.Subtotal > 0m ? i.Subtotal : i.MontoItem);
        MontoNeto = Math.Round(netSum, 2, MidpointRounding.AwayFromZero);
        Iva = Math.Round(MontoNeto * 0.19m, 2, MidpointRounding.AwayFromZero);
        TotalAmount = MontoNeto + Iva;
        MontoTotal = TotalAmount;
    }

    public void AddDomainEvent(INotification domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
