using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using SharpNEATLib.NeatGenome;
using SharpNEATLib.NetworkVisualization;
using SharpNEATLib.NeuralNetwork;

namespace SharpNEATLib
{
	/// <summary>
	/// By placing decode routines in a seperate class we decouple the Genome and Network classes.
	/// Ideally this would be achieved by using intermediate generic data structures, however that 
	/// approach can cause a performance hit. This is a nice balance that allows decoupling without
	/// performance loss. The downside is that we need knowledge of the Network code's 'guts' in order
	/// to construct them.
	/// </summary>
	public class GenomeDecoder
	{
		#region Decode To ConcurrentNetwork

		static public INetwork DecodeToConcurrentNetwork(NeatGenome.NeatGenome g, IActivationFunction activationFn)
		{
		//----- Loop the neuronGenes. Create Neuron for each one.
			// Store a table of neurons keyed by their id.
			Hashtable neuronTable = new Hashtable(g.NeuronGeneList.Count);
			NeuronList neuronList = new NeuronList();

			foreach(NeuronGene neuronGene in g.NeuronGeneList)
			{
				Neuron newNeuron = new Neuron(activationFn, neuronGene.NeuronType, neuronGene.InnovationId);
				neuronTable.Add(newNeuron.Id, newNeuron);
				neuronList.Add(newNeuron);
			}

		//----- Loop the connection genes. Create a Connection for each one and bind them to the relevant Neurons.
			foreach(ConnectionGene connectionGene in g.ConnectionGeneList)
			{
				Connection newConnection = new Connection(connectionGene.SourceNeuronId, connectionGene.TargetNeuronId, connectionGene.Weight);

				// Bind the connection to it's source neuron.
				newConnection.SetSourceNeuron((Neuron)neuronTable[connectionGene.SourceNeuronId]);

				// Store the new connection against it's target neuron.
				((Neuron)(neuronTable[connectionGene.TargetNeuronId])).ConnectionList.Add(newConnection);
			}

			return new ConcurrentNetwork(neuronList);
		}

		#endregion

		#region Decode To FastConcurrentNetwork

		/// <summary>
		/// Create a single comparer to limit the need to reconstruct for each network. 
		/// Not multithread safe!
		/// </summary>
		//static FastConnectionComparer fastConnectionComparer = new FastConnectionComparer();		
		static FloatFastConnection[] fastConnectionArray;
        static IActivationFunction[] activationFunctionArray;

