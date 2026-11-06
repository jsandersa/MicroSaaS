using MediatR;
using Microsoft.EntityFrameworkCore;
using MicroSaaS.Application.ElectronicInvoicing.Commands.EmitDte;
using MicroSaaS.Application.Inventory.Commands.RegisterInventoryMovement;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Core.Enums;
using MicroSaaS.Core.Interfaces;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.Sales.Commands.RegisterSale;

/// <summary>
/// Manejador de <see cref="RegisterSaleCommand"/> que orquesta dentro de una transacción atómica compatible con <c>NpgsqlRetryingExecutionStrategy</c>:
/// a) Validación de sesión de caja abierta,
/// b) Obtención del folio correlativo fiscal y persistencia de <see cref="Sale"/> y sus <see cref="SaleItem"/>s vía <see cref="ISaleRepository"/>,
/// c) Despacho de <see cref="RegisterInventoryMovementCommand"/> (<see cref="MovementType.Out"/>) por cada ítem,
/// d) Despacho de <see cref="EmitDteCommand"/> para la emisión del documento tributario electrónico y sincronización del folio.
/// </summary>
public sealed class RegisterSaleCommandHandler : IRequestHandler<RegisterSaleCommand, Guid>
{
    private readonly ISaleRepository _saleRepository;
    private readonly ICashRegisterRepository _cashRegisterRepository;
    private readonly IDteRepository _dteRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly TenantDbContext _dbContext;
    private readonly IMediator _mediator;

    public RegisterSaleCommandHandler(
        ISaleRepository saleRepository,
        ICashRegisterRepository cashRegisterRepository,
        IDteRepository dteRepository,
        ICurrentUserService currentUserService,
        TenantDbContext dbContext,
        IMediator mediator)
    {
        _saleRepository = saleRepository ?? throw new ArgumentNullException(nameof(saleRepository));
        _cashRegisterRepository = cashRegisterRepository ?? throw new ArgumentNullException(nameof(cashRegisterRepository));
        _dteRepository = dteRepository ?? throw new ArgumentNullException(nameof(dteRepository));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public async Task<Guid> Handle(
        RegisterSaleCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Items);

        if (request.Items.Count == 0)
        {
            throw new InvalidOperationException("La venta debe contener al menos un producto.");
        }

        var tenantId = _currentUserService.TenantId;
        var userId = _currentUserService.UserId;

        if (tenantId == Guid.Empty || userId == Guid.Empty)
        {
            throw new UnauthorizedAccessException(
                "No se pudo determinar el TenantId o el UserId del operador desde el token JWT.");
        }

        // a. Validar vía ICashRegisterRepository que el usuario actual tenga una sesión de caja abierta
        var openSession = await _cashRegisterRepository.GetOpenSessionByUserAsync(
            tenantId: tenantId,
            userId: userId,
            cancellationToken: cancellationToken);

        if (openSession is null)
        {
            throw new InvalidOperationException(
                "No existe una sesión de caja abierta para el operador actual. Debe abrir caja antes de registrar ventas.");
        }

        var mappedPaymentMethod = (request.PaymentMethod?.Trim().ToUpperInvariant()) switch
        {
            "CARD" => PaymentMethod.Card,
            "MIXED" => PaymentMethod.Mixed,
            _ => PaymentMethod.Cash
        };

        var resolvedTipoDte = request.TipoDte > 0 ? request.TipoDte : (int)DteType.BoletaElectronica;
        var resolvedDteType = Enum.IsDefined(typeof(DteType), resolvedTipoDte)
            ? (DteType)resolvedTipoDte
            : DteType.BoletaElectronica;

        // Envolver en CreateExecutionStrategy().ExecuteAsync para soportar EnableRetryOnFailure de Npgsql
        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            var hasExistingTransaction = _dbContext.Database.CurrentTransaction is not null;
            await using var transaction = hasExistingTransaction
                ? null
                : await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                // Obtener el correlativo real desde DteDocuments para mantener alineados Sales.Folio y DteDocuments.Folio
                var correlatedFolio = await _dteRepository.GetNextFolioAsync(
                    tenantId: tenantId,
                    dteType: resolvedDteType,
                    cancellationToken: cancellationToken);

                // b. Crear y persistir la entidad Sale junto a sus SaleItems usando ISaleRepository
                var sale = Sale.Create(
                    tenantId: tenantId,
                    userId: userId,
                    cashRegisterSessionId: openSession.Id,
                    paymentMethod: mappedPaymentMethod);

                sale.Folio = correlatedFolio;
                sale.TipoDte = (int)resolvedDteType;
                sale.RutCliente = string.IsNullOrWhiteSpace(request.RutCliente) ? null : request.RutCliente.Trim();
                sale.RazonSocialCliente = string.IsNullOrWhiteSpace(request.RazonSocialCliente) ? null : request.RazonSocialCliente.Trim();
                sale.CashAmount = request.CashAmount;
                sale.CardAmount = request.CardAmount;

                foreach (var itemDto in request.Items)
                {
                    var resolvedQty = itemDto.Quantity > 0
                        ? itemDto.Quantity
                        : (int)Math.Round(itemDto.Cantidad, 0, MidpointRounding.AwayFromZero);

                    var resolvedUnitPrice = itemDto.UnitPrice > 0m
                        ? itemDto.UnitPrice
                        : itemDto.PrecioUnitario;

                    sale.AddSaleItem(
                        productId: itemDto.ProductId,
                        quantity: resolvedQty,
                        unitPrice: resolvedUnitPrice,
                        nombreItem: itemDto.NombreItem);
                }

                if (request.MontoTotal > 0m)
                {
                    sale.MontoNeto = request.MontoNeto;
                    sale.Iva = request.Iva;
                    sale.TotalAmount = request.MontoTotal;
                    sale.MontoTotal = request.MontoTotal;
                }

                await _saleRepository.AddAsync(sale, cancellationToken);

                // c. Por cada ítem, despachar internamente RegisterInventoryMovementCommand (MovementType.Out) vía MediatR
                foreach (var item in sale.Items)
                {
                    if (item.ProductId != Guid.Empty)
                    {
                        await _mediator.Send(
                            new RegisterInventoryMovementCommand(
                                ProductId: item.ProductId,
                                MovementType: MovementType.Out,
                                Quantity: item.Quantity,
                                UnitCost: item.UnitPrice),
                            cancellationToken);
                    }
                }

                // d. Despachar EmitDteCommand vía MediatR para generar el documento fiscal correspondiente
                await _mediator.Send(
                    new EmitDteCommand(
                        SaleId: sale.Id,
                        DteType: resolvedDteType,
                        TotalAmount: sale.TotalAmount),
                    cancellationToken);

                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return sale.Id;
            }
            catch
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                throw;
            }
        });
    }
}
