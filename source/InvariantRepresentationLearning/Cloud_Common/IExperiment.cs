namespace Cloud_Common
{
    public interface IExperiment
    {
        /// <summary>
        /// Runs experiment.
        /// </summary>
        /// <param name="inputFile"></param>
        /// <returns></returns>
        Task<ExperimentResult> Run(ExerimentRequestMessage msg);

        /// <summary>
        /// Starts the listening for incomming messages, which will trigger the experiment.
        /// </summary>
        /// <param name="cancelToken">Token used to cancel the listening process.</param>
        /// <returns></returns>
        Task RunQueueListener(CancellationToken cancelToken);

    }
}
