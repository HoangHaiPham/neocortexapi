﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoCortexApi;
using NeoCortexApi.Entities;
using NeoCortexEntities.NeuroVisualizer;


namespace UnitTestsProject
{
    //DONE: All test should have a prefix 'Serializationtest_TESTNAME'
    //DONE:[DataRow] Added more different params to be sure that the serialization works well.
    //DONE: Implement HtmConfigTests, HtmModuleTopologyTests, ProximalDentriteTests, TopologyTests, ComputeCycleTests that make sure the .Equals() Method works well.
    [TestClass]
    public class HTMSerializationTests_P3
    {
        /// <summary>
        /// TODO: ALL TEST MUST BE WELL COMMENTED
        /// </summary>
        [TestMethod]
        [TestCategory("serialization")]
        [DataRow(1, 2, 1.0, 1)]
        [DataRow(2, 5, 18.3, 20)]
        [DataRow(10, 25, 12.0, 100)]
        [DataRow(12, 14, 18.7, 1000)]
        public void Serializationtest_COLUMN(int numCells, int colIndx, double synapsePermConnected, int numInputs)
        {
            HtmSerializer2 serializer = new HtmSerializer2();

            Column column = new Column(numCells, colIndx, synapsePermConnected, numInputs);
            
            using (StreamWriter sw = new StreamWriter($"ser_{nameof(Serializationtest_COLUMN)}_column.txt"))
            {
                HtmSerializer2.Serialize(column, null, sw);
            }

            using (StreamReader sr = new StreamReader($"ser_{nameof(Serializationtest_COLUMN)}_column.txt"))
            {
                var columnD = HtmSerializer2.Deserialize<Column>(sr);
                Assert.IsTrue(column.Equals(columnD));
            }
        }


        [TestMethod]
        [TestCategory("serialization")]
        [DataRow(new int[] { 100, 100}, true)]
        [DataRow(new int[] { 10, 100, 1000 }, true)]
        [DataRow(new int[] { 12, 14, 16, 18 }, false)]
        [DataRow(new int[] { 100, 1000, 10000, 100000, 1000000 }, false)]
        public void Serializationtest_SPARSEBINARYMATRIXS(int[] dimensions, bool useColumnMajorOrdering)
        {
            HtmSerializer2 serializer = new HtmSerializer2();

            SparseBinaryMatrix matrix = new SparseBinaryMatrix(dimensions,useColumnMajorOrdering);            

            using (StreamWriter sw = new StreamWriter($"ser_{nameof(Serializationtest_SPARSEBINARYMATRIXS)}_sbmatrix.txt"))
            {
                HtmSerializer2.Serialize(matrix, null, sw);
            }

            using (StreamReader sr = new StreamReader($"ser_{nameof(Serializationtest_SPARSEBINARYMATRIXS)}_sbmatrix.txt"))
            {
                var matrixD = HtmSerializer2.Deserialize<SparseBinaryMatrix>(sr);
                Assert.IsTrue(matrix.Equals(matrixD));
            }
        }

        [TestMethod]
        [TestCategory("serialization")]
        [DataRow(new int[] { 100, 100 }, new int[] { 10, 10 }, 12, 14, 16, 1, 2, 2, 2.0, 100)]
        [DataRow(new int[] { 100, 100 }, new int[] { 10, 10 }, 100, 256, 1000, 10, 20, 20, 1.0, 100)]
        [DataRow(new int[] { 2, 4, 8 }, new int[] { 128, 256, 512 }, 12, 14, 16, 1, 4, 8, 4.0, 1000)]
        [DataRow(new int[] { 2, 4, 8 }, new int[] { 128, 256, 512 }, 1, 1, 2, 1, 2, 2, 2.0, 100)]
        public void Serializationtest_CONNECTIONS(int[] inputDims, int[] columnDims, int parentColumnIndx, int colSeq, int numCellsPerColumn,
            int flatIdx, long lastUsedIteration, int ordinal, double synapsePermConnected, int numInputs)
        {
            HtmConfig config = new HtmConfig(inputDims, columnDims);

            Connections connections = new Connections(config);

            Cell cell = new Cell(parentColumnIndx, colSeq, numCellsPerColumn, new CellActivity());

            var distDend = new DistalDendrite(cell, 1, 2, 2, 1.0, 100);

            connections.ActiveSegments.Add(distDend);

            using (StreamWriter sw = new StreamWriter($"ser_{nameof(Serializationtest_CONNECTIONS)}_connections.txt"))
            {
                HtmSerializer2.Serialize(connections, null, sw);
            }
            using (StreamReader sr = new StreamReader($"ser_{nameof(Serializationtest_CONNECTIONS)}_connections.txt"))
            {
                Connections connectionsD = HtmSerializer2.Deserialize<Connections>(sr);
                Assert.IsTrue(connections.Equals(connectionsD));
            }
        }

