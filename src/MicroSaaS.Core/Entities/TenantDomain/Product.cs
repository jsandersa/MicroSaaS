namespace MicroSaaS.Core.Entities.TenantDomain;

/// <summary>
/// Representa un producto del inventario dentro del esquema privado de una Pyme (Tenant).
/// </summary>
public class Product
{
    public Guid Id { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public int Stock { get; private set; }
    public bool IsActive { get; private set; }

    // Constructor sin parámetros requerido por EF Core
    private Product() { }

    public Product(
        string sku,
        string name,
        decimal price,
        int stock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(price);
        ArgumentOutOfRangeException.ThrowIfNegative(stock);

        Id = Guid.NewGuid();
        Sku = sku.Trim().ToUpperInvariant();
        Name = name.Trim();
        Price = price;
        Stock = stock;
        IsActive = true;
    }

    public static Product Create(
        string sku,
        string name,
        decimal price,
        int stock) =>
        new(sku, name, price, stock);

    public void Update(
        string sku,
        string name,
        decimal price,
        int stock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(price);
        ArgumentOutOfRangeException.ThrowIfNegative(stock);

        Sku = sku.Trim().ToUpperInvariant();
        Name = name.Trim();
        Price = price;
        Stock = stock;
    }

    public void AdjustStock(int quantityDelta)
    {
        var newStock = Stock + quantityDelta;
        if (newStock < 0)
            throw new InvalidOperationException("El stock del producto no puede quedar en negativo.");

        Stock = newStock;
    }

    public void UpdatePrice(decimal newPrice)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(newPrice);
        Price = newPrice;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}

