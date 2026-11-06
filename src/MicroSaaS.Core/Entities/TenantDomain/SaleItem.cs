namespace MicroSaaS.Core.Entities.TenantDomain;

/// <summary>
/// Línea de detalle inmutable asociada a una venta (<see cref="Sale"/>).
/// </summary>
public class SaleItem
{
    public Guid Id { get; set; }
    public Guid SaleId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }

    // Propiedades tributarias DTE sincronizadas
    public int NumeroLinea { get; set; }
    public string NombreItem { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal MontoItem { get; set; }
    public bool EsExento { get; set; }

    public Sale? Sale { get; set; }

    public SaleItem()
    {
    }

    private SaleItem(
        Guid saleId,
        Guid productId,
        int quantity,
        decimal unitPrice,
        int numeroLinea,
        string? nombreItem)
    {
        if (saleId == Guid.Empty)
        {
            throw new ArgumentException("El identificador de la venta no puede ser vacío.", nameof(saleId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "La cantidad vendida debe ser mayor a cero.");
        }

        if (unitPrice < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "El precio unitario no puede ser negativo.");
        }

        var calculatedSubtotal = Math.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);

        Id = Guid.NewGuid();
        SaleId = saleId;
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        Subtotal = calculatedSubtotal;

        NumeroLinea = numeroLinea > 0 ? numeroLinea : 1;
        NombreItem = string.IsNullOrWhiteSpace(nombreItem) ? $"Producto {productId}" : nombreItem.Trim();
        Cantidad = quantity;
        PrecioUnitario = unitPrice;
        MontoItem = calculatedSubtotal;
        EsExento = false;
    }

    public static SaleItem Create(
        Guid saleId,
        Guid productId,
        int quantity,
        decimal unitPrice,
        int numeroLinea = 1,
        string? nombreItem = null)
    {
        return new SaleItem(
            saleId: saleId,
            productId: productId,
            quantity: quantity,
            unitPrice: unitPrice,
            numeroLinea: numeroLinea,
            nombreItem: nombreItem);
    }
}
