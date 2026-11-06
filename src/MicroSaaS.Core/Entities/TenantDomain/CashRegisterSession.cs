using MicroSaaS.Core.Enums;

namespace MicroSaaS.Core.Entities.TenantDomain;

/// <summary>
/// Entidad de dominio inmutable que representa una sesión operativa de caja (apertura, arqueo y cierre)
/// dentro del esquema aislado de un Tenant.
/// </summary>
public sealed class CashRegisterSession
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public decimal OpeningAmount { get; private set; }
    public decimal ExpectedClosingAmount { get; private set; }
    public decimal? ActualClosingAmount { get; private set; }
    public DateTime OpenedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public CashRegisterStatus Status { get; private set; }

    // Constructor privado requerido por Entity Framework Core
    private CashRegisterSession()
    {
    }

    private CashRegisterSession(
        Guid tenantId,
        Guid userId,
        decimal openingAmount,
        DateTime openedAtUtc)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("El identificador del Tenant no puede ser vacío.", nameof(tenantId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("El identificador del usuario cajero no puede ser vacío.", nameof(userId));
        }

        if (openingAmount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(openingAmount),
                "El monto de apertura de caja no puede ser negativo.");
        }

        Id = Guid.NewGuid();
        TenantId = tenantId;
        UserId = userId;
        OpeningAmount = openingAmount;
        ExpectedClosingAmount = openingAmount;
        ActualClosingAmount = null;
        OpenedAt = openedAtUtc;
        ClosedAt = null;
        Status = CashRegisterStatus.Open;
    }

    /// <summary>
    /// Abre una nueva sesión de caja garantizando las invariantes de negocio iniciales.
    /// </summary>
    public static CashRegisterSession Open(
        Guid tenantId,
        Guid userId,
        decimal openingAmount)
    {
        return new CashRegisterSession(
            tenantId: tenantId,
            userId: userId,
            openingAmount: openingAmount,
            openedAtUtc: DateTime.UtcNow);
    }

    /// <summary>
    /// Registra una entrada o salida de efectivo esperada sobre la sesión abierta actual.
    /// </summary>
    public void RegisterExpectedCashMovement(decimal amountDelta)
    {
        if (Status != CashRegisterStatus.Open)
        {
            throw new InvalidOperationException(
                "No se pueden registrar movimientos en una sesión de caja que ya fue cerrada.");
        }

        var updatedExpected = ExpectedClosingAmount + amountDelta;
        if (updatedExpected < 0m)
        {
            throw new InvalidOperationException(
                "El saldo esperado de efectivo en caja no puede quedar en negativo.");
        }

        ExpectedClosingAmount = updatedExpected;
    }

    /// <summary>
    /// Ejecuta el cierre y arqueo de la sesión de caja comparando los montos esperados y reales de efectivo y tarjetas,
    /// fijando el estado en <see cref="CashRegisterStatus.Closed"/> si cuadran exactamente o en <see cref="CashRegisterStatus.Discrepancy"/> si existen diferencias.
    /// </summary>
    public void Close(
        decimal expectedCashAmount,
        decimal expectedCardAmount,
        decimal actualCashAmount,
        decimal actualCardAmount)
    {
        if (Status != CashRegisterStatus.Open)
        {
            throw new InvalidOperationException(
                $"No es posible cerrar la sesión de caja '{Id}' porque su estado actual es '{Status}'.");
        }

        if (expectedCashAmount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedCashAmount),
                "El monto esperado de efectivo no puede ser negativo.");
        }

        if (expectedCardAmount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedCardAmount),
                "El monto esperado de tarjetas no puede ser negativo.");
        }

        if (actualCashAmount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(actualCashAmount),
                "El monto real de efectivo contado no puede ser negativo.");
        }

        if (actualCardAmount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(actualCardAmount),
                "El monto real de vouchers de tarjeta no puede ser negativo.");
        }

        ExpectedClosingAmount = expectedCashAmount + expectedCardAmount;
        ActualClosingAmount = actualCashAmount + actualCardAmount;
        ClosedAt = DateTime.UtcNow;

        var isCashBalanced = actualCashAmount == expectedCashAmount;
        var isCardBalanced = actualCardAmount == expectedCardAmount;

        Status = isCashBalanced && isCardBalanced
            ? CashRegisterStatus.Closed
            : CashRegisterStatus.Discrepancy;
    }

    /// <summary>
    /// Sobrecarga para cierre directo por totales esperados vs reales.
    /// </summary>
    public void Close(decimal expectedClosingAmount, decimal actualClosingAmount)
    {
        Close(
            expectedCashAmount: expectedClosingAmount,
            expectedCardAmount: 0m,
            actualCashAmount: actualClosingAmount,
            actualCardAmount: 0m);
    }
}
