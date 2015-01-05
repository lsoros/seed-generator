using System;
using SharpNEATLib.NeuralNetwork;

namespace SharpNEATLib.NetworkVisualization
{
	public class ModelConnection
	{
		ModelNeuron sourceNeuron;
		ModelNeuron targetNeuron;
		double weight;
		bool omitFromBitmap;

		#region Properties

		public ModelNeuron SourceNeuron
		{
			get
			{
				return sourceNeuron;
			}
			set
			{
				sourceNeuron = value;
			}
		}

		public ModelNeuron TargetNeuron
		{
			get
			{
				return targetNeuron;
			}
			set
			{
				targetNeuron = value;
			}
		}

		public double Weight
		{
			get
			{
				return weight;
			}
			set
			{
				weight=value;
			}
		}

		public bool OmitFromBitmap
		{
			get
			{
				return omitFromBitmap;
			}
			set
			{
				omitFromBitmap = value;
			}
		}

		#endregion
	}
}
