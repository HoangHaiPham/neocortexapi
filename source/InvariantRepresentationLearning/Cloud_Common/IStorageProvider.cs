using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos.Table;

namespace Cloud_Common
{
    public interface IStorageProvider
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName">The name of the local file where the input is downloaded.</param>
        /// <returns></returns>

        Task UploadExperimentResult(ExperimentResult result);

        Task UploadFolderToBlogStorage(BlobContainerClient blobStorageName, string outputFolderBlobStorage, string localFolder);

        Task UploadFileToBlobStorage(BlobContainerClient blobStorageName, string cloudExperimentOutputFolder, string localFilePath);

        Task GetMnistDatasetFromBlobStorage(BlobContainerClient blobStorageName, string MnistFolderFromBlobStorage);

        Task<CloudTable> CreateTableAsync(string tableName);

        Task<ExperimentResult> InsertOrMergeEntityAsync(CloudTable table, ExperimentResult result);

        Task<BlobContainerClient> CreateBlobStorage(string StorageConnectionString, string ContainerName);

        CloudStorageAccount CreateStorageAccountFromConnectionString(string storageConnectionString);
    }
}
