using System;
using System.Collections.Generic;
using System.Text;

namespace SharpNEATLib.NeuralNetwork
{
    class Gaussian : IActivationFunction
    {
        #region IActivationFunction Members

        public double Calculate(double inputSignal)
        {
            return 2 * Math.Exp(-Math.Pow(inputSignal * 2.5, 2)) - 1;
        }

        public float Calculate(float inputSignal)
        {
            return (float)(2 * Math.Exp(-Math.Pow(inputSignal * 2.5, 2)) - 1);
        }

        public string FunctionId
        {
            get { return this.GetType().Name; }
        }

        public string FunctionString
        {
            get { return "2*e^(-(input*2.5)^2) - 1"; }
        }

        public string FunctionDescription
        {
            get { return "bimodal gaussian"; }
        }

        #endregion
    }
}
