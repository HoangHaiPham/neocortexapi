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

        //public async Task<InputFileParameters> DownloadInputFile(string fileName, BlobContainerClient trainingcontainer)
        //{
        //    string path = $"{Directory.GetCurrentDirectory()}\\SPMemoCapaResult\\Download";
        //    Directory.CreateDirectory(path);
        //    string downloadFilePath = $"{path}\\{fileName}";

        //    // Get a reference to a blob
        //    BlobClient blobClient = trainingcontainer.GetBlobClient(fileName);

        //    // List all blobs in the containe
        //    List<String> fileNameInBlob = new List<String>();
        //    await foreach (BlobItem blobItem in trainingcontainer.GetBlobsAsync())
        //    {
        //        fileNameInBlob.Add(blobItem.Name);
        //    }


        //    if (fileNameInBlob.Contains(fileName))
        //    {
        //        // Download the blob's contents and save it to a file
        //        BlobDownloadInfo download = await blobClient.DownloadAsync();

        //        using (FileStream downloadFileStream = File.OpenWrite(downloadFilePath))
        //        {
        //            await download.Content.CopyToAsync(downloadFileStream);
        //            downloadFileStream.Close();
        //        }

        //        // Get parameters from InputFile
        //        string jsonText = File.ReadAllText(downloadFilePath);

        //        Console.ForegroundColor = ConsoleColor.Cyan;
        //        Console.WriteLine($">> Content of InputFile '{fileName}':");
        //        Console.WriteLine($"{jsonText}\n");
        //        Console.ResetColor();

        //        InputFileParameters parameters = JsonConvert.DeserializeObject<InputFileParameters>(jsonText);

        //        //File.Delete(downloadFilePath);
        //        Directory.Delete(path, true);

        //        return parameters;
        //    }

        //    else
        //    {
        //        return null;
        //    }
        //}

        //public async Task UploadResultFile(ExperimentResult result)
        //{
        //    // Create a BlobServiceClient object which will be used to create a container client
        //    BlobServiceClient blobServiceClient = new BlobServiceClient(config.StorageConnectionString);

        //    BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(config.ResultContainer);
        //    try
        //    {
        //        await containerClient.CreateIfNotExistsAsync();
        //    }
        //    catch
        //    {
        //        Console.WriteLine($">> Conatiner {config.ResultContainer} cannot be accessed.\n");
        //        throw new NotImplementedException();
        //    }

        //    UploadFile(containerClient, result.FilePathscalarEncoder);
        //    UploadFile(containerClient, result.FilePathFreshArray);
        //    UploadFile(containerClient, result.FilePathDiffArray);
        //    UploadFile(containerClient, result.FilePathHammingOutput);
        //    UploadFile(containerClient, result.FilePathHammingBitmap);
        //}

        //private async void UploadFile(BlobContainerClient containerClient, string FilePath)
        //{
        //    BlobClient blobClient = containerClient.GetBlobClient(FilePath);

        //    Console.WriteLine($">> Uploading {FilePath} onto Container '{config.ResultContainer}'!!!");

        //    // Upload data from the local file
        //    using FileStream uploadFileStream = File.OpenRead(FilePath);
        //    await blobClient.UploadAsync(uploadFileStream, true);
        //    uploadFileStream.Close();
        //}

        public async Task UploadExperimentResult(ExperimentResult result)
        {
            string tableName = config.ResultTable;

            // Create or reference an existing table
            CloudTable table = await CreateTableAsync(tableName);
            try
            {
                // Insert the entity
                Console.WriteLine($">> Uploading result onto Table Storage '{tableName}'!!!");
                await InsertOrMergeEntityAsync(table, result);
            }
            catch (StorageException e)
            {
                Console.WriteLine(e.Message);
                Console.ReadLine();
                throw;
            }
        }

        public static async Task UploadFolderToBlogStorage(BlobContainerClient blobStorageName, string outputFolderBlobStorage, string localFolder)
        {
            var files = Directory.GetFiles(localFolder, "*.*", SearchOption.AllDirectories);
            foreach (var localFilePath in files)
            {
                await UploadFileToBlobStorage(blobStorageName, outputFolderBlobStorage, localFilePath);
            }
        }

        public static async Task UploadFileToBlobStorage(BlobContainerClient blobStorageName, string cloudExperimentOutputFolder, string localFilePath)
        {
            // Get a reference to a blob
            BlobClient blobClient = blobStorageName.GetBlobClient(Path.Combine(cloudExperimentOutputFolder, localFilePath));
            // Upload data from the local file
            await blobClient.UploadAsync(localFilePath, true);

            //Console.WriteLine("Uploading to Blob storage as blob:\n\t {0}\n", blobClient.Uri);
        }

        public static async Task<BlobContainerClient> CreateBlobStorage(MyConfig config)
        {
            // TODO created blob storage
            BlobServiceClient blobServiceClient = new BlobServiceClient(config.StorageConnectionString);

            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(config.TrainingContainer);
            try
            {
                bool isExist = containerClient.Exists();
                if (!isExist)
                {
                    await containerClient.CreateIfNotExistsAsync();
                    Console.WriteLine($">> Training Container '{config.TrainingContainer}' created!");
                }
                else
                {
                    Console.WriteLine($">> Training Container '{config.TrainingContainer}' already exists!");
                }
                Console.WriteLine($">> Waiting for Queue Message ...");
            }
            catch
            {
                Console.WriteLine($">> Training Container {config.TrainingContainer} cannot be accessed.\n");
                throw new NotImplementedException();
            }
            return containerClient;
        }

        public static async Task<ExperimentResult> InsertOrMergeEntityAsync(CloudTable table, ExperimentResult result)
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

        /// <summary>
        /// Validate the connection string information in app.config and throws an exception if it looks like 
        /// the user hasn't updated this to valid values. 
        /// </summary>
        /// <param name="storageConnectionString">The storage connection string</param>
        /// <returns>CloudStorageAccount object</returns>
        public static CloudStorageAccount CreateStorageAccountFromConnectionString(string storageConnectionString)
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

        public static async Task GetMnistDatasetFromBlobStorage(BlobContainerClient blobStorageName, string MnistFolderFromBlobStorage)
        {
            await foreach (BlobItem blobInfo in blobStorageName.GetBlobsAsync())
            {
                BlobClient blobClient = blobStorageName.GetBlobClient(blobInfo.Name);

                // Download the blob's contents and save it
                if (!File.Exists(blobInfo.Name) && (blobInfo.Name.Contains(MnistFolderFromBlobStorage)))
                {
                    Utility.CreateFolderIfNotExist(Path.Combine(blobInfo.Name, @"..\"));

                    Console.WriteLine($"Download {MnistFolderFromBlobStorage}: " + blobInfo.Name);

                    using (var fileStream = System.IO.File.OpenWrite(blobInfo.Name))
                    {
                        await blobClient.DownloadToAsync(fileStream);
                    }

                }
            }
        }
    }
}