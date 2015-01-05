using System;
using System.Xml;

using SharpNEATLib.Evolution;
using SharpNEATLib.Evolution.Xml;

namespace SharpNEATLib.NeatGenome.Xml
{
	public class XmlNeatGenomeReader : IGenomeReader
	{
		public IGenome Read(XmlElement xmlGenome)
		{
			return XmlNeatGenomeReaderStatic.Read(xmlGenome);
		}
	}
}
