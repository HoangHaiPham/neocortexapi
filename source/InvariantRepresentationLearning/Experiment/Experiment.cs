using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Queue;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Newtonsoft.Json;
using Azure.Storage.Blobs.Models;
using Cloud_Common;
using InvariantLearning_Utilities;

namespace Cloud_Experiment
{
    public class Experiment : IExperiment
    {
        private IStorageProvider storageProvider;

        private ILogger logger;

        private MyConfig config;
        private Task GetMnistDataset;

        public Experiment(MyConfig config, IStorageProvider storageProvider, ILogger log)
        {
            //this.storageProvider = storageProvider;
            this.storageProvider = (AzureStorageProvider)storageProvider;
            this.config = config;
            this.logger = log;
        }


        public Task<ExperimentResult> Run(ExerimentRequestMessage msg, BlobContainerClient blobStorageName)
        {
            // TODO read file

            ExperimentResult res = new ExperimentResult(this.config.GroupId, null);

            res.StartTimeUtc = DateTime.UtcNow;

            RunInvariantRepresentation InvariantRepresentation = new RunInvariantRepresentation();

            Dictionary<string, string> keyValues = null;

            InvariantRepresentation.Semantic_InvariantRepresentation(this.config, msg, blobStorageName);

            res.PartitionKey = ($"Experiment ID: {InvariantRepresentation.experimentFolder}");
            res.RowKey = ($"{msg.IMAGE_WIDTH}x{msg.IMAGE_HEIGHT}_{msg.FRAME_WIDTH}x{msg.FRAME_HEIGHT}_{msg.NUM_IMAGES_PER_LABEL * 10}-{msg.PER_TESTSET}%_Cycle{msg.MAX_CYCLE}");
            
            //res.Name = msg.Name;
            //res.Description = msg.Description;
            //res.InputFileName = msg.InputFile;
            //res.EndTimeUtc = DateTime.UtcNow;
            //res.DurationSec = (long)(res.EndTimeUtc - res.StartTimeUtc).TotalSeconds;
            //res.FilePathFreshArray = keyValues["FilePathFreshArray"];
            //res.FilePathDiffArray = keyValues["FilePathDiffArray"];
            //res.FilePathscalarEncoder = keyValues["FilePathscalarEncoder"];
            //res.FilePathHammingOutput = keyValues["FilePathHammingOutput"];
            //res.FilePathHammingBitmap = keyValues["FilePathHammingBitmap"];

            return Task.FromResult<ExperimentResult>(res); // TODO...
        }

        /// <inheritdoc/>
        //public async Task RunQueueListener(string experimentFolder, string MnistFolderFromBlobStorage, string outputFolderBlobStorage, BlobContainerClient blobStorageName, CancellationToken cancelToken)
        public async Task RunQueueListener(CancellationToken cancelToken)
        {
            CloudQueue queue = await CreateQueueAsync(config);
            BlobContainerClient blobStorageName = await AzureStorageProvider.CreateBlobStorage(config);
            QueueMessageRequirements();

            while (cancelToken.IsCancellationRequested == false)
            {
                CloudQueueMessage message = await queue.GetMessageAsync();
                if (message != null)
                {
                    try
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($">> Received the Queue Message:");
                        Console.WriteLine($"{message.AsString}\n");
                        Console.ResetColor();

                        //---------------------  READ QUEUE MESSAGE ---------------------------
                        ExerimentRequestMessage msg = null;
                        ExperimentResult result = null;

                        msg = JsonConvert.DeserializeObject<ExerimentRequestMessage>(message.AsString);

                        Thread.Sleep(10);

                        if (CheckMessageOK(msg))
                        {
                            //------------------------------------RUN EXPERIMENT--------------------------------------
                            result = await this.Run(msg, blobStorageName);
                            //----------------------------------------------------------------------------------------

                            ////---------------------------DOWNLOAD FILE FROM BLOB STORAGE------------------------------
                            //inputFileParameter = await this.storageProvider.DownloadInputFile(msg.InputFile, trainingcontainer);
                            ////----------------------------------------------------------------------------------------

                            //if (inputFileParameter != null)
                            //{
                            //    if (CheckInputParamOK(inputFileParameter))
                            //    {
                            //        Console.ForegroundColor = ConsoleColor.Yellow;
                            //        Console.WriteLine($">> EXPERIMENT IS RUNNING ...");

                            //        //------------------------------------RUN EXPERIMENT--------------------------------------
                            //        result = await this.Run(inputFileParameter, msg);
                            //        //----------------------------------------------------------------------------------------

                            //        if (result != null)
                            //        {
                            //            //---------------------------UPLOAD FILES ONTO BLOB STORAGE-------------------------------
                            //            await storageProvider.UploadResultFile(result);
                            //            //----------------------------------------------------------------------------------------

                            //            //------------------------UPLOAD RESULT FILES TO TABLE STORAGE----------------------------
                            //            await storageProvider.UploadExperimentResult(result);
                            //            //----------------------------------------------------------------------------------------

                            //            Console.ForegroundColor = ConsoleColor.DarkGreen;
                            //            Console.WriteLine($">> EXPERIMENT FINISHED!!!\n");
                            //            Console.ResetColor();
                            //        }
                            //        else
                            //        {
                            //            Console.ForegroundColor = ConsoleColor.DarkRed;
                            //            Console.WriteLine($">> EXPERIMENT FAILED!!!\n");
                            //            Console.ResetColor();
                            //        }
                            //    }

                            //    else
                            //    {
                            //        Console.ForegroundColor = ConsoleColor.DarkRed;
                            //        Console.WriteLine($">> Invalid Parameters in InputFile '{msg.InputFile}'.\n");
                            //        Console.ResetColor();
                            //    }
                            //}

                            //else
                            //{
                            //    Console.ForegroundColor = ConsoleColor.DarkRed;
                            //    Console.WriteLine($">> Container '{config.TrainingContainer}' does not have InputFile '{msg.InputFile}'.");
                            //    Console.WriteLine($">> Please upload the InputFile '{msg.InputFile}' onto container '{config.TrainingContainer}'.\n");
                            //    Console.ResetColor();
                            //}
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.DarkRed;
                            Console.WriteLine($">> Invalid input Queue Message.\n");
                            Console.ResetColor();
                        }

                        //---------------------- DELETE QUEUE MESSAGE ---------------------------
                        await queue.DeleteMessageAsync(message);
                        //-----------------------------------------------------------------------

                        Console.WriteLine($">> Queue Message was deleted.");
                        Console.WriteLine($"=====================================================================================================================");
                        Console.WriteLine($">> Waiting for Queue Message ...");
                        //QueueMessageRequirements();
                    }

                    catch (Exception ex)
                    {
                        this.logger?.LogError(ex, ">> Queue Message Error ...");
                        Thread.Sleep(10);
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine($"\n>> Invalid input Queue Message.");
                        Console.ResetColor();

                        //---------------------- DELETE QUEUE MESSAGE ---------------------------
                        await queue.DeleteMessageAsync(message);
                        //-----------------------------------------------------------------------

                        Console.WriteLine($"\n>> Queue Message was deleted.");
                        Console.WriteLine($"=====================================================================================================================");
                        Console.WriteLine($">> Waiting for Queue Message ...");
                        QueueMessageRequirements();
                    }
                }
                else
                    await Task.Delay(1000);
            }