        [TestMethod]
        [TestCategory("serialization")]
        [DataRow(new int[] { 8000 }, new int[] { 100 })]
        [DataRow(new int[] { 100, 100 }, new int[] { 10, 10 })]
        [DataRow(new int[] { 2, 4, 8 }, new int[] { 128, 256, 512 })]
        [DataRow(new int[] { 256 }, new int[] { 10, 15 })]
        public void Serializationtest_HTMCONFIG(int[] inputDims, int[] columnDims)
        {
            HtmConfig config = new HtmConfig(inputDims, columnDims);

            using (StreamWriter sw = new StreamWriter($"ser_{nameof(Serializationtest_HTMCONFIG)}_config.txt"))
            {
                HtmSerializer2.Serialize(config, null, sw);
            }
            using (StreamReader sr = new StreamReader($"ser_{nameof(Serializationtest_HTMCONFIG)}_config.txt"))
            {
                HtmConfig configD = HtmSerializer2.Deserialize<HtmConfig>(sr);
                Assert.IsTrue(config.Equals(configD));                 
            }
        }      

        [TestMethod]
        [TestCategory("serialization")]
        [DataRow(1, 2, 4, 1, 2, 2, 2.0, 100)]
        [DataRow(11, 12, 22, 10, 20, 20, 1.0, 100)]
        [DataRow(12, 14, 16, 1, 4, 8, 4.0, 1000)]
        [DataRow(100, 200, 400, 10, 20, 20, 20.0, 1000)]
        public void Serializationtest_DISTALDENDRITE(int parentColumnIndx, int colSeq, int numCellsPerColumn,int flatIdx, long lastUsedIteration, int ordinal, double synapsePermConnected, int numInputs)
        {
            Cell cell = new Cell(parentColumnIndx, colSeq, numCellsPerColumn, new CellActivity());
            DistalDendrite distalDendrite = new DistalDendrite(cell, flatIdx, lastUsedIteration, ordinal, synapsePermConnected, numInputs);

            using (StreamWriter sw = new StreamWriter($"ser_{nameof(Serializationtest_DISTALDENDRITE)}_dd.txt"))
            {
                HtmSerializer2.Serialize(distalDendrite, null, sw);
            }
            using (StreamReader sr = new StreamReader($"ser_{nameof(Serializationtest_DISTALDENDRITE)}_dd.txt"))
            {
                DistalDendrite distalDendriteD = HtmSerializer2.Deserialize<DistalDendrite>(sr);
                Assert.IsTrue(distalDendrite.Equals(distalDendriteD));
            }
        }


