using System;

namespace SharpNEATLib.Evolution
{
	public enum ConnectionPerturbationType
	{
		/// <summary>
		/// Reset weights.
		/// </summary>
		Reset,

		/// <summary>
		/// Jiggle - even distribution
		/// </summary>
		JiggleEven,

		/// <summary>
		/// Jiggle - normal distribution
		/// </summary>
		JiggleND
	}
}
