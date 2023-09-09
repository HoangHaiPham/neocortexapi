using Microsoft.Azure.Cosmos.Table;

namespace Cloud_Common
{
    public class ExperimentResult : TableEntity
    {
        public ExperimentResult(string partitionKey, string rowKey)
        {
            this.PartitionKey = partitionKey;
            this.RowKey = rowKey;
        }
 
        public DateTime StartTimeUtc { get; set; }
        public DateTime EndTimeUtc { get; set; }
        public long DurationSec { get; set; }
        public string outputFolderBlobStorage { get; set; }
        public string trainingImage_FolderName { get; set; }
        public string testSetBigScale_FolderName { get; set; }
        public string logResult_FileName { get; set; }
        public string accuracy { get; set; }
    }
}