		static public FloatFastConcurrentNetwork DecodeToFloatFastConcurrentNetwork(NeatGenome.NeatGenome g, IActivationFunction activationFn)
		{			
			int outputNeuronCount = g.OutputNeuronCount;
			int neuronGeneCount = g.NeuronGeneList.Count;

			// Slightly inefficient - determine the number of bias nodes. Fortunately there is not actually
			// any reason to ever have more than one bias node - although there may be 0.

            activationFunctionArray = new IActivationFunction[neuronGeneCount];
            
            int neuronGeneIdx=0;
			for(; neuronGeneIdx<neuronGeneCount; neuronGeneIdx++)
			{
                activationFunctionArray[neuronGeneIdx] = g.NeuronGeneList[neuronGeneIdx].ActivationFunction;
				if(g.NeuronGeneList[neuronGeneIdx].NeuronType != NeuronType.Bias)
					break;
			}
			int biasNodeCount = neuronGeneIdx;
			int inputNeuronCount = g.InputNeuronCount;
            for (; neuronGeneIdx < neuronGeneCount; neuronGeneIdx++)
            {
                activationFunctionArray[neuronGeneIdx] = g.NeuronGeneList[neuronGeneIdx].ActivationFunction;
            }
			
			// ConnectionGenes point to a neuron ID. We need to map this ID to a 0 based index for
			// efficiency. 
			
			// Use a quick heuristic to determine which will be the fastest technique for mapping the connection end points
			// to neuron indexes. This is heuristic is not 100% perfect but has been found to be very good in in real word 
			// tests. Feel free to perform your own calculation and create a more intelligent heuristic!
			int connectionCount=g.ConnectionGeneList.Count;
			if(neuronGeneCount * connectionCount < 45000)
			{	
				fastConnectionArray = new FloatFastConnection[connectionCount];
				int connectionIdx=0;
				for(int connectionGeneIdx=0; connectionGeneIdx<connectionCount; connectionGeneIdx++)
				{
                    //fastConnectionArray[connectionIdx] = new FloatFastConnection();
                    //Note. Binary search algorithm assume that neurons are ordered by their innovation Id.
					ConnectionGene connectionGene = g.ConnectionGeneList[connectionIdx];
					fastConnectionArray[connectionIdx].sourceNeuronIdx = (int)g.NeuronGeneList.BinarySearch(connectionGene.SourceNeuronId);
					fastConnectionArray[connectionIdx].targetNeuronIdx = (int)g.NeuronGeneList.BinarySearch(connectionGene.TargetNeuronId);

					System.Diagnostics.Debug.Assert(fastConnectionArray[connectionIdx].sourceNeuronIdx>=0 && fastConnectionArray[connectionIdx].targetNeuronIdx>=0, "invalid idx");

					fastConnectionArray[connectionIdx].weight = (float)connectionGene.Weight;
                    fastConnectionArray[connectionIdx].learningRate = connectionGene.learningRate;
                    fastConnectionArray[connectionIdx].A = connectionGene.A;
                    fastConnectionArray[connectionIdx].B = connectionGene.B;
                    fastConnectionArray[connectionIdx].C = connectionGene.C;

					connectionIdx++;
				}
			}
			else
			{
				// Build a table of indexes (ints) keyed on neuron ID. This approach is faster when dealing with large numbers 
				// of lookups.
				Hashtable neuronIndexTable = new Hashtable(neuronGeneCount);
				for(int i=0; i<neuronGeneCount; i++)
					neuronIndexTable.Add(g.NeuronGeneList[i].InnovationId, i);

				// Now we can build the connection array(s).
				//int connectionCount=g.ConnectionGeneList.Count;
				//FastConnection[] connectionArray = new FastConnection[connectionCount];
				fastConnectionArray = new FloatFastConnection[connectionCount];
				int connectionIdx=0;
				for(int connectionGeneIdx=0; connectionGeneIdx<connectionCount; connectionGeneIdx++)
				{
					ConnectionGene connectionGene = g.ConnectionGeneList[connectionIdx];
					fastConnectionArray[connectionIdx].sourceNeuronIdx = (int)neuronIndexTable[connectionGene.SourceNeuronId];
					fastConnectionArray[connectionIdx].targetNeuronIdx = (int)neuronIndexTable[connectionGene.TargetNeuronId];
					fastConnectionArray[connectionIdx].weight = (float)connectionGene.Weight;
                    fastConnectionArray[connectionIdx].learningRate = connectionGene.learningRate;
                    fastConnectionArray[connectionIdx].A = connectionGene.A;
                    fastConnectionArray[connectionIdx].B = connectionGene.B;
                    fastConnectionArray[connectionIdx].C = connectionGene.C;
					connectionIdx++;
				}
			}
			// Now sort the connection array on sourceNeuronIdx, secondary sort on targetNeuronIdx.
			// Using Array.Sort is 10 times slower than the hand-coded sorting routine. See notes on that routine for more 
			// information. Also note that in tests that this sorting did no t actually improve the speed of the network!
			// However, it may have a benefit for CPUs with small caches or when networks are very large, and since the new
			// sort takes up hardly any time for even large networks, it seems reasonable to leave in the sort.
			//Array.Sort(fastConnectionArray, fastConnectionComparer);
			//if(fastConnectionArray.Length>1)
			//	QuickSortFastConnections(0, fastConnectionArray.Length-1);
			
			return new FloatFastConcurrentNetwork(	biasNodeCount, inputNeuronCount, 
				outputNeuronCount, neuronGeneCount,
				fastConnectionArray, activationFunctionArray);
		}
		
