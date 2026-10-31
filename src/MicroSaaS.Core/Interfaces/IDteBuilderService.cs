using MicroSaaS.Core.Entities.TenantDomain.Dte;

namespace MicroSaaS.Core.Interfaces;

/// <summary>
/// Contrato para el servicio constructor de la estructura XML de Documentos Tributarios Electrónicos (DTE) del SII.
/// </summary>
public interface IDteBuilderService
{
    string BuildDteXml(DteDocument document, CafDocument caf);
}

