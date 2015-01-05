using System;

namespace SharpNEATLib.Maths
{
	public class MathsException : System.Exception
	{
		public MathsException()
		{
		}

		public MathsException(string message) : base(message)
		{	
		}

		public MathsException(string message, System.Exception innerException) : base(message, innerException)
		{	
		}
	}
}
