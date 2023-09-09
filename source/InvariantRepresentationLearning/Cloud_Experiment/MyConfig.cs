namespace Cloud_Experiment
{
    /// <summary>
    /// This is the class which describe the project configuration.
    /// </summary>
    public class MyConfig
    {
        public string StorageConnectionString { get; set; }

        /// <summary>
        /// The name of the container in the blob storage, where training files are located.
        /// </summary>
        public string MnistDataContainer { get; set; }

        /// <summary>
        /// The name of the container in the blob storage, where result files will be uploaded.
        /// </summary>
        public string ResultContainer { get; set; }

        /// <summary>
        /// The name of the table where result information will be uploaded.
        /// </summary>
        public string ResultTable { get; set; }

        /// <summary>
        /// The name of the queue used to trigger the computation.
        /// </summary>
        public string Queue { get; set; }
    }
}
