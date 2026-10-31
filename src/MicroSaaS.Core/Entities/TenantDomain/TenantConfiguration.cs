namespace MicroSaaS.Core.Entities.TenantDomain;

/// <summary>
/// Representa los parámetros de configuración operativos y tributarios dentro del esquema aislado de un Tenant.
/// </summary>
public class TenantConfiguration
{
    public Guid Id { get; private set; }
    public decimal TaxPercentage { get; private set; } = 19.00m;
    public string CurrencyCode { get; private set; } = "CLP";
    public string CurrencySymbol { get; private set; } = "$";
    public string TimeZoneId { get; private set; } = "America/Santiago";
    public string? ReceiptFooterText { get; private set; }

    // Constructor sin parámetros requerido por EF Core
    private TenantConfiguration() { }

    public TenantConfiguration(
        decimal taxPercentage = 19.00m,
        string currencyCode = "CLP",
        string currencySymbol = "$",
        string timeZoneId = "America/Santiago",
        string? receiptFooterText = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(taxPercentage);
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(currencySymbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        var normalizedCurrencyCode = currencyCode.Trim().ToUpperInvariant();
        if (normalizedCurrencyCode.Length > 3)
        {
            throw new ArgumentException("El código de moneda no puede exceder los 3 caracteres ISO.", nameof(currencyCode));
        }

        Id = Guid.NewGuid();
        TaxPercentage = taxPercentage;
        CurrencyCode = normalizedCurrencyCode;
        CurrencySymbol = currencySymbol.Trim();
        TimeZoneId = timeZoneId.Trim();
        ReceiptFooterText = string.IsNullOrWhiteSpace(receiptFooterText)
            ? null
            : receiptFooterText.Trim();
    }

    public static TenantConfiguration Create(
        decimal taxPercentage = 19.00m,
        string currencyCode = "CLP",
        string currencySymbol = "$",
        string timeZoneId = "America/Santiago",
        string? receiptFooterText = null) =>
        new(taxPercentage, currencyCode, currencySymbol, timeZoneId, receiptFooterText);

    public static TenantConfiguration CreateDefault(string? receiptFooterText = null) =>
        new(
            taxPercentage: 19.00m,
            currencyCode: "CLP",
            currencySymbol: "$",
            timeZoneId: "America/Santiago",
            receiptFooterText: receiptFooterText);

    public void Update(
        decimal taxPercentage,
        string currencyCode,
        string currencySymbol,
        string timeZoneId,
        string? receiptFooterText)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(taxPercentage);
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(currencySymbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        var normalizedCurrencyCode = currencyCode.Trim().ToUpperInvariant();
        if (normalizedCurrencyCode.Length > 3)
        {
            throw new ArgumentException("El código de moneda no puede exceder los 3 caracteres ISO.", nameof(currencyCode));
        }

        TaxPercentage = taxPercentage;
        CurrencyCode = normalizedCurrencyCode;
        CurrencySymbol = currencySymbol.Trim();
        TimeZoneId = timeZoneId.Trim();
        ReceiptFooterText = string.IsNullOrWhiteSpace(receiptFooterText)
            ? null
            : receiptFooterText.Trim();
    }
}

