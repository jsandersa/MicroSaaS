using MicroSaaS.Core.Enums;

namespace MicroSaaS.Core.Entities.TenantDomain;

/// <summary>
/// Entidad de dominio inmutable que representa un Documento Tributario Electrónico (DTE) emitido por el Tenant.
/// </summary>
public sealed class DteDocument
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SaleId { get; private set; }
    public DteType DteType { get; private set; }
    public int Folio { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string XmlContent { get; private set; } = string.Empty;
    public DteStatus Status { get; private set; }
    public DateTime EmissionDate { get; private set; }

    // Constructor privado requerido por Entity Framework Core
    private DteDocument()
    {
    }

    private DteDocument(
        Guid tenantId,
        Guid saleId,
        DteType dteType,
        int folio,
        decimal totalAmount,
        DateTime emissionDateUtc)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("El identificador del Tenant no puede ser vacío.", nameof(tenantId));
        }

        if (saleId == Guid.Empty)
        {
            throw new ArgumentException("El identificador de la venta asociada no puede ser vacío.", nameof(saleId));
        }

        if (folio <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(folio),
                "El folio correlativo del DTE debe ser mayor a cero.");
        }

        if (totalAmount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalAmount),
                "El monto total del DTE no puede ser negativo.");
        }

        Id = Guid.NewGuid();
        TenantId = tenantId;
        SaleId = saleId;
        DteType = dteType;
        Folio = folio;
        TotalAmount = totalAmount;
        XmlContent = string.Empty;
        Status = DteStatus.Pending;
        EmissionDate = emissionDateUtc;
    }

    public static DteDocument Create(
        Guid tenantId,
        Guid saleId,
        DteType dteType,
        int folio,
        decimal totalAmount)
    {
        return new DteDocument(
            tenantId: tenantId,
            saleId: saleId,
            dteType: dteType,
            folio: folio,
            totalAmount: totalAmount,
            emissionDateUtc: DateTime.UtcNow);
    }

    public void MarkAsSigned(string signedXmlContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signedXmlContent);

        if (Status != DteStatus.Pending)
        {
            throw new InvalidOperationException(
                $"No se puede firmar el DTE '{Id}' porque su estado actual es '{Status}'.");
        }

        XmlContent = signedXmlContent.Trim();
        Status = DteStatus.Signed;
    }

    public void MarkAsSentToSii()
    {
        if (Status != DteStatus.Signed)
        {
            throw new InvalidOperationException(
                $"El DTE '{Id}' debe estar firmado antes de enviarse al SII. Estado actual: '{Status}'.");
        }

        Status = DteStatus.SentToSii;
    }

    public void MarkAsAccepted()
    {
        if (Status != DteStatus.Signed && Status != DteStatus.SentToSii)
        {
            throw new InvalidOperationException(
                $"No se puede marcar como aceptado un DTE en estado '{Status}'.");
        }

        Status = DteStatus.Accepted;
    }

    public void MarkAsRejected()
    {
        if (Status == DteStatus.Accepted)
        {
            throw new InvalidOperationException(
                $"No se puede rechazar un DTE que ya fue aceptado por el SII ('{Id}').");
        }

        Status = DteStatus.Rejected;
    }

    public void MarkAsAnnulledByCreditNote()
    {
        Status = DteStatus.Rejected;
    }
}

