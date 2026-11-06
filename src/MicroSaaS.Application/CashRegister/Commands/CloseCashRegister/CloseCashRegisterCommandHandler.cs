using MediatR;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.Application.CashRegister.Commands.CloseCashRegister;

/// <summary>
/// Manejador de <see cref="CloseCashRegisterCommand"/> encargado de recuperar las ventas del turno,
/// calcular los montos esperados de efectivo y tarjetas, evaluar la cuadratura y cerrar la sesión de caja.
/// </summary>
public sealed class CloseCashRegisterCommandHandler
    : IRequestHandler<CloseCashRegisterCommand, CloseCashRegisterResultDto>
{
    private readonly ICashRegisterRepository _cashRegisterRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly ICurrentUserService _currentUserService;

    public CloseCashRegisterCommandHandler(
        ICashRegisterRepository cashRegisterRepository,
        ISaleRepository saleRepository,
        ICurrentUserService currentUserService)
    {
        _cashRegisterRepository = cashRegisterRepository ?? throw new ArgumentNullException(nameof(cashRegisterRepository));
        _saleRepository = saleRepository ?? throw new ArgumentNullException(nameof(saleRepository));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<CloseCashRegisterResultDto> Handle(
        CloseCashRegisterCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // a. Obtener el TenantId y UserId actual vía ICurrentUserService
        var tenantId = _currentUserService.TenantId;
        var userId = _currentUserService.UserId;

        if (tenantId == Guid.Empty || userId == Guid.Empty)
        {
            throw new UnauthorizedAccessException(
                "No se pudo determinar el TenantId o el UserId del operador desde el token JWT.");
        }

        // b. Obtener la sesión de caja abierta actual; si no existe, lanzar InvalidOperationException
        var openSession = await _cashRegisterRepository.GetOpenSessionByUserAsync(
            tenantId: tenantId,
            userId: userId,
            cancellationToken: cancellationToken);

        if (openSession is null)
        {
            throw new InvalidOperationException(
                "No existe ninguna sesión de caja abierta para el usuario actual que pueda ser cerrada.");
        }

        // c. Obtener todas las ventas asociadas a esa sesión
        var sessionSales = await _saleRepository.GetSalesByCashRegisterSessionAsync(
            sessionId: openSession.Id,
            cancellationToken: cancellationToken);

        // d. Calcular matemáticamente los montos esperados:
        //    - Efectivo Esperado = Fondo inicial de apertura + suma de ventas en efectivo
        //    - Tarjetas Esperado = suma de ventas con tarjeta
        decimal totalCashSales = 0m;
        decimal totalCardSales = 0m;

        foreach (var sale in sessionSales)
        {
            var effectiveSaleTotal = sale.TotalAmount > 0m ? sale.TotalAmount : sale.MontoTotal;

            switch (sale.PaymentMethod)
            {
                case PaymentMethod.Cash:
                    totalCashSales += sale.CashAmount > 0m ? sale.CashAmount : effectiveSaleTotal;
                    break;

                case PaymentMethod.Card:
                    totalCardSales += sale.CardAmount > 0m ? sale.CardAmount : effectiveSaleTotal;
                    break;

                case PaymentMethod.Mixed:
                    totalCashSales += sale.CashAmount;
                    totalCardSales += sale.CardAmount;
                    break;
            }
        }

        var expectedCashAmount = Math.Round(openSession.OpeningAmount + totalCashSales, 2, MidpointRounding.AwayFromZero);
        var expectedCardAmount = Math.Round(totalCardSales, 2, MidpointRounding.AwayFromZero);

        var actualCashAmount = Math.Round(request.ActualCashAmount, 2, MidpointRounding.AwayFromZero);
        var actualCardAmount = Math.Round(request.ActualCardAmount, 2, MidpointRounding.AwayFromZero);

        // e & f. Evaluar cuadratura e invocar el método Close en la entidad CashRegisterSession
        openSession.Close(
            expectedCashAmount: expectedCashAmount,
            expectedCardAmount: expectedCardAmount,
            actualCashAmount: actualCashAmount,
            actualCardAmount: actualCardAmount);

        // g. Persistir la actualización en la base de datos vía el repositorio
        await _cashRegisterRepository.UpdateAsync(openSession, cancellationToken);

        var cashDifference = actualCashAmount - expectedCashAmount;
        var cardDifference = actualCardAmount - expectedCardAmount;
        var totalDifference = (openSession.ActualClosingAmount ?? 0m) - openSession.ExpectedClosingAmount;

        return new CloseCashRegisterResultDto(
            SessionId: openSession.Id,
            Status: openSession.Status,
            OpeningAmount: openSession.OpeningAmount,
            ExpectedCashAmount: expectedCashAmount,
            ActualCashAmount: actualCashAmount,
            CashDifference: cashDifference,
            ExpectedCardAmount: expectedCardAmount,
            ActualCardAmount: actualCardAmount,
            CardDifference: cardDifference,
            ExpectedClosingAmount: openSession.ExpectedClosingAmount,
            ActualClosingAmount: openSession.ActualClosingAmount ?? 0m,
            TotalDifference: totalDifference,
            ClosedAt: openSession.ClosedAt ?? DateTime.UtcNow);
    }
}

