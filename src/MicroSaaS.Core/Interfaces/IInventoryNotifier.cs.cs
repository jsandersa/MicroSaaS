namespace MicroSaaS.Core.Interfaces;

public interface IInventoryNotifier
{
    Task NotifyStockUpdateAsync(Guid productId, int newStock, CancellationToken cancellationToken = default);
}