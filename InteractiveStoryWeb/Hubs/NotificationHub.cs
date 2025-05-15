using Microsoft.AspNetCore.SignalR;

namespace InteractiveStoryWeb.Hubs
{
    public class NotificationHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
            await Clients.Caller.SendAsync("ReceiveMessage", "Connected to notification hub");
        }
    }
}