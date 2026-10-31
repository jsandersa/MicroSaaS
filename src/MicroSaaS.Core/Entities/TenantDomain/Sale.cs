namespace MicroSaaS.Core.Entities.TenantDomain;

/// <summary>
/// Entidad de dominio que representa un documento de venta emitido por el Tenant (ej. Factura Electrónica Tipo 33).
/// </summary>
public class Sale
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public int Folio { get; set; }
    public int TipoDte { get; set; }
    public DateTime FechaEmision { get; set; }
    public string RutCliente { get; set; } = string.Empty;
    public string RazonSocialCliente { get; set; } = string.Empty;
    public decimal MontoNeto { get; set; }
    public decimal Iva { get; set; }
    public decimal MontoTotal { get; set; }

    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
}

