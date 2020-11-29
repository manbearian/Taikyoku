using System;
using System.Net.Http;
using System.Threading.Tasks;
using Grpc.Net.Client;

namespace ShogiClient
{
    public class Connector
    {
        public static async Task<ReadOnlyMemory<byte>> Connect()
        {
            // The port number(5001) must match the port of the gRPC server.
            using var channel = GrpcChannel.ForAddress("https://localhost:5001");
            var client = new GameHost.GameHostClient(channel);
            var reply = await client.StartGameAsync(new Nothing());
            return reply.State.Memory;
        }
    }
}
