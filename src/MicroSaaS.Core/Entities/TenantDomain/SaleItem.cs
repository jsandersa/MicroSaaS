namespace MicroSaaS.Core.Entities.TenantDomain;

/// <summary>
/// Línea de detalle asociada a una venta (<see cref="Sale"/>).
/// </summary>
public class SaleItem
{
    public Guid Id { get; set; }
    public Guid SaleId { get; set; }
    public int NumeroLinea { get; set; }
    public string NombreItem { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal MontoItem { get; set; }
    public bool EsExento { get; set; }

    public Sale? Sale { get; set; }
}

