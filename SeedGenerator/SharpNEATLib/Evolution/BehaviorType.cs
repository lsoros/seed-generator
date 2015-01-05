// BehaviorType.cs created with MonoDevelop
// User: joel at 1:44 PM 8/5/2009
//
// To change standard headers go to Edit->Preferences->Coding->Standard Headers
//

using System;
using System.Collections.Generic;
namespace SharpNEATLib
{
    public class BehaviorType
    {        
        public List<double> behaviorList;
		public double[] objectives;
        public BehaviorType()
        {
         
        }
        public BehaviorType(BehaviorType copyFrom)
        {
            if(copyFrom.behaviorList!=null)
            behaviorList = new List<double>(copyFrom.behaviorList);
        }
    }
    
    public static class BehaviorDistance
    {
        public static double Distance(BehaviorType x, BehaviorType y)
        {
           double behavioralDistance = 0.0;

           if (x != null && y != null)
           {
               // Loop through each triple in the behavior vector
               for (int k = 0; k < x.behaviorList.Count; k+=3)
               {
                   // Position component
                   //behavioralDistance += Globals.positionWeight * Scale(EuclideanDistance(x.behaviorList[k], y.behaviorList[k], x.behaviorList[k + 1], y.behaviorList[k + 1]), 0.0, 1389.25, 0.0, 1.0);
                   
                   // Planting component
                   //behavioralDistance += Globals.plantingWeight * (Math.Abs(x.behaviorList[k+2] - y.behaviorList[k+2]));
               }
           }
           return behavioralDistance;
        }

        public static double EuclideanDistance(double x1, double y1, double x2, double y2)
        {
            return Math.Sqrt(Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2));
        }

        /// <summary>
        /// Scales a value which is expected to be in some range to some other range.
        /// </summary>
        /// <param name="value">Value to be scaled</param>
        /// <param name="min">Minimum of original range</param>
        /// <param name="max">Maximum of original range</param>
        /// <param name="scaledMin">Minimum of desired range</param>
        /// <param name="scaledMax">Maximum of desired range</param>
        /// <returns>The original value scaled to be in the desired range</returns>
        private static double Scale(double value, double min, double max, double scaledMin, double scaledMax)
        {
            return (((scaledMax - scaledMin) * (value - min)) / (max - min)) + scaledMin;
        }
    }
}
