using Microsoft.EntityFrameworkCore;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Core.Enums;
using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación con EF Core de <see cref="ICashRegisterRepository"/> sobre <see cref="TenantDbContext"/>.
/// </summary>
public sealed class CashRegisterRepository : ICashRegisterRepository
{
    private readonly TenantDbContext _context;

    public CashRegisterRepository(TenantDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<bool> HasOpenSessionTodayAsync(
        Guid tenantId,
        Guid userId,
        DateTime utcToday,
        CancellationToken cancellationToken = default)
    {
        var dayStartUtc = DateTime.SpecifyKind(utcToday.Date, DateTimeKind.Utc);
        var dayEndUtc = dayStartUtc.AddDays(1);

        return await _context.CashRegisterSessions
            .AsNoTracking()
            .AnyAsync(
                s => s.TenantId == tenantId
                     && s.UserId == userId
                     && s.Status == CashRegisterStatus.Open
                     && s.OpenedAt >= dayStartUtc
                     && s.OpenedAt < dayEndUtc,
                cancellationToken);
    }

    public async Task<CashRegisterSession?> GetOpenSessionByUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.CashRegisterSessions
            .FirstOrDefaultAsync(
                s => s.TenantId == tenantId
                     && s.UserId == userId
                     && s.Status == CashRegisterStatus.Open,
                cancellationToken);
    }

    public async Task<CashRegisterSession?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.CashRegisterSessions
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task AddAsync(
        CashRegisterSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        await _context.CashRegisterSessions.AddAsync(session, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        CashRegisterSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        _context.CashRegisterSessions.Update(session);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

