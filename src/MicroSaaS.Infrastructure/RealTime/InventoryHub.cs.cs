using Microsoft.AspNetCore.SignalR;

namespace MicroSaaS.Infrastructure.RealTime;

/// <summary>
/// Hub de SignalR limpio y sin dependencias de contexto HTTP 
/// para garantizar la conexión exitosa del cliente web.
/// </summary>
public class InventoryHub : Hub
{
    // Mantenemos la clase limpia. El enrutamiento global (Clients.All) 
    // se maneja directamente desde el SignalRInventoryNotifier.
}