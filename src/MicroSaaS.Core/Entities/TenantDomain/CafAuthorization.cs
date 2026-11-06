namespace MicroSaaS.Core.Entities.TenantDomain;

/// <summary>
/// Representa un Código de Autorización de Folios (CAF) otorgado por el SII para un tipo de DTE específico.
/// </summary>
public class CafAuthorization
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public int DteType { get; private set; }
    public int StartFolio { get; private set; }
    public int EndFolio { get; private set; }
    public int CurrentFolio { get; private set; }
    public string XmlContent { get; private set; } = string.Empty;
    public DateTime AuthorizedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public bool IsActive { get; private set; }

    private CafAuthorization() { }

    public CafAuthorization(
        Guid tenantId,
        int dteType,
        int startFolio,
        int endFolio,
        int currentFolio,
        string xmlContent,
        DateTime authorizedAt,
        DateTime expiresAt)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El TenantId no puede ser vacío.", nameof(tenantId));

        if (startFolio <= 0 || endFolio < startFolio)
            throw new ArgumentException("El rango de folios CAF (Desde / Hasta) no es válido.");

        Id = Guid.NewGuid();
        TenantId = tenantId;
        DteType = dteType;
        StartFolio = startFolio;
        EndFolio = endFolio;
        CurrentFolio = Math.Max(startFolio - 1, currentFolio);
        XmlContent = string.IsNullOrWhiteSpace(xmlContent)
            ? GenerateStandardCafXml(dteType, startFolio, endFolio, authorizedAt)
            : xmlContent.Trim();
        AuthorizedAt = authorizedAt.Kind == DateTimeKind.Utc ? authorizedAt : authorizedAt.ToUniversalTime();
        ExpiresAt = expiresAt.Kind == DateTimeKind.Utc ? expiresAt : expiresAt.ToUniversalTime();
        IsActive = true;
    }

    public void UpdateCurrentFolio(int lastUsedFolio)
    {
        if (lastUsedFolio >= StartFolio - 1)
        {
            CurrentFolio = Math.Min(lastUsedFolio, EndFolio);
        }
    }

    public void Deactivate() => IsActive = false;

    private static string GenerateStandardCafXml(int dteType, int startFolio, int endFolio, DateTime authorizedAt)
    {
        var fecha = authorizedAt.ToString("yyyy-MM-dd");
        return $"""
            <CAF version="1.0">
              <DA>
                <TD>{dteType}</TD>
                <RNG>
                  <D>{startFolio}</D>
                  <H>{endFolio}</H>
                </RNG>
                <FA>{fecha}</FA>
              </DA>
            </CAF>
            """;
    }
}

