namespace MicroSaaS.Core.Entities.TenantDomain;

/// <summary>
/// Representa un producto del inventario dentro del esquema privado de un inquilino (Tenant),
/// con valorización dinámica de costo mediante Precio Medio Ponderado (PMP).
/// </summary>
public class Product
{
    public Guid Id { get; private set; }
    public string TenantId { get; private set; } = string.Empty;
    public string Sku { get; private set; } = string.Empty;
    public string SKU => Sku;
    public string Name { get; private set; } = string.Empty;
    public decimal SalePrice { get; private set; }
    public decimal CostPrice { get; private set; }
    public decimal PmpCost { get; private set; }
    public int CurrentStock { get; private set; }
    public bool IsActive { get; private set; } = true;

    // Constructor privado requerido por Entity Framework Core
    private Product()
    {
    }

    public Product(
        string tenantId,
        string sku,
        string name,
        decimal salePrice,
        decimal costPrice,
        int currentStock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(salePrice);
        ArgumentOutOfRangeException.ThrowIfNegative(costPrice);
        ArgumentOutOfRangeException.ThrowIfNegative(currentStock);

        Id = Guid.NewGuid();
        TenantId = tenantId?.Trim() ?? string.Empty;
        Sku = sku.Trim().ToUpperInvariant();
        Name = name.Trim();
        SalePrice = salePrice;
        CostPrice = costPrice;
        PmpCost = costPrice;
        CurrentStock = currentStock;
        IsActive = true;
    }

    public static Product Create(
        Guid tenantId,
        string sku,
        string name,
        decimal salePrice,
        int initialStock,
        decimal initialUnitCost)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("El identificador del Tenant no puede ser vacío.", nameof(tenantId));
        }

        return new Product(
            tenantId: tenantId.ToString(),
            sku: sku,
            name: name,
            salePrice: salePrice,
            costPrice: initialUnitCost,
            currentStock: initialStock);
    }

    /// <summary>
    /// Registra una entrada de inventario (compra o ingreso), recalcula dinámicamente el Precio Medio Ponderado (PMP)
    /// y actualiza el precio de venta aplicando el porcentaje de margen comercial parametrizado del Tenant.
    /// </summary>
    public void RegisterStockIn(int quantity, decimal unitCost, decimal marginPercentage = 30.00m)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "La cantidad de entrada de stock debe ser mayor a cero.");
        }

        if (unitCost < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unitCost),
                "El costo unitario de entrada no puede ser negativo.");
        }

        var effectiveMargin = marginPercentage >= 0m ? marginPercentage : 30.00m;

        var currentTotalValue = CurrentStock * PmpCost;
        var incomingTotalValue = quantity * unitCost;
        var newStock = CurrentStock + quantity;

        var recalculatedPmp = newStock > 0
            ? Math.Round((currentTotalValue + incomingTotalValue) / newStock, 2, MidpointRounding.AwayFromZero)
            : unitCost;

        CurrentStock = newStock;
        PmpCost = recalculatedPmp;
        CostPrice = recalculatedPmp;

        if (recalculatedPmp > 0m)
        {
            var suggestedSalePrice = Math.Round(
                recalculatedPmp * (1m + (effectiveMargin / 100m)),
                0,
                MidpointRounding.AwayFromZero);
            SalePrice = suggestedSalePrice;
        }
    }

    /// <summary>
    /// Registra una salida de inventario (venta o merma) validando que el stock resultante no sea negativo.
    /// </summary>
    public void RegisterStockOut(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "La cantidad de salida de stock debe ser mayor a cero.");
        }

        if (CurrentStock - quantity < 0)
        {
            throw new InvalidOperationException(
                $"Stock insuficiente para el producto '{Sku}' ('{Name}'). Stock disponible: {CurrentStock}, solicitado: {quantity}.");
        }

        CurrentStock -= quantity;
    }

    public void Update(
        string sku,
        string name,
        decimal salePrice,
        decimal costPrice,
        int currentStock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(salePrice);
        ArgumentOutOfRangeException.ThrowIfNegative(costPrice);
        ArgumentOutOfRangeException.ThrowIfNegative(currentStock);

        Sku = sku.Trim().ToUpperInvariant();
        Name = name.Trim();
        SalePrice = salePrice;
        CostPrice = costPrice;
        PmpCost = costPrice;
        CurrentStock = currentStock;
    }

    public void AdjustStock(int quantityDelta)
    {
        if (quantityDelta > 0)
        {
            RegisterStockIn(quantityDelta, PmpCost > 0m ? PmpCost : CostPrice);
            return;
        }

        if (quantityDelta < 0)
        {
            RegisterStockOut(Math.Abs(quantityDelta));
        }
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
