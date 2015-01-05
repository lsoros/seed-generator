using System;

namespace SharpNEATLib.NeuralNetwork
{
	public struct IntegerFastConnection
	{
		public int sourceNeuronIdx;
		public int targetNeuronIdx;
		public int weight;
		public int signal;
	}
}
