using MicroSaaS.Core.Entities.TenantDomain;

namespace MicroSaaS.Core.Interfaces;

/// <summary>
/// Servicio de aplicación/infraestructura encargado de firmar digitalmente y emitir un <see cref="DteDocument"/>
/// ante el SII o proveedor externo de facturación electrónica (ej. SimpleAPI, Haulmer).
/// </summary>
public interface IDteEmissionService
{
    Task<string> SignAndEmitAsync(
        DteDocument document,
        CancellationToken cancellationToken = default);
}

