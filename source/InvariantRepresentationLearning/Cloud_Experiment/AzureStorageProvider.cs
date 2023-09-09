using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.Configuration;
using Cloud_Common;
using AzureStorageCloudStorageAccount = Microsoft.Azure.Storage.CloudStorageAccount;
using InvariantLearning_Utilities;

namespace Cloud_Experiment
{
    public class AzureStorageProvider : IStorageProvider
    {
        private MyConfig config;

        public AzureStorageProvider(IConfigurationSection configSection)
        {
            config = new MyConfig();
            configSection.Bind(config);
        }

        public async Task UploadExperimentResult(ExperimentResult result)
        {
            string tableName = config.ResultTable;

            // Create or reference an existing table
            CloudTable table = await CreateTableAsync(tableName);
            try
            {
                // Insert the entity
                Console.WriteLine($">> Uploading result to Table Storage '{tableName}'!!!");
                await InsertOrMergeEntityAsync(table, result);
            }
            catch (StorageException e)
            {
                Console.WriteLine(e.Message);
                Console.ReadLine();
                throw;
            }
        }

        public async Task UploadFolderToBlogStorage(BlobContainerClient blobStorageName, string outputFolderBlobStorage, string localFolder)
        {
            var files = Directory.GetFiles(localFolder, "*.*", SearchOption.AllDirectories);
            foreach (var localFilePath in files)
            {
                await UploadFileToBlobStorage(blobStorageName, outputFolderBlobStorage, localFilePath);
            }
        }

        public async Task UploadFileToBlobStorage(BlobContainerClient blobStorageName, string cloudExperimentOutputFolder, string localFilePath)
        {
            // Get a reference to a blob
            BlobClient blobClient = blobStorageName.GetBlobClient(Path.Combine(cloudExperimentOutputFolder, localFilePath));
            // Upload data from the local file
            await blobClient.UploadAsync(localFilePath, true);

            //Console.WriteLine("Uploading to Blob storage as blob:\n\t {0}\n", blobClient.Uri);
        }

        // TODO remove this function because when running on docker, permission denied when trying to download MnistDataset
        public async Task GetMnistDatasetFromBlobStorage(BlobContainerClient blobStorageName, string MnistFolderFromBlobStorage)
        {
            int numMnistDataset = 0;
            await foreach (BlobItem blobInfo in blobStorageName.GetBlobsAsync())
            {
                BlobClient blobClient = blobStorageName.GetBlobClient(blobInfo.Name);
                numMnistDataset += 1;

                // Download the blob's contents and save it
                if (!File.Exists(blobInfo.Name) && (blobInfo.Name.Contains(MnistFolderFromBlobStorage)))
                {
                    Utility.CreateFolderIfNotExist(Path.GetDirectoryName(blobInfo.Name));
                    
                    Console.WriteLine($"Download {MnistFolderFromBlobStorage}: " + blobInfo.Name);

                    await blobClient.DownloadToAsync(blobInfo.Name);

                    //using (var fileStream = System.IO.File.OpenWrite(blobInfo.Name))
                    //{
                    //    await blobClient.DownloadToAsync(fileStream);
                    //}
                }
                else
                {
                    Console.WriteLine($"{blobInfo.Name} already exists!");
                }
            }

            if (numMnistDataset == 0)
            {
                throw new Exception($"{MnistFolderFromBlobStorage} is NULL. Please upload {MnistFolderFromBlobStorage} to {blobStorageName}");
            }
        }

        public async Task<CloudTable> CreateTableAsync(string tableName)
        {
            string storageConnectionString = config.StorageConnectionString;

            // Retrieve storage account information from connection string.
            CloudStorageAccount storageAccount = CreateStorageAccountFromConnectionString(storageConnectionString);

            // Create a table client for interacting with the table service
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient(new TableClientConfiguration());
            CloudTable table = tableClient.GetTableReference(tableName);

            try
            {
                await table.CreateIfNotExistsAsync();
            }
            catch
            {
                Console.WriteLine($">> Table '{tableName}' cannot be accessed.\n");
                throw new NotImplementedException();
            }
            return table;
        }

        public async Task<ExperimentResult> InsertOrMergeEntityAsync(CloudTable table, ExperimentResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException("result");
            }

            try
            {
                // Create the InsertOrReplace table operation
                TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(result);

                // Execute the operation.
                TableResult resultTable = await table.ExecuteAsync(insertOrMergeOperation);
                ExperimentResult insertedCustomer = resultTable.Result as ExperimentResult;

                if (resultTable.RequestCharge.HasValue)
                {
                    Console.WriteLine(">> Request Charge of InsertOrMerge Operation: " + resultTable.RequestCharge);
                }

                return insertedCustomer;
            }
            catch (StorageException e)
            {
                Console.WriteLine(e.Message);
                Console.ReadLine();
                throw;
            }
        }

        public async Task<BlobContainerClient> CreateBlobStorage(string StorageConnectionString, string ContainerName)
        {
            // TODO created blob storage
            BlobServiceClient blobServiceClient = new BlobServiceClient(StorageConnectionString);

            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
            try
            {
                bool isExist = containerClient.Exists();
                if (!isExist)
                {
                    await containerClient.CreateIfNotExistsAsync();
                    Console.WriteLine($">> Training Container '{ContainerName}' created!");
                }
                else
                {
                    Console.WriteLine($">> Training Container '{ContainerName}' already exists!");
                }
                Console.WriteLine($">> Waiting for Queue Message ...");
            }
            catch
            {
                Console.WriteLine($">> Training Container {ContainerName} cannot be accessed.\n");
                throw new NotImplementedException();
            }
            return containerClient;
        }

        /// <summary>
        /// Validate the connection string information in app.config and throws an exception if it looks like 
        /// the user hasn't updated this to valid values. 
        /// </summary>
        /// <param name="storageConnectionString">The storage connection string</param>
        /// <returns>CloudStorageAccount object</returns>
        public CloudStorageAccount CreateStorageAccountFromConnectionString(string storageConnectionString)
        {
            CloudStorageAccount storageAccount;
            try
            {
                storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            }
            catch (FormatException)
            {
                Console.WriteLine(">> Invalid storage account information provided. Please confirm the AccountName and AccountKey are valid in the app.config file - then restart the application.");
                throw;
            }
            catch (ArgumentException)
            {
                Console.WriteLine(">> Invalid storage account information provided. Please confirm the AccountName and AccountKey are valid in the app.config file - then restart the sample.");
                Console.ReadLine();
                throw;
            }

            return storageAccount;
        }

        /// <summary>
        /// Validate the connection string information in app.config and throws an exception if it looks like 
        /// the user hasn't updated this to valid values. 
        /// </summary>
        /// <param name="storageConnectionString">The storage connection string</param>
        /// <returns>CloudStorageAccount object</returns>
        public static AzureStorageCloudStorageAccount CreateAzureStorageAccountFromConnectionString(string storageConnectionString)
        {
            AzureStorageCloudStorageAccount storageAccount;
            try
            {
                storageAccount = AzureStorageCloudStorageAccount.Parse(storageConnectionString);
            }
            catch (FormatException)
            {
                Console.WriteLine(">> Invalid storage account information provided. Please confirm the AccountName and AccountKey are valid in the app.config file - then restart the application.");
                throw;
            }
            catch (ArgumentException)
            {
                Console.WriteLine(">> Invalid storage account information provided. Please confirm the AccountName and AccountKey are valid in the app.config file - then restart the sample.");
                Console.ReadLine();
                throw;
            }

            return storageAccount;
        }
    }
}