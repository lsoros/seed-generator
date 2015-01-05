using System;
using System.Drawing;

namespace SharpNEATLib.NetworkVisualization
{
	public interface ILayoutManager
	{
		void Layout(NetworkModel nm, Size areaSize);
	}
}
