﻿namespace SharpNEATLib.NeuralNetwork
{

    public struct ModulePacket
    {
        public IModule function;
        public int[] inputLocations;
        public int[] outputLocations;
    }

}
