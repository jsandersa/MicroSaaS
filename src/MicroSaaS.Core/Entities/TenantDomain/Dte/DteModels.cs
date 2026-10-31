namespace MicroSaaS.Core.Entities.TenantDomain.Dte;

/// <summary>
/// Representa la estructura raíz de un Documento Tributario Electrónico (DTE) del SII (ej. Factura Electrónica Tipo 33).
/// </summary>
public record DteDocument(
    DocumentHeader Header,
    IReadOnlyList<DocumentItem> Items);

/// <summary>
/// Representa el encabezado (<c>&lt;Encabezado&gt;</c>) del DTE, incluyendo identificación del documento, emisor, receptor y totales.
/// </summary>
public record DocumentHeader(
    int TipoDte,
    int Folio,
    DateTime FechaEmision,
    Issuer Emisor,
    Receiver Receptor,
    decimal MontoNeto,
    decimal TasaIva,
    decimal Iva,
    decimal MontoTotal,
    decimal MontoExento = 0m);

/// <summary>
/// Representa los datos tributarios del contribuyente emisor (<c>&lt;Emisor&gt;</c>).
/// </summary>
public record Issuer(
    string RutEmisor,
    string RazonSocial,
    string GiroEmisor,
    int Acteco,
    string DireccionOrigen,
    string ComunaOrigen,
    string CiudadOrigen);

/// <summary>
/// Representa los datos tributarios del cliente receptor (<c>&lt;Receptor&gt;</c>).
/// </summary>
public record Receiver(
    string RutReceptor,
    string RazonSocialReceptor,
    string GiroReceptor,
    string DireccionReceptor,
    string ComunaReceptor,
    string CiudadReceptor);

/// <summary>
/// Representa una línea de detalle de producto o servicio (<c>&lt;Detalle&gt;</c>) dentro del DTE.
/// </summary>
public record DocumentItem(
    int NumeroLinea,
    string NombreItem,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal MontoItem,
    string? CodigoItem = null,
    string? DescripcionItem = null,
    string? UnidadMedida = null,
    bool EsExento = false);

