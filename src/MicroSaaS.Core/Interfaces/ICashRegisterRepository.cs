using MicroSaaS.Core.Entities.TenantDomain;

namespace MicroSaaS.Core.Interfaces;

/// <summary>
/// Contrato de repositorio para gestionar la persistencia transaccional de sesiones de caja (<see cref="CashRegisterSession"/>).
/// </summary>
public interface ICashRegisterRepository
{
    Task<bool> HasOpenSessionTodayAsync(
        Guid tenantId,
        Guid userId,
        DateTime utcToday,
        CancellationToken cancellationToken = default);

    Task<CashRegisterSession?> GetOpenSessionByUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<CashRegisterSession?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        CashRegisterSession session,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        CashRegisterSession session,
        CancellationToken cancellationToken = default);
}

