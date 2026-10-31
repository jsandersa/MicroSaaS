using System.Globalization;
using System.Xml.Linq;
using MicroSaaS.Core.Entities.TenantDomain.Dte;
using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.Infrastructure.Sii;

/// <summary>
/// Constructor de XML para Documentos Tributarios Electrónicos (DTE) del SII (Factura Electrónica Tipo 33, etc.)
/// utilizando LINQ to XML y respetando el namespace oficial <c>http://www.sii.cl/SiiDte</c>.
/// </summary>
public class SiiDteBuilderService : IDteBuilderService
{
    public const string SiiDteNamespaceUri = "http://www.sii.cl/SiiDte";
    private static readonly XNamespace SiiNs = SiiDteNamespaceUri;
    private readonly ITedGeneratorService _tedGeneratorService;

    public SiiDteBuilderService(ITedGeneratorService tedGeneratorService)
    {
        _tedGeneratorService = tedGeneratorService;
    }

    public string BuildDteXml(DteDocument document, CafDocument caf)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Header);
        ArgumentNullException.ThrowIfNull(document.Header.Emisor);
        ArgumentNullException.ThrowIfNull(document.Header.Receptor);
        ArgumentNullException.ThrowIfNull(document.Items);
        ArgumentNullException.ThrowIfNull(caf);

        if (document.Items.Count == 0)
        {
            throw new ArgumentException("El DTE debe contener al menos una línea de detalle (<Detalle>).", nameof(document));
        }

        var header = document.Header;
        var documentId = $"F{header.Folio}T{header.TipoDte}";

        // 1. Construir nodo <Encabezado> (<IdDoc>, <Emisor>, <Receptor>, <Totales>)
        var encabezadoElement = new XElement(SiiNs + "Encabezado",
            new XElement(SiiNs + "IdDoc",
                new XElement(SiiNs + "TipoDTE", header.TipoDte),
                new XElement(SiiNs + "Folio", header.Folio),
                new XElement(SiiNs + "FchEmis", header.FechaEmision.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            ),
            new XElement(SiiNs + "Emisor",
                new XElement(SiiNs + "RUTEmisor", NormalizeRut(header.Emisor.RutEmisor)),
                new XElement(SiiNs + "RznSoc", header.Emisor.RazonSocial.Trim()),
                new XElement(SiiNs + "GiroEmis", header.Emisor.GiroEmisor.Trim()),
                new XElement(SiiNs + "Acteco", header.Emisor.Acteco),
                new XElement(SiiNs + "DirOrigen", header.Emisor.DireccionOrigen.Trim()),
                new XElement(SiiNs + "CmnaOrigen", header.Emisor.ComunaOrigen.Trim()),
                new XElement(SiiNs + "CiudadOrigen", header.Emisor.CiudadOrigen.Trim())
            ),
            new XElement(SiiNs + "Receptor",
                new XElement(SiiNs + "RUTRecep", NormalizeRut(header.Receptor.RutReceptor)),
                new XElement(SiiNs + "RznSocRecep", header.Receptor.RazonSocialReceptor.Trim()),
                new XElement(SiiNs + "GiroRecep", header.Receptor.GiroReceptor.Trim()),
                new XElement(SiiNs + "DirRecep", header.Receptor.DireccionReceptor.Trim()),
                new XElement(SiiNs + "CmnaRecep", header.Receptor.ComunaReceptor.Trim()),
                new XElement(SiiNs + "CiudadRecep", header.Receptor.CiudadReceptor.Trim())
            ),
            BuildTotalesElement(header)
        );

        // 2. Construir lista de nodos <Detalle>
        var detalleElements = document.Items
            .OrderBy(i => i.NumeroLinea)
            .Select(BuildDetalleElement);

        // 3. Generar el Timbre Electrónico de Datos (<TED>) firmado con la clave RSA del CAF
        var tedElement = _tedGeneratorService.GenerateTed(document, caf);

        // 4. Construir nodo <Documento ID="F{Folio}T{TipoDTE}"> incrustando <TED> después de <Detalle> y antes de <TmstFirma>
        var documentoElement = new XElement(SiiNs + "Documento",
            new XAttribute("ID", documentId),
            encabezadoElement,
            detalleElements,
            tedElement,
            new XElement(SiiNs + "TmstFirma", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture))
        );

        // 4. Construir nodo raíz <DTE version="1.0" xmlns="http://www.sii.cl/SiiDte">
        var dteRoot = new XElement(SiiNs + "DTE",
            new XAttribute("version", "1.0"),
            documentoElement
        );

        var xDocument = new XDocument(
            new XDeclaration("1.0", "ISO-8859-1", null),
            dteRoot);

        return xDocument.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement BuildTotalesElement(DocumentHeader header)
    {
        var totales = new XElement(SiiNs + "Totales",
            new XElement(SiiNs + "MntNeto", Math.Round(header.MontoNeto, 0, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)));

        if (header.MontoExento > 0)
        {
            totales.Add(new XElement(SiiNs + "MntExe", Math.Round(header.MontoExento, 0, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)));
        }

        totales.Add(
            new XElement(SiiNs + "TasaIVA", header.TasaIva.ToString("0.##", CultureInfo.InvariantCulture)),
            new XElement(SiiNs + "IVA", Math.Round(header.Iva, 0, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)),
            new XElement(SiiNs + "MntTotal", Math.Round(header.MontoTotal, 0, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture))
        );

        return totales;
    }

    private static XElement BuildDetalleElement(DocumentItem item)
    {
        var detalle = new XElement(SiiNs + "Detalle",
            new XElement(SiiNs + "NroLinDet", item.NumeroLinea));

        if (!string.IsNullOrWhiteSpace(item.CodigoItem))
        {
            detalle.Add(new XElement(SiiNs + "CdgItem",
                new XElement(SiiNs + "TpoCodigo", "INT1"),
                new XElement(SiiNs + "VlrCodigo", item.CodigoItem.Trim())));
        }

        if (item.EsExento)
        {
            detalle.Add(new XElement(SiiNs + "IndExe", 1));
        }

        detalle.Add(new XElement(SiiNs + "NmbItem", item.NombreItem.Trim()));

        if (!string.IsNullOrWhiteSpace(item.DescripcionItem))
        {
            detalle.Add(new XElement(SiiNs + "DscItem", item.DescripcionItem.Trim()));
        }

        detalle.Add(new XElement(SiiNs + "QtyItem", item.Cantidad.ToString("0.######", CultureInfo.InvariantCulture)));

        if (!string.IsNullOrWhiteSpace(item.UnidadMedida))
        {
            detalle.Add(new XElement(SiiNs + "UnmdItem", item.UnidadMedida.Trim()));
        }

        detalle.Add(
            new XElement(SiiNs + "PrcItem", item.PrecioUnitario.ToString("0.######", CultureInfo.InvariantCulture)),
            new XElement(SiiNs + "MontoItem", Math.Round(item.MontoItem, 0, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture))
        );

        return detalle;
    }

    private static string NormalizeRut(string rut)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rut);
        return rut.Replace(".", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant();
    }
}

