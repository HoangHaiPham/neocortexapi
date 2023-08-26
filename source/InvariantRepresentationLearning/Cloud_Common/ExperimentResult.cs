﻿using System;
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

        public string Name { get; set; }
        public string Description { get; set; }
        public string InputFileName { get; set; }
        public DateTime StartTimeUtc { get; set; }
        public DateTime EndTimeUtc { get; set; }
        public long DurationSec { get; set; }
        public string FilePathscalarEncoder { get; set; }
        public string FilePathFreshArray { get; set; }
        public string FilePathDiffArray { get; set; }
        public string FilePathHammingOutput { get; set; }
        public string FilePathHammingBitmap { get; set; }
    }
}
