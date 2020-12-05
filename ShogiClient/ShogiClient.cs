using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR.Client;

namespace ShogiClient
{
    public class ShogiClient
    {
        public readonly HubConnection connection;

        public ShogiClient()
        {
            connection = new HubConnectionBuilder().
                WithUrl("https://localhost:44352/ShogiHub").
                Build();

            connection.Closed += async (error) =>
            {
                // manual reconnect on disconnect
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await connection.StartAsync();
            };
        }

    }
}
