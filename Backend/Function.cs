using System;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

// this was automatically created and i don't know if i need it
#if false

namespace ShogiBackend
{
    public static class Function
    {
        [FunctionName("Function")]
        public static void Run([BlobTrigger("Path/{name}", Connection = "ConnectionStrings:AzureWebJobsStorage")]Stream myBlob, string name, ILogger log)
        {
            log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");
        }
    }
}
#endif
