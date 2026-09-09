using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using MesaiYonetimSistemi.Core.Interfaces;

namespace MesaiYonetimSistemi.Infrastructure.Hubs
{
    public class NotificationHubService : INotificationHubService
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public NotificationHubService(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task SendUserNotificationAsync(string userId, object payload)
        {
            await _hubContext.Clients.Group($"User_{userId}").SendAsync("ReceiveNotification", payload);
        }

        public async Task SendRoleNotificationAsync(string roleName, object payload)
        {
            await _hubContext.Clients.Group($"Role_{roleName}").SendAsync("ReceiveNotification", payload);
        }
    }
}
