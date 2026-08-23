using Microsoft.AspNetCore.SignalR;

namespace DarajaDemo.Hubs;

public class PaymentHub : Hub
{
    // Clients connect to this hub. 
    // You can add logic here to map connection IDs to specific POS registers if needed.
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }
}