		static public FastConcurrentMultiplicativeNetwork DecodeToFastConcurrentMultiplicativeNetwork(NeatGenome.NeatGenome g, IActivationFunction activationFn)
		{
			
			int outputNeuronCount = g.OutputNeuronCount;
			int neuronGeneCount = g.NeuronGeneList.Count;

			// Slightly inefficient - determine the number of bias nodes. Fortunately there is not actually
			// any reason to ever have more than one bias node - although there may be 0.
			int neuronGeneIdx=0;
			for(; neuronGeneIdx<neuronGeneCount; neuronGeneIdx++)
			{
				if(g.NeuronGeneList[neuronGeneIdx].NeuronType != NeuronType.Bias)
					break;
			}
			int biasNodeCount = neuronGeneIdx;
			int inputNeuronCount = g.InputNeuronCount;
			
			// ConnectionGenes point to a neuron ID. We need to map this ID to a 0 based index for
			// efficiency. To do this we build a table of indexes (ints) keyed on neuron ID.
			// TODO: An alternative here would be to forgo the building of a table and do a binary 
			// search directly on the NeuronGeneList - probably a good idea to use a heuristic based upon
			// neuroncount*connectioncount that decides on which technique to use. Small networks will
			// likely be faster to decode using the binary search.

			// Actually we can partly achieve the above optimzation by using HybridDictionary instead of Hashtable.
			// Although creating a table is a bit expensive.
			HybridDictionary neuronIndexTable = new HybridDictionary(neuronGeneCount);
			for(int i=0; i<neuronGeneCount; i++)
				neuronIndexTable.Add(g.NeuronGeneList[i].InnovationId, i);

			// Count how many of the connections are actually enabled. TODO: make faster - store disable count?
			int connectionGeneCount = g.ConnectionGeneList.Count;
			int connectionCount=connectionGeneCount;
			//			for(int i=0; i<connectionGeneCount; i++)
			//			{
			//				if(g.ConnectionGeneList[i].Enabled)
			//					connectionCount++;
			//			}

			// Now we can build the connection array(s).			
			FloatFastConnection[] connectionArray = new FloatFastConnection[connectionCount];
			int connectionIdx=0;
			for(int connectionGeneIdx=0; connectionGeneIdx<connectionCount; connectionGeneIdx++)
			{
				ConnectionGene connectionGene = g.ConnectionGeneList[connectionIdx];
				connectionArray[connectionIdx].sourceNeuronIdx = (int)neuronIndexTable[connectionGene.SourceNeuronId];
				connectionArray[connectionIdx].targetNeuronIdx = (int)neuronIndexTable[connectionGene.TargetNeuronId];
				connectionArray[connectionIdx].weight = (float)connectionGene.Weight;
				connectionIdx++;
			}

			// Now sort the connection array on sourceNeuronIdx, secondary sort on targetNeuronIdx.
			// TODO: custom sort routine to prevent boxing/unboxing required by Array.Sort(ValueType[])
			//Array.Sort(connectionArray, fastConnectionComparer);
			QuickSortFastConnections(0, fastConnectionArray.Length-1);

			return new FastConcurrentMultiplicativeNetwork(
				biasNodeCount, inputNeuronCount, 
				outputNeuronCount, neuronGeneCount,
				connectionArray, activationFn);
		}

		#endregion

        #region Decode To ModularNetwork

