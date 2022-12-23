using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace ShogiServerless
{
    class TableStorage
    {
        private readonly CloudTableClient _cloudTableClient;
        private readonly string RunningGameTableName = "RunningGames";

        public TableStorage()
        {
            var storageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage")
                ?? throw new Exception("AzureWebJobsStorage not set");

            //#if true
            //            var storageConnectionString = "UseDevelopmentStorage=true"; //AppSettings.LoadAppSettings().StorageConnectionString ?? string.Empty;
            //#else
            //            var storageConnectionString = "ShogiStorConnectionString"; //  CloudStorageAccount.Parse(.Ap.Parse(CloudConfigurationManger.GetSetting("ShogiStorConnectionString"));
            //#endif
            var storageAccount = CreateStorageAccountFromConnectionString(storageConnectionString);
            _cloudTableClient = storageAccount.CreateCloudTableClient();
        }

        // returns the added GameInfo if successfully added to the table strorage otherwise null
        public async Task<GameInfo?> AddGame(GameInfo gameInfo)
        {
            try
            {
                var table = _cloudTableClient.GetTableReference(RunningGameTableName);
                await table.CreateIfNotExistsAsync();
                var lookupOp = TableOperation.Retrieve<GameInfo>("", gameInfo.Id.ToString());
                var oldGameInfo = (await table.ExecuteAsync(lookupOp)).Result as GameInfo;
                
                // A serious eroror occured... this game is already in the table storage! 
                if (oldGameInfo is not null)
                    return null;

                // todo: validate what happens here if the game was updated between the read and write
                // theoretically it should fail, but what we really want to do is retry the write.
                var updateOp = TableOperation.InsertOrReplace(gameInfo);
                return (await table.ExecuteAsync(updateOp)).Result as GameInfo;
            }
            catch (StorageException)
            {

            }

            return null;
        }

        // returns the updated GameInfo if successfully updated otherwise null
        public async Task<GameInfo?> UpdateGame(GameInfo gameInfo)
        {
            try
            {
                var table = _cloudTableClient.GetTableReference(RunningGameTableName);
                await table.CreateIfNotExistsAsync();

                var lookupOp = TableOperation.Retrieve<GameInfo>("", gameInfo.Id.ToString());
                var oldGameInfo = (await table.ExecuteAsync(lookupOp)).Result as GameInfo;

                // A serious eroror occured... this game is not in the table storage! 
                if (oldGameInfo is null)
                    return null;

                // game was recorded as ending; don't update things
                // this can happen if our opponent conceded while we were making a move
                if (oldGameInfo?.Game.Ending is not null)
                    return oldGameInfo;

                // todo: validate what happens here if the game was updated between the read and write
                // theoretically it should fail, but what we really want to do is retry the write.
                var updateOp = TableOperation.InsertOrReplace(gameInfo);
                return (await table.ExecuteAsync(updateOp)).Result as GameInfo;
            }
            catch(StorageException)
            {

            }

            return null;
        }

        public async Task<IEnumerable<GameInfo>> AllGames()
        {
            try
            {
                var table = _cloudTableClient.GetTableReference(RunningGameTableName);
                TableContinuationToken? token = null;
                var entities = new List<GameInfo>();
                do
                {
                    var queryResult = await table.ExecuteQuerySegmentedAsync(new TableQuery<GameInfo>(), token);
                    entities.AddRange(queryResult.Results);
                    token = queryResult.ContinuationToken;
                } while (token is not null);

                return entities;
            }
            catch (StorageException)
            {

            }

            return new List<GameInfo>();
        }

        public async Task<GameInfo?> FindGame(Guid gameId)
        {
            try
            {
                var table = _cloudTableClient.GetTableReference(RunningGameTableName);

                var tableOp = TableOperation.Retrieve<GameInfo>("", gameId.ToString());

                // Execute the operation.
                var result = await table.ExecuteAsync(tableOp);
                return result.Result as GameInfo;
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