            this.logger?.LogInformation(">> Cancel pressed. Exiting the listener loop.");
        }


        #region Private Methods

        private void QueueMessageRequirements()
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine(
                " +=================================================+\n" +
                " +         SPECIFICATION FOR QUEUE MESSAGE         +\n" +
                " +  {                                              +\n" +
                " +      'IMAGE_WIDTH': number,                     +\n" +
                " +      'IMAGE_HEIGHT': number,                    +\n" +
                " +      'FRAME_WIDTH': number,                     +\n" +
                " +      'FRAME_HEIGHT': number,                    +\n" +
                " +      'PIXEL_SHIFTED': number,                   +\n" +
                " +      'MAX_CYCLE': number,                       +\n" +
                " +      'NUM_IMAGES_PER_LABEL': number,            +\n" +
                " +      'PER_TESTSET': number,                     +\n" +
                " +  }                                              +\n" +
                " +=================================================+\n" +
                " +     UNDESIRED QUEUE MESSAGE WILL BE DELETED     +\n" +
                " +=================================================+\n");
            Console.ResetColor();
        }

        private bool CheckMessageOK(ExerimentRequestMessage msg)
        {
            bool valid;
            valid = (msg.IMAGE_WIDTH != null
                && msg.IMAGE_HEIGHT != null
                && msg.FRAME_WIDTH != null
                && msg.FRAME_HEIGHT != null
                && msg.PIXEL_SHIFTED != null
                && msg.MAX_CYCLE != null
                && msg.NUM_IMAGES_PER_LABEL != null
                && msg.PER_TESTSET != null);
            return valid;
        }

        //private bool CheckInputParamOK(InputFileParameters inputFileParameter)
        //{
        //    bool valid;

        //    valid = ((inputFileParameter.Mode != null) && (inputFileParameter.Mode.ToUpper() == "CSI" || inputFileParameter.Mode.ToUpper() == "SPI") && inputFileParameter.iteration > 0 && (inputFileParameter.minVal != 0 || inputFileParameter.maxVal != 0));
        //    return valid;
        //}

        /// <summary>
        /// Create a queue for the sample application to process messages in. 
        /// </summary>
        /// <returns>A CloudQueue object</returns>
        private static async Task<CloudQueue> CreateQueueAsync(MyConfig config)
        {
            // Retrieve storage account information from connection string.
            CloudStorageAccount storageAccount = AzureStorageProvider.CreateAzureStorageAccountFromConnectionString(config.StorageConnectionString);

            // Create a queue client for interacting with the queue service
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();

            CloudQueue queue = queueClient.GetQueueReference(config.Queue);
            try
            {
                if (await queue.CreateIfNotExistsAsync())
                {
                    Console.WriteLine($">> Queue Client '{config.Queue}' created!");
                }
                else
                {
                    Console.WriteLine($">> Queue Client '{config.Queue}' already exists!");
                }
            }
            catch
            {
                Console.WriteLine(">> If you are running with the default configuration please make sure you have started the storage emulator. Press the Windows key and type Azure Storage to select and run it from the list of applications - then restart the sample.");
                Console.ReadLine();
                throw;
            }

            return queue;
        }

        #endregion
    }
}
