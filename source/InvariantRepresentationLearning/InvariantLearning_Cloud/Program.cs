using NeoCortexApi.Entities;
using System.Diagnostics;
using Invariant.Entities;
using Microsoft.Extensions.Logging;
using Cloud_Common;
using Cloud_Experiment;
using InvariantLearning_Utilities;
using Microsoft.Extensions.Configuration;

//using Experiment;

namespace InvariantLearning_FrameCheck
{
    public class InvariantLearning
    {

        private static string projectName = "Thesis - HoangHaiPham - Invariant Representation";

        //public static void Main()
        static async Task Main(string[] args)
        {
            CancellationTokenSource tokeSrc = new CancellationTokenSource();

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                tokeSrc.Cancel();
            };

            //init configuration
            var cfgRoot = Cloud_Common.InitHelpers.InitConfiguration(args);

            var cfgSec = cfgRoot.GetSection("MyConfig");
            var config = new MyConfig();
            cfgSec.Bind(config);

            // InitLogging
            var logFactory = InitHelpers.InitLogging(cfgRoot);
            var logger = logFactory.CreateLogger("Train.Console");

            logger?.LogInformation($"{DateTime.Now} -  Started experiment: {projectName}");

            IStorageProvider storageProvider = new AzureStorageProvider(cfgSec);

            Experiment experiment = new Experiment(config, storageProvider, logger/* put some additional config here */);

            experiment.RunQueueListener(tokeSrc.Token).Wait();

            logger?.LogInformation($"{DateTime.Now} -  Experiment exit: {projectName}");
        }
    }
}
