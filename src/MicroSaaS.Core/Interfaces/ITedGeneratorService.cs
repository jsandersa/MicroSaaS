using System.Xml.Linq;
using MicroSaaS.Core.Entities.TenantDomain.Dte;

namespace MicroSaaS.Core.Interfaces;

/// <summary>
/// Contrato para el servicio generador del Timbre Electrónico de Datos (<c>&lt;TED&gt;</c>) del SII.
/// </summary>
public interface ITedGeneratorService
{
    XElement GenerateTed(DteDocument document, CafDocument caf);
}