        static public ModularNetwork DecodeToModularNetwork(NeatGenome.NeatGenome g)
        {
            int inputCount = g.InputNeuronCount;
            int outputCount = g.OutputNeuronCount;
            int neuronCount = g.NeuronGeneList.Count;

            IActivationFunction[] activationFunctions = new IActivationFunction[neuronCount];
            float[] biasList = new float[neuronCount];

            Dictionary<uint, int> neuronLookup = new Dictionary<uint, int>(neuronCount);

            // Create an array of the activation functions for each non-module node node in the genome.
            // Start with a bias node if there is one in the genome.
            // The genome's neuron list is assumed to be ordered by type, with the bias node appearing first.
            int neuronGeneIndex = 0;
            for (; neuronGeneIndex < neuronCount; neuronGeneIndex++) {
                if (g.NeuronGeneList[neuronGeneIndex].NeuronType != NeuronType.Bias)
                    break;
                activationFunctions[neuronGeneIndex] = g.NeuronGeneList[neuronGeneIndex].ActivationFunction;
                neuronLookup.Add(g.NeuronGeneList[neuronGeneIndex].InnovationId, neuronGeneIndex);
            }
            int biasCount = neuronGeneIndex;
            for (; neuronGeneIndex < neuronCount; neuronGeneIndex++) {
                activationFunctions[neuronGeneIndex] = g.NeuronGeneList[neuronGeneIndex].ActivationFunction;
                //TODO:DAVID, I don't know why it's getting the wrong geneindex here
                if (neuronLookup.ContainsKey(g.NeuronGeneList[neuronGeneIndex].InnovationId))
                    g.NeuronGeneList.RemoveAt(neuronGeneIndex);
                else
                {
                    neuronLookup.Add(g.NeuronGeneList[neuronGeneIndex].InnovationId, neuronGeneIndex);
                    biasList[neuronGeneIndex] = g.NeuronGeneList[neuronGeneIndex].Bias;
                }
            }

            // Create an array of the activation functions, inputs, and outputs for each module in the genome.
            ModulePacket[] modules = new ModulePacket[g.ModuleGeneList.Count];
            for (int i = g.ModuleGeneList.Count - 1; i >= 0; i--) {
                modules[i].function = g.ModuleGeneList[i].Function;
                // Must translate input and output IDs to array locations.
                modules[i].inputLocations = new int[g.ModuleGeneList[i].InputIds.Count];
                for (int j = g.ModuleGeneList[i].InputIds.Count - 1; j >= 0; j--) {
                    modules[i].inputLocations[j] = neuronLookup[g.ModuleGeneList[i].InputIds[j]];
                }
                modules[i].outputLocations = new int[g.ModuleGeneList[i].OutputIds.Count];
                for (int j = g.ModuleGeneList[i].OutputIds.Count - 1; j >= 0; j--) {
                    modules[i].outputLocations[j] = neuronLookup[g.ModuleGeneList[i].OutputIds[j]];
                }
            }

            // ConnectionGenes point to a neuron's innovation ID. Translate this ID to the neuron's index in the neuron array. 
            FloatFastConnection[] connections = new FloatFastConnection[g.ConnectionGeneList.Count];
            for (int connectionGeneIndex = g.ConnectionGeneList.Count - 1; connectionGeneIndex >= 0; connectionGeneIndex--) {
                ConnectionGene connectionGene = g.ConnectionGeneList[connectionGeneIndex];
                connections[connectionGeneIndex].sourceNeuronIdx = neuronLookup[connectionGene.SourceNeuronId];
                connections[connectionGeneIndex].targetNeuronIdx = neuronLookup[connectionGene.TargetNeuronId];
                connections[connectionGeneIndex].weight = (float)connectionGene.Weight;

                connections[connectionGeneIndex].learningRate = connectionGene.learningRate;
                connections[connectionGeneIndex].A = connectionGene.A;
                connections[connectionGeneIndex].B = connectionGene.B;
                connections[connectionGeneIndex].C = connectionGene.C;
                connections[connectionGeneIndex].D = connectionGene.D;
                connections[connectionGeneIndex].modConnection = connectionGene.modConnection;

                connections[connectionGeneIndex].hive = connectionGene.hive;

            }

            ModularNetwork mn = new ModularNetwork(biasCount, inputCount, outputCount, neuronCount, connections, biasList, activationFunctions, modules);
            if (g.networkAdaptable) mn.adaptable = true;
            if (g.networkModulatory) mn.modulatory = true;
            
            mn.genome = g;
            return mn;
        }

        #endregion

        #region Decode to CTRNN

