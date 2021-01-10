using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Azure.Cosmos.Table;

using ShogiServer.Hubs;

namespace ShogiServer
{
    class TableStorage
    {
        private readonly CloudTableClient _cloudTableClient;
        private readonly string RunningGameTableName = "RunningGames";

        public TableStorage()
        {
            var storageConnectionString = AppSettings.LoadAppSettings().StorageConnectionString ?? string.Empty;
            var storageAccount = CreateStorageAccountFromConnectionString(storageConnectionString);
            _cloudTableClient = storageAccount.CreateCloudTableClient(new TableClientConfiguration());
        }

        public void AddOrUpdateGame(ShogiHub.GameInfo gameInfo)
        {
            // Create a table client for interacting with the table service 
            var table = _cloudTableClient.GetTableReference(RunningGameTableName);
            table.CreateIfNotExists();

            var tableOp = TableOperation.InsertOrReplace(gameInfo);

            // Execute the operation.
            table.Execute(tableOp);
        }

        public async Task<ShogiHub.GameInfo?> FindGame(Guid gameId)
        {
            try
            {
                var table = _cloudTableClient.GetTableReference(RunningGameTableName);

                var tableOp = TableOperation.Retrieve<ShogiHub.GameInfo>("", gameId.ToString());

                // Execute the operation.
                var result = await table.ExecuteAsync(tableOp);
                return result.Result as ShogiHub.GameInfo;
            }
            catch (StorageException)
            {
            }

            return null;
        }

        private static CloudStorageAccount CreateStorageAccountFromConnectionString(string storageConnectionString)
        {
            CloudStorageAccount storageAccount;
            try
            {
                storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            }
            catch (FormatException)
            {
                Console.WriteLine("Invalid storage account information provided. Please confirm the AccountName and AccountKey are valid in the app.config file - then restart the application.");
                throw;
            }
            catch (ArgumentException)
            {
                Console.WriteLine("Invalid storage account information provided. Please confirm the AccountName and AccountKey are valid in the app.config file - then restart the sample.");
                Console.ReadLine();
                throw;
            }

            return storageAccount;
        }
    }
}
