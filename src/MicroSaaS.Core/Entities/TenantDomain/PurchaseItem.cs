namespace MicroSaaS.Core.Entities.TenantDomain;

/// <summary>
/// Línea de detalle asociada a una compra (<see cref="Purchase"/>).
/// </summary>
public class PurchaseItem
{
    public Guid Id { get; set; }
    public Guid PurchaseId { get; set; }
    public int NumeroLinea { get; set; }
    public string NombreItem { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal MontoItem { get; set; }
    public bool EsExento { get; set; }

    public Purchase? Purchase { get; set; }
}

