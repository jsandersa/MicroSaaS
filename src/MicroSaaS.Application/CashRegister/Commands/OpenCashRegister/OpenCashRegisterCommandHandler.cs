using MediatR;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.Application.CashRegister.Commands.OpenCashRegister;

/// <summary>
/// Manejador de <see cref="OpenCashRegisterCommand"/> encargado de verificar que el usuario actual
/// no posea una sesión de caja abierta en el día en curso, instanciar la apertura inmutable y persistirla.
/// </summary>
public sealed class OpenCashRegisterCommandHandler : IRequestHandler<OpenCashRegisterCommand, Guid>
{
    private readonly ICashRegisterRepository _cashRegisterRepository;
    private readonly ICurrentUserService _currentUserService;

    public OpenCashRegisterCommandHandler(
        ICashRegisterRepository cashRegisterRepository,
        ICurrentUserService currentUserService)
    {
        _cashRegisterRepository = cashRegisterRepository ?? throw new ArgumentNullException(nameof(cashRegisterRepository));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<Guid> Handle(
        OpenCashRegisterCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenantId = _currentUserService.TenantId;
        var userId = _currentUserService.UserId;

        if (tenantId == Guid.Empty || userId == Guid.Empty)
        {
            throw new UnauthorizedAccessException(
                "No se pudo determinar el TenantId o el UserId del operador desde el contexto de sesión actual.");
        }

        var utcToday = DateTime.UtcNow.Date;

        var hasOpenSessionToday = await _cashRegisterRepository.HasOpenSessionTodayAsync(
            tenantId: tenantId,
            userId: userId,
            utcToday: utcToday,
            cancellationToken: cancellationToken);

        if (hasOpenSessionToday)
        {
            throw new InvalidOperationException(
                "El usuario ya cuenta con una sesión de caja abierta en el día de hoy. Debe cerrarla antes de iniciar una nueva apertura.");
        }

        var session = CashRegisterSession.Open(
            tenantId: tenantId,
            userId: userId,
            openingAmount: request.OpeningAmount);

        await _cashRegisterRepository.AddAsync(session, cancellationToken);

        return session.Id;
    }
}

