using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace MesaiYonetimSistemi.Infrastructure.Hubs
{
    public class NotificationHub : Hub
    {
        public async Task JoinUserGroup(string userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
        }

        public async Task LeaveUserGroup(string userId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId}");
        }

        public async Task JoinRoleGroup(string roleName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Role_{roleName}");
        }
    }
}
