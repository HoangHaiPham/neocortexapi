using System.Threading.Tasks;
using Azure.Storage.Blobs;

namespace Cloud_Common
{
    public interface IStorageProvider
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName">The name of the local file where the input is downloaded.</param>
        /// <returns></returns>

        Task<InputFileParameters> DownloadInputFile(string fileName, BlobContainerClient trainingcontainer);

        Task UploadResultFile(ExperimentResult result);

        Task UploadExperimentResult(ExperimentResult result);

    }
}
