using System;
using System.Xml;
using SharpNEATLib.Evolution;

namespace SharpNEATLib.Evolution.Xml
{
	public interface IGenomeReader
	{
		IGenome Read(XmlElement xmlGenome);
	}
}