        [TestMethod]
        [TestCategory("serialization")]
        [DataRow(1, 1, 1.0, 1)]        
        [DataRow(2, 5, 8.3, 2)]
        [DataRow(10, 25, 10.0, 100)]
        [DataRow(12, 14, 8.7, 1000)]
        public void Serializationtest_DISTRIBUTEDMEMORY(int numCells, int colIndx, double synapsePermConnected, int numInputs)
        {
            Column column = new Column(numCells, colIndx, synapsePermConnected, numInputs);

            DistributedMemory distributedMemory = new DistributedMemory();

            distributedMemory.ColumnDictionary.Add(1, column);

            using (StreamWriter sw = new StreamWriter($"ser_{nameof(Serializationtest_DISTRIBUTEDMEMORY)}_dm.txt"))
            {
                HtmSerializer2.Serialize(distributedMemory, null, sw);
            }
            using (StreamReader sr = new StreamReader($"ser_{nameof(Serializationtest_DISTRIBUTEDMEMORY)}_dm.txt"))
            {
                DistributedMemory distributedMemoryD = HtmSerializer2.Deserialize<DistributedMemory>(sr);
                Assert.IsTrue(distributedMemory.Equals(distributedMemoryD));
            }
        }

        
        [TestMethod]
        [TestCategory("serialization")]
        [DataRow(new int[] {1, 2, 4}, true)]
        [DataRow(new int[] {10, 12, 14}, false)]
        [DataRow(new int[] { 1028 }, true)]
        [DataRow(new int[] { 100, 1000, 10000, 100000 }, false)]
        public void Serializationtest_HTMMODULETOPOLOGY(int[] dimension, bool isMajorOrdering)
        {
            
            HtmModuleTopology topology = new HtmModuleTopology(dimension, isMajorOrdering);

            using (StreamWriter sw = new StreamWriter($"ser_{nameof(Serializationtest_HTMMODULETOPOLOGY)}_topology.txt"))
            {
                HtmSerializer2.Serialize(topology, null, sw);
            }
            using (StreamReader sr = new StreamReader($"ser_{nameof(Serializationtest_HTMMODULETOPOLOGY)}_topology.txt"))
            {
                HtmModuleTopology topologyD = HtmSerializer2.Deserialize<HtmModuleTopology>(sr);
                Assert.IsTrue(topology.Equals(topologyD));
            }
        }

        
        //Currently fail because the created proDent's Synapses is an empty list (after added Pool). The Deserialize object is correct.
        //Equal() method tested.
        [TestMethod]
        [TestCategory("serialization")]
        public void Serializationtest_PROXIMALDENTRITE()
        {
            Pool rfPool = new Pool(size: 2, numInputs: 100);

            Cell cell = new Cell(parentColumnIndx: 1, colSeq: 20, numCellsPerColumn: 16, new CellActivity());
            Cell preSynapticCell = new Cell(parentColumnIndx: 2, colSeq: 22, numCellsPerColumn: 26, new CellActivity());

            DistalDendrite dd = new DistalDendrite(parentCell: cell, flatIdx: 10, lastUsedIteration: 20, ordinal: 10, synapsePermConnected: 15, numInputs: 100);
            cell.DistalDendrites.Add(dd);

            Synapse synapse = new Synapse(presynapticCell: cell, distalSegmentIndex: dd.SegmentIndex, synapseIndex: 23, permanence: 1.0);
            preSynapticCell.ReceptorSynapses.Add(synapse);

            rfPool.m_SynapsesBySourceIndex = new Dictionary<int, Synapse>();
            rfPool.m_SynapsesBySourceIndex.Add(1, synapse);

            int colIndx = 10;
            double synapsePermConnected = 20.5;
            int numInputs = 30;

            ProximalDendrite proDend = new ProximalDendrite(colIndx, synapsePermConnected, numInputs);
            proDend.RFPool = rfPool;

            using (StreamWriter sw = new StreamWriter($"ser_{nameof(Serializationtest_PROXIMALDENTRITE)}_prodent.txt"))
            {
                HtmSerializer2.Serialize(proDend, null, sw);
            }
            using (StreamReader sr = new StreamReader($"ser_{nameof(Serializationtest_PROXIMALDENTRITE)}_prodent.txt"))
            {
                ProximalDendrite proDendD = HtmSerializer2.Deserialize<ProximalDendrite>(sr);
                Assert.IsTrue(proDend.Equals(proDendD));
            }
        }


