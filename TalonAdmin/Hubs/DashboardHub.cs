using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.AspNet.SignalR;

namespace TalonAdmin.Hubs
{
    public class DashboardHub : Hub
    {
        public void Hello()
        {
            Clients.All.hello();
        }
    }
}