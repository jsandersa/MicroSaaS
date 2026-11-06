using MicroSaaS.Core.Enums;

namespace MicroSaaS.Core.Entities.TenantDomain;

/// <summary>
/// Entidad de dominio inmutable que registra cada movimiento físico y valorizado en el libro Kardex del Tenant.
/// </summary>
public sealed class KardexTransaction
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProductId { get; private set; }
    public MovementType MovementType { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal TotalValue { get; private set; }
    public DateTime TransactionDate { get; private set; }
    public string? Reason { get; private set; }

    // Constructor privado requerido por Entity Framework Core
    private KardexTransaction()
    {
    }

    public KardexTransaction(
        Guid tenantId,
        Guid productId,
        MovementType movementType,
        int quantity,
        decimal unitCost,
        DateTime? transactionDateUtc = null,
        string? reason = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("El identificador del Tenant no puede ser vacío.", nameof(tenantId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("El identificador del producto no puede ser vacío.", nameof(productId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "La cantidad del movimiento Kardex debe ser mayor a cero.");
        }

        if (unitCost < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unitCost),
                "El costo unitario del movimiento Kardex no puede ser negativo.");
        }

        Id = Guid.NewGuid();
        TenantId = tenantId;
        ProductId = productId;
        MovementType = movementType;
        Quantity = quantity;
        UnitCost = Math.Round(unitCost, 2, MidpointRounding.AwayFromZero);
        TotalValue = Math.Round(quantity * UnitCost, 2, MidpointRounding.AwayFromZero);
        TransactionDate = transactionDateUtc ?? DateTime.UtcNow;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    public static KardexTransaction Create(
        Guid tenantId,
        Guid productId,
        MovementType movementType,
        int quantity,
        decimal unitCost,
        string? reason = null)
    {
        return new KardexTransaction(
            tenantId: tenantId,
            productId: productId,
            movementType: movementType,
            quantity: quantity,
            unitCost: unitCost,
            transactionDateUtc: DateTime.UtcNow,
            reason: reason);
    }
}