        [TestMethod]
        [TestCategory("serialization")]
        public void Serializationtest_SYNAPSE()
        {  
            Cell cell = new Cell(parentColumnIndx: 1, colSeq: 20, numCellsPerColumn: 16, new CellActivity());
            Cell presynapticCell = new Cell(parentColumnIndx: 8, colSeq: 36, numCellsPerColumn: 46, new CellActivity());

            DistalDendrite dd = new DistalDendrite(parentCell: cell, flatIdx: 10, lastUsedIteration: 20, ordinal: 10, synapsePermConnected: 15, numInputs: 100);
            cell.DistalDendrites.Add(dd);

            Synapse synapse = new Synapse(presynapticCell: cell, distalSegmentIndex: dd.SegmentIndex, synapseIndex: 23, permanence: 1.0);
            presynapticCell.ReceptorSynapses.Add(synapse);
           
            using (StreamWriter sw = new StreamWriter($"ser_{nameof(Serializationtest_SYNAPSE)}_synapse.txt"))
            {
                HtmSerializer2.Serialize(synapse, null, sw);
            }
            using (StreamReader sr = new StreamReader($"ser_{nameof(Serializationtest_SYNAPSE)}_synapse.txt"))
            {
                Synapse synapseD = HtmSerializer2.Deserialize<Synapse>(sr);
                Assert.IsTrue(synapse.Equals(synapseD));
            }
        }

        
        [TestMethod]
        [TestCategory("serialization")]
        [DataRow(new int[] { 1, 2, 4 }, true)]
        [DataRow(new int[] { 10, 12, 14 }, false)]
        [DataRow(new int[] { 1028 }, true)]
        [DataRow(new int[] { 100, 1000, 10000, 100000 }, false)]
        public void Serializationtest_TOPOLOGY(int[] shape, bool useColumnMajorOrdering)
        {
            Topology topology = new Topology(shape, useColumnMajorOrdering);

            using (StreamWriter sw = new StreamWriter($"ser_{nameof(Serializationtest_TOPOLOGY)}_topology.txt"))
            {
                HtmSerializer2.Serialize(topology, null, sw);
            }
            using (StreamReader sr = new StreamReader($"ser_{nameof(Serializationtest_TOPOLOGY)}_topology.txt"))
            {
                Topology topologyD = HtmSerializer2.Deserialize<Topology>(sr);
                Assert.IsTrue(topology.Equals(topologyD));
            }
        }


        //Currently fail. Deserialize object is not correct.
        [TestMethod]
        [TestCategory("serialization")]
        public void Serializationtest_HOMEOSTATICPLASTICITYCONTROLLER()
        {
            int[] inputDims = { 100, 100 };
            int[] columnDims = { 10, 10 };
            HtmConfig config = new HtmConfig(inputDims, columnDims);

            Connections htmMemory = new Connections();
            int minCycles = 50;
            Action<bool, int, double, int> onStabilityStatusChanged = (isStable, numPatterns, actColAvg, seenInputs) => { };
            int numOfCyclesToWaitOnChange = 50;
            double requiredSimilarityThreshold = 0.97;

            HomeostaticPlasticityController controller = new HomeostaticPlasticityController(htmMemory, minCycles, onStabilityStatusChanged, numOfCyclesToWaitOnChange, requiredSimilarityThreshold);
                        
            using (StreamWriter sw = new StreamWriter($"ser_{nameof(Serializationtest_HOMEOSTATICPLASTICITYCONTROLLER)}_hpc.txt"))
            {
                HtmSerializer2.Serialize(controller, null, sw);
            }
            using (StreamReader sr = new StreamReader($"ser_{nameof(Serializationtest_HOMEOSTATICPLASTICITYCONTROLLER)}_hpc.txt"))
            {
                HomeostaticPlasticityController controllerD = HtmSerializer2.Deserialize<HomeostaticPlasticityController>(sr);
                Assert.IsTrue(controller.Equals(controllerD));
            }
        }

