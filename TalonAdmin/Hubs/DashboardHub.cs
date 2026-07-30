using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace TalonAdmin.Hubs
{
    public class DashboardHub : Hub
    {
        public Task Hello()
        {
            return Clients.All.SendAsync("hello");
        }
    }
}