        static public ModularNetwork DecodeToCTRNN(NeatGenome.NeatGenome g)
        {
            int inputCount = g.InputNeuronCount;
            int outputCount = g.OutputNeuronCount;
            int neuronCount = g.NeuronGeneList.Count;

            IActivationFunction[] activationFunctions = new IActivationFunction[neuronCount];
            float[] biasList = new float[neuronCount];
            float[] timeConst = new float[neuronCount];

            Dictionary<uint, int> neuronLookup = new Dictionary<uint, int>(neuronCount);

            // Create an array of the activation functions for each non-module node node in the genome.
            // Start with a bias node if there is one in the genome.
            // The genome's neuron list is assumed to be ordered by type, with the bias node appearing first.
            int neuronGeneIndex = 0;
            for (; neuronGeneIndex < neuronCount; neuronGeneIndex++)
            {
                if (g.NeuronGeneList[neuronGeneIndex].NeuronType != NeuronType.Bias)
                    break;
                activationFunctions[neuronGeneIndex] = g.NeuronGeneList[neuronGeneIndex].ActivationFunction;
                neuronLookup.Add(g.NeuronGeneList[neuronGeneIndex].InnovationId, neuronGeneIndex);
            }
            int biasCount = neuronGeneIndex;
            for (; neuronGeneIndex < neuronCount; neuronGeneIndex++)
            {
                activationFunctions[neuronGeneIndex] = g.NeuronGeneList[neuronGeneIndex].ActivationFunction;
                neuronLookup.Add(g.NeuronGeneList[neuronGeneIndex].InnovationId, neuronGeneIndex);
                biasList[neuronGeneIndex] = g.NeuronGeneList[neuronGeneIndex].Bias;
                timeConst[neuronGeneIndex] = g.NeuronGeneList[neuronGeneIndex].TimeConstant;
            }

            // Create an array of the activation functions, inputs, and outputs for each module in the genome.
            ModulePacket[] modules = new ModulePacket[g.ModuleGeneList.Count];
            for (int i = g.ModuleGeneList.Count - 1; i >= 0; i--)
            {
                modules[i].function = g.ModuleGeneList[i].Function;
                // Must translate input and output IDs to array locations.
                modules[i].inputLocations = new int[g.ModuleGeneList[i].InputIds.Count];
                for (int j = g.ModuleGeneList[i].InputIds.Count - 1; j >= 0; j--)
                {
                    modules[i].inputLocations[j] = neuronLookup[g.ModuleGeneList[i].InputIds[j]];
                }
                modules[i].outputLocations = new int[g.ModuleGeneList[i].OutputIds.Count];
                for (int j = g.ModuleGeneList[i].OutputIds.Count - 1; j >= 0; j--)
                {
                    modules[i].outputLocations[j] = neuronLookup[g.ModuleGeneList[i].OutputIds[j]];
                }
            }

            // ConnectionGenes point to a neuron's innovation ID. Translate this ID to the neuron's index in the neuron array. 
            FloatFastConnection[] connections = new FloatFastConnection[g.ConnectionGeneList.Count];
            for (int connectionGeneIndex = g.ConnectionGeneList.Count - 1; connectionGeneIndex >= 0; connectionGeneIndex--)
            {
                ConnectionGene connectionGene = g.ConnectionGeneList[connectionGeneIndex];
                connections[connectionGeneIndex].sourceNeuronIdx = neuronLookup[connectionGene.SourceNeuronId];
                connections[connectionGeneIndex].targetNeuronIdx = neuronLookup[connectionGene.TargetNeuronId];
                connections[connectionGeneIndex].weight = (float)connectionGene.Weight;

                connections[connectionGeneIndex].learningRate = connectionGene.learningRate;
                connections[connectionGeneIndex].A = connectionGene.A;
                connections[connectionGeneIndex].B = connectionGene.B;
                connections[connectionGeneIndex].C = connectionGene.C;
                connections[connectionGeneIndex].D = connectionGene.D;
                connections[connectionGeneIndex].modConnection = connectionGene.modConnection;

                connections[connectionGeneIndex].hive = connectionGene.hive;

            }

            CTRNN mn = new CTRNN(biasCount, inputCount, outputCount, neuronCount, connections, biasList, activationFunctions, modules,timeConst);
            if (g.networkAdaptable) mn.adaptable = true;
            if (g.networkModulatory) mn.modulatory = true;

            mn.genome = g;
            return mn;
        }

        #endregion

        #region Decode To IntegerFastConcurrentNetwork

        /// <summary>
		/// Create a single comparer to limit the need to reconstruct for each network. 
		/// Not multithread safe!
		/// </summary>
		//static FastConnectionComparer fastConnectionComparer = new FastConnectionComparer();		
		static IntegerFastConnection[] intFastConnectionArray;

