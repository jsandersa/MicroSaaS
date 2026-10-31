namespace MicroSaaS.Core.Entities.TenantDomain;

/// <summary>
/// Entidad de dominio que representa un documento de compra recibido por el Tenant.
/// </summary>
public class Purchase
{
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
}

