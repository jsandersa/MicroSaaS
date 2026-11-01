namespace MicroSaaS.Core.Entities.TenantDomain;

/// <summary>
/// Representa un producto del inventario dentro del esquema privado de un inquilino (Tenant).
/// </summary>
public class Product
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal SalePrice { get; set; }
    public decimal CostPrice { get; set; }
    public int CurrentStock { get; set; }
    public bool IsActive { get; set; } = true;

    public Product()
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
        CurrentStock = currentStock;
        IsActive = true;
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
        CurrentStock = currentStock;
    }

    public void AdjustStock(int quantityDelta)
    {
        var newStock = CurrentStock + quantityDelta;
        if (newStock < 0)
        {
            throw new InvalidOperationException("El stock del producto no puede quedar en negativo.");
        }

        CurrentStock = newStock;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