		static public IntegerFastConcurrentNetwork DecodeToIntegerFastConcurrentNetwork(NeatGenome.NeatGenome g)
		{			
			int outputNeuronCount = g.OutputNeuronCount;
			int neuronGeneCount = g.NeuronGeneList.Count;

			// Slightly inefficient - determine the number of bias nodes. Fortunately there is not actually
			// any reason to ever have more than one bias node - although there may be 0.
			int neuronGeneIdx=0;
			for(; neuronGeneIdx<neuronGeneCount; neuronGeneIdx++)
			{
				if(g.NeuronGeneList[neuronGeneIdx].NeuronType != NeuronType.Bias)
					break;
			}
			int biasNodeCount = neuronGeneIdx;
			int inputNeuronCount = g.InputNeuronCount;
			
			// ConnectionGenes point to a neuron ID. We need to map this ID to a 0 based index for
			// efficiency. 
			
			// Use a quick heuristic to determine which will be the fastest technique for mapping the connection end points
			// to neuron indexes. This is heuristic is not 100% perfect but has been found to be very good in in real word 
			// tests. Feel free to perform your own calculation and create a more intelligent heuristic!
			int connectionCount=g.ConnectionGeneList.Count;
			if(neuronGeneCount * connectionCount < 45000)
			{	
				intFastConnectionArray = new IntegerFastConnection[connectionCount];
				int connectionIdx=0;
				for(int connectionGeneIdx=0; connectionGeneIdx<connectionCount; connectionGeneIdx++)
				{
					//Note. Binary search algorithm assume that neurons are ordered by their innovation Id.
					ConnectionGene connectionGene = g.ConnectionGeneList[connectionIdx];
					intFastConnectionArray[connectionIdx].sourceNeuronIdx = (int)g.NeuronGeneList.BinarySearch(connectionGene.SourceNeuronId);
					intFastConnectionArray[connectionIdx].targetNeuronIdx = (int)g.NeuronGeneList.BinarySearch(connectionGene.TargetNeuronId);

					System.Diagnostics.Debug.Assert(intFastConnectionArray[connectionIdx].sourceNeuronIdx>=0 && intFastConnectionArray[connectionIdx].targetNeuronIdx>=0, "invalid idx");

					// Scale weight to range expected by the integer network class.
					// +-5 -> +-0x1000
					intFastConnectionArray[connectionIdx].weight = (int)(connectionGene.Weight * 0x333D);
					connectionIdx++;
				}
			}
			else
			{
				// Build a table of indexes (ints) keyed on neuron ID. This approach is faster when dealing with large numbers 
				// of lookups.
				Hashtable neuronIndexTable = new Hashtable(neuronGeneCount);
				for(int i=0; i<neuronGeneCount; i++)
					neuronIndexTable.Add(g.NeuronGeneList[i].InnovationId, i);

				// Now we can build the connection array(s).
				//int connectionCount=g.ConnectionGeneList.Count;
				//FastConnection[] connectionArray = new FastConnection[connectionCount];
				intFastConnectionArray = new IntegerFastConnection[connectionCount];
				int connectionIdx=0;
				for(int connectionGeneIdx=0; connectionGeneIdx<connectionCount; connectionGeneIdx++)
				{
					ConnectionGene connectionGene = g.ConnectionGeneList[connectionIdx];
					intFastConnectionArray[connectionIdx].sourceNeuronIdx = (int)neuronIndexTable[connectionGene.SourceNeuronId];
					intFastConnectionArray[connectionIdx].targetNeuronIdx = (int)neuronIndexTable[connectionGene.TargetNeuronId];

					// Scale weight to range expected by the integer network class.
					// +-5 -> +-0x1000
					intFastConnectionArray[connectionIdx].weight = (int)(connectionGene.Weight * 0x333D);
					connectionIdx++;
				}
			}

			// Now sort the connection array on sourceNeuronIdx, secondary sort on targetNeuronIdx.
			// Using Array.Sort is 10 times slower than the hand-coded sorting routine. See notes on that routine for more 
			// information. Also note that in tests that this sorting did no t actually improve the speed of the network!
			// However, it may have a benefit for CPUs with small caches or when networks are very large, and since the new
			// sort takes up hardly any time for even large networks, it seems reasonable to leave in the sort.
			//Array.Sort(fastConnectionArray, fastConnectionComparer);
			if(intFastConnectionArray.Length>1)
				QuickSortIntFastConnections(0, intFastConnectionArray.Length-1);
			
			return new IntegerFastConcurrentNetwork(biasNodeCount, inputNeuronCount, 
													outputNeuronCount, neuronGeneCount,
													intFastConnectionArray);
		}
		
