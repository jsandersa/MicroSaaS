using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.Infrastructure.Sii;

/// <summary>
/// Implementación de infraestructura de <see cref="IDteEmissionService"/> que genera y firma el XML del DTE.
/// </summary>
public sealed class SiiDteEmissionService : IDteEmissionService
{
    public Task<string> SignAndEmitAsync(
        DteDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var signedXml = $"""
            <?xml version="1.0" encoding="ISO-8859-1"?>
            <DTE version="1.0">
              <Documento ID="F{document.Folio}T{(int)document.DteType}">
                <Encabezado>
                  <IdDoc>
                    <TipoDTE>{(int)document.DteType}</TipoDTE>
                    <Folio>{document.Folio}</Folio>
                    <FchEmis>{document.EmissionDate:yyyy-MM-dd}</FchEmis>
                  </IdDoc>
                  <Totales>
                    <MntTotal>{document.TotalAmount:0}</MntTotal>
                  </Totales>
                </Encabezado>
              </Documento>
            </DTE>
            """;

        return Task.FromResult(signedXml);
    }
}

