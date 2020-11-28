using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

using Oracle;

namespace ShogiBackend
{
    public interface IGameEntity
    {
        void Create();
    }

    public class GameEntity : IGameEntity
    {
        public TaikyokuShogi Game { get; set; }

        public void Create() => Game = new TaikyokuShogi();

        [FunctionName(nameof(GameEntity))]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx)
            => ctx.DispatchAsync<GameEntity>();
    }

    public static class GameOrchestrator
    {
        [FunctionName("GameOrchestrator")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
#if false
            var entityId = new EntityId(nameof(GameEntity), "theGame");
                if (i++ == 0)
                {
                    await context.SignalEntity(entityId, "Create");
                    return client.(req, instanceId);
                }
                else
                {
                    return client.SignalEntityAsync(entityId, "Create");
                }
            }
#endif

            var game = context.GetInput<TaikyokuShogi>();
            var outputs = new List<string>();

            // Replace "hello" with the name of your Durable Activity Function.
            outputs.Add(await context.CallActivityAsync<string>("GameOrchestrator_Hello", "Tokyo"));
            outputs.Add(await context.CallActivityAsync<string>("GameOrchestrator_Hello", "Seattle"));
            outputs.Add(await context.CallActivityAsync<string>("GameOrchestrator_Hello", "London"));

            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
            return outputs;
        }

        [FunctionName("GameOrchestrator_Hello")]
        public static string SayHello([ActivityTrigger] string name, ILogger log)
        {
            log.LogInformation($"Saying hello to {name}.");
            return $"Hello {name}!";
        }

#if false
        [FunctionName("GameOrchestrator_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            const string instanceId = "dummyId";

            var status = await starter.GetStatusAsync(instanceId);
            if (status == null)
            {
                // start a new game with this id
                var game = new TaikyokuShogi();
                await starter.StartNewAsync("GameOrchestrator", instanceId, game);
                log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
                return starter.CreateCheckStatusResponse(req, instanceId);
            }
            else
            {
                // An instance with the specified ID exists, don't create one.

                return req.CreateErrorResponse(
                    HttpStatusCode.Conflict,
                    $"An instance with ID '{instanceId}' already exists.");
            }
        }
#else

        [FunctionName("GameOrchestrator_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableEntityClient starter,
            ILogger log)
        {
            var entityId = new EntityId(nameof(GameEntity), "myCounter");
            log.LogInformation($"Started orchestration with ID = 'myCounter'.");
            await starter.SignalEntityAsync<IGameEntity>(entityId, proxy => proxy.Create());
            return req.CreateResponse(HttpStatusCode.OK, "hello, world");
        }
#endif
    }
}