		#endregion
		
		#region Decode To NetworkModel

		static public NetworkModel DecodeToNetworkModel(ConcurrentNetwork network)
		{
			ModelNeuronList masterNeuronList = new ModelNeuronList();
			
			// loop all neurons and build a table keyed on id.
			Hashtable neuronTable = new Hashtable(network.MasterNeuronList.Count);
			foreach(Neuron neuron in network.MasterNeuronList)
			{	
				ModelNeuron modelNeuron = new ModelNeuron(neuron.NeuronType, neuron.Id,ActivationFunctionFactory.GetActivationFunction("NullFn"));
				neuronTable.Add(modelNeuron.Id, modelNeuron);
				masterNeuronList.Add(modelNeuron);
			}

			// Loop through all of the connections (within the neurons)
			// Now we have a neuron table keyed on id we can attach the connections
			// to their source and target neurons.
			foreach(Neuron neuron in network.MasterNeuronList)
			{
				foreach(Connection connection in neuron.ConnectionList)
				{
					ModelConnection modelConnection = new ModelConnection();
					modelConnection.Weight = connection.Weight;
					modelConnection.SourceNeuron = (ModelNeuron)neuronTable[connection.SourceNeuronId];
					modelConnection.TargetNeuron = (ModelNeuron)neuronTable[connection.TargetNeuronId];

					modelConnection.SourceNeuron.OutConnectionList.Add(modelConnection);
					modelConnection.TargetNeuron.InConnectionList.Add(modelConnection);
				}
			}

			return new NetworkModel(masterNeuronList);
		}

		static public NetworkModel DecodeToNetworkModel(NeatGenome.NeatGenome g)
		{
			ModelNeuronList masterNeuronList = new ModelNeuronList();

			// loop all neurons and build a table keyed on id.
			HybridDictionary neuronTable = new HybridDictionary(g.NeuronGeneList.Count);
			foreach(NeuronGene neuronGene in g.NeuronGeneList)
			{
				ModelNeuron modelNeuron = new ModelNeuron(neuronGene.NeuronType, neuronGene.InnovationId,neuronGene.ActivationFunction);
				neuronTable.Add(modelNeuron.Id, modelNeuron);
				masterNeuronList.Add(modelNeuron);
			}

			// Loop through all of the connections.
			// Now we have a neuron table keyed on id we can attach the connections
			// to their source and target neurons.
			foreach(ConnectionGene connectionGene in g.ConnectionGeneList)
			{
				ModelConnection modelConnection = new ModelConnection();
				modelConnection.Weight = connectionGene.Weight;
				modelConnection.SourceNeuron = (ModelNeuron)neuronTable[connectionGene.SourceNeuronId];
				modelConnection.TargetNeuron = (ModelNeuron)neuronTable[connectionGene.TargetNeuronId];

				modelConnection.SourceNeuron.OutConnectionList.Add(modelConnection);
				modelConnection.TargetNeuron.InConnectionList.Add(modelConnection);
			}

            //Sebastian. Build Model connections
            foreach (ModuleGene mg in g.ModuleGeneList)
            {
                foreach (uint sourceID in mg.InputIds)
                {
                    foreach (uint targetID in mg.OutputIds)
                    {
                        ModelConnection modelConnection = new ModelConnection();
                        modelConnection.Weight = 1.0; //TODO  connectionGene.Weight;
                        modelConnection.SourceNeuron = (ModelNeuron)neuronTable[sourceID];
                        modelConnection.TargetNeuron = (ModelNeuron)neuronTable[targetID];

                        modelConnection.SourceNeuron.OutConnectionList.Add(modelConnection);
                        modelConnection.TargetNeuron.InConnectionList.Add(modelConnection);
                    }
                }
            }

			return new NetworkModel(masterNeuronList);
		}