        [TestMethod]
        [TestCategory("serialization")]
        public void Serializationtest_SPARSEOBJECTMATRIX()
        {
            int[] dimensions = { 10, 20, 30 };
            bool useColumnMajorOrdering = false;

            SparseObjectMatrix<Column> matrix = new SparseObjectMatrix<Column>(dimensions, useColumnMajorOrdering, dict: null);

            // TODO: This test must initialize a full set of columns.

            /*for (int i = 0; i < numColumns; i++)
            {
                Column column = colZero == null ?
                    new Column(cellsPerColumn, i, this.connections.HtmConfig.SynPermConnected, this.connections.HtmConfig.NumInputs) : matrix.GetObject(i);

                for (int j = 0; j < cellsPerColumn; j++)
                {
                    cells[i * cellsPerColumn + j] = column.Cells[j];
                }
                //If columns have not been previously configured
                if (colZero == null)
                    matrix.set(i, column);

            }*/


            using (StreamWriter sw = new StreamWriter($"ser_{nameof(Serializationtest_SPARSEOBJECTMATRIX)}_hpc.txt"))
            {
                HtmSerializer2.Serialize(matrix, null, sw);
            }
            using (StreamReader sr = new StreamReader($"ser_{nameof(Serializationtest_SPARSEOBJECTMATRIX)}_hpc.txt"))
            {
                SparseObjectMatrix<Column> matrixD = HtmSerializer2.Deserialize<SparseObjectMatrix<Column>>(sr);
                Assert.IsTrue(matrix.Equals(matrixD));
            }
        }

        [TestMethod]
        [TestCategory("serialization")]
        public void Serializationtest_POOL()
        {
            Pool pool = new Pool(size: 1, numInputs: 200);

            Cell cell = new Cell(parentColumnIndx: 1, colSeq: 20, numCellsPerColumn: 16, new CellActivity());
            Cell preSynapticCell = new Cell(parentColumnIndx: 2, colSeq: 22, numCellsPerColumn: 26, new CellActivity());

            DistalDendrite dd = new DistalDendrite(parentCell: cell, flatIdx: 10, lastUsedIteration: 20, ordinal: 10, synapsePermConnected: 15, numInputs: 100);
            cell.DistalDendrites.Add(dd);

            Synapse synapse = new Synapse(presynapticCell: cell, distalSegmentIndex: dd.SegmentIndex, synapseIndex: 23, permanence: 1.0);
            preSynapticCell.ReceptorSynapses.Add(synapse);

            pool.m_SynapsesBySourceIndex = new Dictionary<int, Synapse>();
            pool.m_SynapsesBySourceIndex.Add(2, synapse);

            using (StreamWriter sw = new StreamWriter($"ser_{nameof(Serializationtest_POOL)}_pool.txt"))
            {
                HtmSerializer2.Serialize(pool, null, sw);
            }
            using (StreamReader sr = new StreamReader($"ser_{nameof(Serializationtest_POOL)}_pool.txt"))
            {
                Pool poolD = HtmSerializer2.Deserialize<Pool>(sr);
                Assert.IsTrue(pool.Equals(poolD));
            }
        }

        //Test passed. Equal method of ComputeCycle object fixed.
        [TestMethod]
        [TestCategory("serialization")]
        public void Serializationtest_COMPUTECYCLE()
        {
            int[] inputDims = { 100, 100 };
            int[] columnDims = { 10, 10 };
            HtmConfig config = new HtmConfig(inputDims, columnDims);

            Connections connections = new Connections(config);

            Cell cell = new Cell(12, 14, 16, new CellActivity());

            var distDend = new DistalDendrite(cell, 1, 2, 2, 1.0, 100);

            connections.ActiveSegments.Add(distDend);

            ComputeCycle computeCycle = new ComputeCycle(connections);

            using (StreamWriter sw = new StreamWriter($"ser_{nameof(Serializationtest_COMPUTECYCLE)}_compute.txt"))
            {
                HtmSerializer2.Serialize(computeCycle, null, sw);
            }
            using (StreamReader sr = new StreamReader($"ser_{nameof(Serializationtest_COMPUTECYCLE)}_compute.txt"))
            {
                ComputeCycle computeCycleD = HtmSerializer2.Deserialize<ComputeCycle>(sr);
                Assert.IsTrue(computeCycle.Equals(computeCycleD)); 
            }
        }

        
    }
}