		#endregion

		#region Built-In FastConnection Sorting

		// This is a quick sort algorithm that manipulates FastConnection structures. Although this
		// is the same sorting technique used internally by Array.Sort this is approximately 10 times 
		// faster because it eliminates the need for boxing and unboxing of the structs.
		// So although this code could be replcaed by a single Array.Sort statement, the pay off
		// was though to be worth it.

		private static int CompareKeys(ref FloatFastConnection a, ref FloatFastConnection b)
		{
			int diff = a.sourceNeuronIdx - b.sourceNeuronIdx;
			if(diff==0)
			{
				// Secondary sort on targetNeuronIdx.
				return a.targetNeuronIdx - b.targetNeuronIdx;
			}
			else
			{
				return diff;
			}
		}

		/// <summary>
		/// Standard qquicksort algorithm.
		/// </summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		private static void QuickSortFastConnections(int left, int right) 
		{
			do 
			{
				int i = left;
				int j = right;
				FloatFastConnection x = fastConnectionArray[(i + j) >> 1];
				do 
				{
					while (CompareKeys(ref fastConnectionArray[i], ref x) < 0) i++;
					while (CompareKeys(ref x, ref fastConnectionArray[j]) < 0) j--;

					System.Diagnostics.Debug.Assert(i>=left && j<=right, "(i>=left && j<=right)  Sort failed - Is your IComparer bogus?");
					if (i > j) break;
					if (i < j) 
					{
						FloatFastConnection key = fastConnectionArray[i];
						fastConnectionArray[i] = fastConnectionArray[j];
						fastConnectionArray[j] = key;
					}
					i++;
					j--;
				} while (i <= j);

				if (j-left <= right-i) 
				{
					if (left < j) QuickSortFastConnections(left, j);
					left = i;
				}
				else 
				{
					if (i < right) QuickSortFastConnections(i, right);
					right = j;
				}
			} while (left < right);
		}
	
		#endregion

		#region Built-In IntegerFastConnection Sorting

		// This is a quick sort algorithm that manipulates FastConnection structures. Although this
		// is the same sorting technique used internally by Array.Sort this is approximately 10 times 
		// faster because it eliminates the need for boxing and unboxing of the structs.
		// So although this code could be replcaed by a single Array.Sort statement, the pay off
		// was though to be worth it.

		private static int CompareKeys(ref IntegerFastConnection a, ref IntegerFastConnection b)
		{
			int diff = a.sourceNeuronIdx - b.sourceNeuronIdx;
			if(diff==0)
			{
				// Secondary sort on targetNeuronIdx.
				return a.targetNeuronIdx - b.targetNeuronIdx;
			}
			else
			{
				return diff;
			}
		}

		/// <summary>
		/// Standard qquicksort algorithm.
		/// </summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		private static void QuickSortIntFastConnections(int left, int right) 
		{
			do 
			{
				int i = left;
				int j = right;
				IntegerFastConnection x = intFastConnectionArray[(i + j) >> 1];
				do 
				{
				while (CompareKeys(ref intFastConnectionArray[i], ref x) < 0) i++;
				while (CompareKeys(ref x, ref intFastConnectionArray[j]) < 0) j--;

					System.Diagnostics.Debug.Assert(i>=left && j<=right, "(i>=left && j<=right)  Sort failed - Is your IComparer bogus?");
					if (i > j) break;
					if (i < j) 
					{
						IntegerFastConnection key = intFastConnectionArray[i];
						intFastConnectionArray[i] = intFastConnectionArray[j];
						intFastConnectionArray[j] = key;
					}
					i++;
					j--;
				} while (i <= j);

				if (j-left <= right-i) 
				{
					if (left < j) QuickSortIntFastConnections(left, j);
					left = i;
				}
				else 
				{
					if (i < right) QuickSortIntFastConnections(i, right);
					right = j;
				}
			} while (left < right);
		}
	
		#endregion
	}
}
