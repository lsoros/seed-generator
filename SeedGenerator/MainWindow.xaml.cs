using SharpNEATLib;
using SharpNEATLib.CPPNs;
using SharpNEATLib.Evolution;
using SharpNEATLib.Evolution.Xml;
using SharpNEATLib.Experiments;
using SharpNEATLib.Maths;
using SharpNEATLib.NeatGenome;
using SharpNEATLib.NeatGenome.Xml;
using SharpNEATLib.NeuralNetwork;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;

namespace SeedGenerator
{
    /// <summary>
    /// This class contains most of the useful code for the project. 
    /// It links the GUI and the underlying SharpNEAT CPPN architecture. 
    /// </summary>
    public partial class MainWindow : Window
    {
        class NeuronConnectionLookup
        {
            public NeuronGene neuronGene;
            public ConnectionGeneList incomingList = new ConnectionGeneList();
            public ConnectionGeneList outgoingList = new ConnectionGeneList();
        }

        // Constant declarations
        const int numOffspring = 8;
        const int numInputs = 2;
        const int numOutputs = 4;
        const int pixelHeight = 100;

        // Declarations for NEAT components.
        List<NeatGenome> offspringGenomes;
        NeatGenome parentGenome;
        GenomeDecoder decoder;
        uint currentGeneration;
        List<Image> offspringImages;
        WriteableBitmap blankBMP;
        List<WriteableBitmap> bmpList;
        int activationFunction;

        NeatParameters neatParams;
        IdGenerator idGenerator;
        Hashtable newConnectionGeneTable;
        Hashtable newNeuronGeneStructTable;

        public MainWindow()
        {
            // Initialize class-specific objects
            neatParams = new NeatParameters();
            offspringGenomes = new List<NeatGenome>();
            bmpList = new List<WriteableBitmap>();
            idGenerator = new IdGenerator();
            newConnectionGeneTable = new Hashtable();
            newNeuronGeneStructTable = new Hashtable();

            // Initialize WPF-specific objects
            InitializeComponent();
            
            // Set internal NEAT parameters based on GUI slider values
            neatParams.pMutateAddConnection = addConnectionSlider.Value;
            neatParams.pMutateAddNode = addNeuronSlider.Value;
            neatParams.pInitialPopulationInterconnections = 1.0f;
            neatParams.pMutateDeleteConnection = deleteConnectionSlider.Value;

            

            //activationFunction = 0;

            // Create a random (simple) parent for the initial generation.
            parentGenome = (NeatGenome)GenomeFactory.CreateGenome(neatParams, idGenerator, numInputs, numOutputs, 1.0f);

            // Generate genomes for the undisplayed initial population of offspring.
            currentGeneration = 0;
            CreateOffspringGenomes();

            // Add each of the offspring images (currently blank) to the internal list.
            offspringImages = new List<Image>();
            offspringImages.Add(offspringImage1);
            offspringImages.Add(offspringImage2);
            offspringImages.Add(offspringImage3);
            offspringImages.Add(offspringImage4);
            offspringImages.Add(offspringImage9);
            offspringImages.Add(offspringImage10);
            offspringImages.Add(offspringImage11);
            offspringImages.Add(offspringImage12);

            /*
             * Here, each of the offspring images is intialized to be entirely white.
             * Note that the format here is BGRA, not RGBA. B and R are switched. This seems confusing, but it
             * means that each image uses half the amount of space as the RGBA version. 
             * Each pixel requires 4 adjacent bytes to specify, ordered Blue, Green, Red, Alpha. 
             * Note that Alpha must be set to 255 in order for the color to fully show. An Alpha of 0 will cause transparency.
            */

            blankBMP = new WriteableBitmap(pixelHeight, pixelHeight, 300, 300, PixelFormats.Bgra32, null);
            byte[] pixels = new byte[blankBMP.PixelHeight * blankBMP.PixelWidth * blankBMP.Format.BitsPerPixel / 8];
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 0xff;
                pixels[i + 1] = 0xff;
                pixels[i + 2] = 0xff;
                pixels[i + 3] = 0xff;
            }
            blankBMP.WritePixels(new Int32Rect(0, 0, blankBMP.PixelWidth, blankBMP.PixelHeight), pixels, blankBMP.PixelWidth * blankBMP.Format.BitsPerPixel / 8, 0);
            foreach (Image offspringImage in offspringImages)
            {
                offspringImage.Source = blankBMP;
                bmpList.Add(new WriteableBitmap(blankBMP));
            }

            bmpList.Add(new WriteableBitmap(blankBMP));
            DrawCreatureAtIndex(8, true);
        }

        /// <summary>
        /// Generate a new batch of offspring from the selected parent. 
        /// If no new parent is selected from the current batch of offspring, keep the current parent
        /// as the parent of the new generation.
        /// </summary>
        private void buttonEvolve_Click(object sender, RoutedEventArgs e)
        {
            currentGeneration = 0;

            // Create a random (simple) parent for the new initial generation and draw it to the Current Seed slot
            parentGenome = (NeatGenome)GenomeFactory.CreateGenome(neatParams, idGenerator, numInputs, numOutputs, 1.0f);
            DrawCreatureAtIndex(8, true);

            // Generate new offspring from the parent and then draw them.
            CreateOffspringGenomes();
            for (int i = 0; i < offspringGenomes.Count; i++)
                DrawCreatureAtIndex(i, false);
        }

        /// <summary>
        /// Asexually creates offspring by mutating the selected parent.
        /// </summary>
        private void CreateOffspringGenomes()
        {
            offspringGenomes.Clear();
            newConnectionGeneTable.Clear();
            newNeuronGeneStructTable.Clear();

            for (int i = 0; i < numOffspring; i++)
                    offspringGenomes.Add(parentGenome);

            currentGeneration++;
        }

        /// <summary>
        /// Activate the CPPN to draw the individual located in the index'th slot.
        /// If parent == true, then we are drawing a selected offspring into the parent slot.
        /// InputSignalArray[0-1] = [r,theta]
        /// OutputSignalArray[0-3] = [R,G,B,rmax]
        /// </summary>
        private void DrawCreatureAtIndex(int index, bool parent)
        {
            // Create the cppn by decoding the selected individual's genome.
            INetwork cppn;
            if (!parent)
                cppn = offspringGenomes[index].Decode(ActivationFunctionFactory.GetActivationFunction("Linear"));
            else
                cppn = parentGenome.Decode(ActivationFunctionFactory.GetActivationFunction("Linear"));

            // Set up a writeable bitmap to contain the CPPN-generated pixels
            bmpList[index] = new WriteableBitmap(pixelHeight, pixelHeight, 300, 300, PixelFormats.Bgra32, null);
            byte[] pixels = new byte[bmpList[index].PixelHeight * bmpList[index].PixelWidth * bmpList[index].Format.BitsPerPixel / 8];
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 0xff;
                pixels[i + 1] = 0xff;
                pixels[i + 2] = 0xff;
                pixels[i + 3] = 0xff;
            }

            // Temporary variable declarations
            int rmax;
            float inputSignal;
            Point xyPoint;

            // Draw the image one line segment at a time, starting at theta=0 and working around until you hit 360 degrees
            for (float theta = (float)-Math.PI; theta < Math.PI; theta += (1.0f / 360.0f))
            {
                // Activate the CPPN with r=0 to find rmax for this value of theta.
                cppn.SetInputSignal(0, 0);
                inputSignal = Scale(theta, -Math.PI, Math.PI, -1.0, 1.0);
                cppn.SetInputSignal(1, inputSignal);
                cppn.SingleStep();

                // Draw the outermost point at (rmax, theta) here, getting rmax from cppn.OutputSignalArray[3]
                rmax = (int)Math.Floor(Scale(cppn.GetOutputSignal(3), -1.0, 1.0, 0.0, 1.0) * 49.0);
                xyPoint = ConvertToCartesian(rmax, theta + (float)(Math.PI / 2));
                //pixels[(int)xyPoint.X + ((int)xyPoint.Y * pixelHeight)] = Color.TransparentBlack;
                pixels[((int)Math.Floor(xyPoint.X) * pixelHeight + (int)Math.Floor(xyPoint.Y)) * 4] = 0x00;
                pixels[((int)Math.Floor(xyPoint.X) * pixelHeight + (int)Math.Floor(xyPoint.Y)) * 4 + 1] = 0x00;
                pixels[((int)Math.Floor(xyPoint.X) * pixelHeight + (int)Math.Floor(xyPoint.Y)) * 4 + 2] = 0x00;
                pixels[((int)Math.Floor(xyPoint.X) * pixelHeight + (int)Math.Floor(xyPoint.Y)) * 4 + 3] = 0xff;

                // Color one line segment extending from the origin to rmax
                for (int r = 0; r < rmax; r++)
                {
                    cppn.SetInputSignal(0, Scale((float)r, 0.0, 49.0, -1.0, 1.0));
                    cppn.SingleStep();

                    xyPoint = ConvertToCartesian(r, theta + (float)(Math.PI / 2));

                    // Convert the CPPN output signals to byte format.
                    // Will want to experiment with simple squashing instead of using the absolute value here.
                    byte blueComponent = (byte)(Scale(cppn.GetOutputSignal(0), -1.0, 1.0, 0.0, 1.0) * 255.0);
                    byte greenComponent = (byte)(Scale(cppn.GetOutputSignal(1), -1.0, 1.0, 0.0, 1.0) * 255.0);
                    byte redComponent = (byte)(Scale(cppn.GetOutputSignal(2), -1.0, 1.0, 0.0, 1.0) * 255.0);

                    // Write the bytes into the pixel's chunk of the byte array (4 bytes per pixel)
                    // The fourth byte is for transparency.
                    pixels[((int)Math.Floor(xyPoint.X) * pixelHeight + (int)Math.Floor(xyPoint.Y)) * 4] = blueComponent;
                    pixels[((int)Math.Floor(xyPoint.X) * pixelHeight + (int)Math.Floor(xyPoint.Y)) * 4 + 1] = greenComponent;
                    pixels[((int)Math.Floor(xyPoint.X) * pixelHeight + (int)Math.Floor(xyPoint.Y)) * 4 + 2] = redComponent;
                    pixels[((int)Math.Floor(xyPoint.X) * pixelHeight + (int)Math.Floor(xyPoint.Y)) * 4 + 3] = 0xff;
                }   
            }

            // Draw the pixels to the screen all at once.
            bmpList[index].WritePixels(new Int32Rect(0, 0, blankBMP.PixelWidth, blankBMP.PixelHeight), pixels, blankBMP.PixelWidth * blankBMP.Format.BitsPerPixel / 8, 0);
            if (!parent)
                offspringImages[index].Source = bmpList[index];
            else
                currentParentImage.Source = bmpList[index];
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
        private float Scale(double value, double min, double max, double scaledMin, double scaledMax)
        {
            return (float)((((scaledMax - scaledMin) * (value - min)) / (max - min)) + scaledMin);
        }

        /// <summary>
        /// Converts polar coordinates (r,theta) to (x,y). 
        /// R comes in as degrees, which must be converted to radians before processing.
        /// </summary>
        private Point ConvertToCartesian(int r, int theta)
        {
            double radians = theta * (Math.PI / 180.0);
            Point XY = new Point();
            XY.X = (float)Math.Cos(radians) * r + (pixelHeight / 2);
            XY.Y = (float)Math.Sin(radians) * r + (pixelHeight / 2);
            return XY;
        }

        /// <summary>
        /// Converts polar coordinates (r,theta) to (x,y). 
        /// R comes in as radians.
        /// </summary>
        public static Point ConvertToCartesian(int r, float radians)
        {
            Point XY = new Point();
            XY.X = Convert.ToInt32((float)Math.Cos(radians) * r + (pixelHeight / 2));
            XY.Y = Convert.ToInt32((float)Math.Sin(radians) * r + (pixelHeight / 2));
            return XY;
        }

        /// <summary>
        /// Erases all current offspring and parents and starts the experiment over from scratch.
        /// </summary>
        private void buttonRestart_Click(object sender, RoutedEventArgs e)
        {
            currentGeneration = 0;
            neatParams = new NeatParameters();
            neatParams.pMutateAddConnection = addConnectionSlider.Value;
            neatParams.pMutateAddNode = addNeuronSlider.Value;
            neatParams.pInitialPopulationInterconnections = 1.0f;
            neatParams.pMutateDeleteConnection = deleteConnectionSlider.Value;
            idGenerator = new IdGenerator();
            newConnectionGeneTable = new Hashtable();
            newNeuronGeneStructTable = new Hashtable();

            // Create a random (simple) parent for the initial generation and draw it to the Current Seed slot.
            parentGenome = (NeatGenome)GenomeFactory.CreateGenome(neatParams, idGenerator, numInputs, numOutputs, 1.0f);
            DrawCreatureAtIndex(8, true);

            // Create new initial offspring
            CreateOffspringGenomes();
            for (int i = 0; i < offspringGenomes.Count; i++)
                DrawCreatureAtIndex(i, false);
        }

        /// <summary>
        /// Saves the currently selected individual to the row at the top of the GUI. 
        /// This function is currently not implemented.
        /// </summary>
        private void buttonSave_Click(object sender, RoutedEventArgs e)
        {
            // Write the new morphology genome to an internal XML document
            XmlDocument morphologyGenomeXML = new XmlDocument();
            XmlDeclaration declaration = morphologyGenomeXML.CreateXmlDeclaration("1.0", null, null);
            morphologyGenomeXML.AppendChild(declaration);
            XmlGenomeWriterStatic.Write(morphologyGenomeXML, parentGenome);

            // Set up the Save File As... dialog box
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = "Seed"; 
            dlg.DefaultExt = ".xml"; 
            dlg.Filter = "XML documents (.xml)|*.xml";        

            // Process save file dialog box results
            Nullable<bool> result = dlg.ShowDialog();
            if (result == true)
            {
                // Write the internal XML document to an actual file
                string filename = dlg.FileName;
                morphologyGenomeXML.Save(filename);
            }
        }

        // The rest of the functions store the relevant genome when a new parent is selected. 
        private void offspringButton1_Click(object sender, RoutedEventArgs e)
        {
            parentGenome = offspringGenomes[0];
        }

        private void offspringButton2_Click(object sender, RoutedEventArgs e)
        {
            parentGenome = offspringGenomes[1];
        }

        private void offspringButton3_Click(object sender, RoutedEventArgs e)
        {
            parentGenome = offspringGenomes[2];
        }

        private void offspringButton4_Click(object sender, RoutedEventArgs e)
        {
            parentGenome = offspringGenomes[3];
        }

        private void offspringButton9_Click(object sender, RoutedEventArgs e)
        {
            parentGenome = offspringGenomes[4];
        }

        private void offspringButton10_Click(object sender, RoutedEventArgs e)
        {
            parentGenome = offspringGenomes[5];
        }

        private void offspringButton11_Click(object sender, RoutedEventArgs e)
        {
            parentGenome = offspringGenomes[6];
        }

        private void offspringButton12_Click(object sender, RoutedEventArgs e)
        {
            parentGenome = offspringGenomes[7];
        }

        private void slider4_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            neatParams.pMutateAddNode = addNeuronSlider.Value;
        }

        private void addConnectionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            neatParams.pMutateAddConnection = addConnectionSlider.Value;
        }

        private void deleteConnectionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            neatParams.pMutateDeleteConnection = deleteConnectionSlider.Value;
        }

        private void weightRangeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            neatParams.connectionWeightRange = weightRangeSlider.Value;
        }

        private void weightMutationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            neatParams.pMutateConnectionWeights = weightMutationSlider.Value;
        }

        public NeatGenome mutate(NeatGenome parentGenome)
        {
            // Determine the type of mutation to perform.
            double[] probabilities = new double[] 
			{
				neatParams.pMutateAddNode,
				neatParams.pMutateAddModule,
				neatParams.pMutateAddConnection,
				neatParams.pMutateDeleteConnection,
				neatParams.pMutateDeleteSimpleNeuron,
				neatParams.pMutateConnectionWeights
			};

            int outcome = RouletteWheel.SingleThrow(probabilities);

            switch (outcome)
            {
                case 0:
                    return Mutate_AddNode(parentGenome);
                case 1:
                    return Mutate_AddModule(parentGenome);
                case 2:
                    return Mutate_AddConnection(parentGenome);
                case 3:
                    return Mutate_DeleteConnection(parentGenome);
                case 4:
                    return Mutate_DeleteSimpleNeuronStructure(parentGenome);
                case 5:
                    return Mutate_ConnectionWeights(parentGenome);
            }
            return parentGenome;
        }

        /// <summary>
        /// Add a new node to the Genome. We do this by removing a connection at random and inserting 
        /// a new node and two new connections that make the same circuit as the original connection.
        /// This way the new node is properly integrated into the network from the outset.
        /// </summary>
        /// <param name="ea"></param>
        private NeatGenome Mutate_AddNode(NeatGenome parentGenome)
        {
            NeatGenome offspring = new NeatGenome(parentGenome, idGenerator.NextGenomeId);
            if (parentGenome.ConnectionGeneList.Count == 0)
                return offspring;

            for (int attempts = 0; attempts < 5; attempts++)
            {
                // Select a connection at random.
                int connectionToReplaceIdx = (int)Math.Floor(Utilities.NextDouble() * parentGenome.ConnectionGeneList.Count);
                ConnectionGene connectionToReplace = offspring.ConnectionGeneList[connectionToReplaceIdx];

                // Delete the existing connection. JOEL: Why delete old connection?
                offspring.ConnectionGeneList.RemoveAt(connectionToReplaceIdx);

                // Check if this connection has already been split on another genome. If so then we should re-use the
                // neuron ID and two connection ID's so that matching structures within the population maintain the same ID.
                object existingNeuronGeneStruct = newNeuronGeneStructTable[connectionToReplace.InnovationId];

                NeuronGene newNeuronGene;
                ConnectionGene newConnectionGene1;
                ConnectionGene newConnectionGene2;
                IActivationFunction actFunct;

                if (existingNeuronGeneStruct == null)
                {	// No existing matching structure, so generate some new ID's.

                    // Replace connectionToReplace with two new connections and a neuron.
                    actFunct = ActivationFunctionFactory.GetRandomActivationFunction(neatParams);
                    newNeuronGene = new NeuronGene(idGenerator.NextInnovationId, NeuronType.Hidden, actFunct);
                    newConnectionGene1 = new ConnectionGene(idGenerator.NextInnovationId, connectionToReplace.SourceNeuronId, newNeuronGene.InnovationId, 1.0);
                    newConnectionGene2 = new ConnectionGene(idGenerator.NextInnovationId, newNeuronGene.InnovationId, connectionToReplace.TargetNeuronId, connectionToReplace.Weight);

                    // Register the new IDs with NewNeuronGeneStructTable.
                    newNeuronGeneStructTable.Add(connectionToReplace.InnovationId,
                                                    new NewNeuronGeneStruct(newNeuronGene, newConnectionGene1, newConnectionGene2));
                }
                else
                {	// An existing matching structure has been found. Re-use its ID's

                    if (offspring.NeuronGeneList.GetNeuronById(((NewNeuronGeneStruct)existingNeuronGeneStruct).NewNeuronGene.InnovationId) != null)
                    {
                        continue;
                    }

                    // Replace connectionToReplace with two new connections and a neuron.
                    actFunct = ActivationFunctionFactory.GetRandomActivationFunction(neatParams);
                    NewNeuronGeneStruct tmpStruct = (NewNeuronGeneStruct)existingNeuronGeneStruct;
                    newNeuronGene = new NeuronGene(tmpStruct.NewNeuronGene.InnovationId, NeuronType.Hidden, actFunct);
                    newConnectionGene1 = new ConnectionGene(tmpStruct.NewConnectionGene_Input.InnovationId, connectionToReplace.SourceNeuronId, newNeuronGene.InnovationId, (Utilities.NextDouble() * neatParams.connectionWeightRange) - neatParams.connectionWeightRange / 2.0);
                    newConnectionGene2 = new ConnectionGene(tmpStruct.NewConnectionGene_Output.InnovationId, newNeuronGene.InnovationId, connectionToReplace.TargetNeuronId, (Utilities.NextDouble() * neatParams.connectionWeightRange / 8.0) - neatParams.connectionWeightRange / 16.0);
                }

                // Add the new genes to the genome.
                offspring.NeuronGeneList.Add(newNeuronGene);
                offspring.ConnectionGeneList.InsertIntoPosition(newConnectionGene1);
                offspring.ConnectionGeneList.InsertIntoPosition(newConnectionGene2);
                break;
            }

            return offspring;
        }

        private NeatGenome Mutate_AddModule(NeatGenome parentGenome)
        {
            NeatGenome offspring = new NeatGenome(parentGenome, idGenerator.NextGenomeId);

            // Find all potential inputs, or quit if there are not enough. 
            // Neurons cannot be inputs if they are dummy input nodes created for another module.
            NeuronGeneList potentialInputs = new NeuronGeneList();
            foreach (NeuronGene n in offspring.NeuronGeneList)
            {
                if (!(n.ActivationFunction is ModuleInputNeuron))
                {
                    potentialInputs.Add(n);
                }
            }
            if (potentialInputs.Count < 1)
                return offspring;

            // Find all potential outputs, or quit if there are not enough.
            // Neurons cannot be outputs if they are dummy input or output nodes created for another module, or network input or bias nodes.
            NeuronGeneList potentialOutputs = new NeuronGeneList();
            foreach (NeuronGene n in offspring.NeuronGeneList)
            {
                if (n.NeuronType != NeuronType.Bias && n.NeuronType != NeuronType.Input
                        && !(n.ActivationFunction is ModuleInputNeuron)
                        && !(n.ActivationFunction is ModuleOutputNeuron))
                {
                    potentialOutputs.Add(n);
                }
            }
            if (potentialOutputs.Count < 1)
                return offspring;

            // Pick a new function for the new module.
            IModule func = ModuleFactory.GetRandom();

            // Choose inputs uniformly at random, with replacement.
            // Create dummy neurons to represent the module's inputs.
            // Create connections between the input nodes and the dummy neurons.
            IActivationFunction inputFunction = ActivationFunctionFactory.GetActivationFunction("ModuleInputNeuron");
            List<uint> inputDummies = new List<uint>(func.InputCount);
            for (int i = 0; i < func.InputCount; i++)
            {
                NeuronGene newNeuronGene = new NeuronGene(idGenerator.NextInnovationId, NeuronType.Hidden, inputFunction);
                offspring.NeuronGeneList.Add(newNeuronGene);

                uint sourceId = potentialInputs[Utilities.Next(potentialInputs.Count)].InnovationId;
                uint targetId = newNeuronGene.InnovationId;

                inputDummies.Add(targetId);

                // Create a new connection with a new ID and add it to the Genome.
                ConnectionGene newConnectionGene = new ConnectionGene(idGenerator.NextInnovationId, sourceId, targetId,
                    (Utilities.NextDouble() * neatParams.connectionWeightRange) - neatParams.connectionWeightRange / 2.0);

                // Register the new connection with NewConnectionGeneTable.
                ConnectionEndpointsStruct connectionKey = new ConnectionEndpointsStruct(sourceId, targetId);
                newConnectionGeneTable.Add(connectionKey, newConnectionGene);

                // Add the new gene to this genome. We have a new ID so we can safely append the gene to the end 
                // of the list without risk of breaking the innovation ID order.
                offspring.ConnectionGeneList.Add(newConnectionGene);
            }

            return offspring;
        }

        private NeatGenome Mutate_AddConnection(NeatGenome parent)
        {
            NeatGenome offspring = new NeatGenome(parentGenome, idGenerator.NextGenomeId);

            // Make a fixed number of attempts at finding a suitable connection to add. 
            if (offspring.NeuronGeneList.Count > 1)
            {	// At least 2 neurons, so we have a chance at creating a connection.

                for (int attempts = 0; attempts < 5; attempts++)
                {
                    // Find all potential inputs, or quit if there are not enough. 
                    // Neurons cannot be inputs if they are dummy input nodes of a module.
                    NeuronGeneList potentialInputs = new NeuronGeneList();
                    foreach (NeuronGene n in offspring.NeuronGeneList)
                    {
                        if (!(n.ActivationFunction is ModuleInputNeuron))
                        {
                            potentialInputs.Add(n);
                        }
                    }
                    if (potentialInputs.Count < 1)
                        return offspring;

                    // Find all potential outputs, or quit if there are not enough.
                    // Neurons cannot be outputs if they are dummy input or output nodes of a module, or network input or bias nodes.
                    NeuronGeneList potentialOutputs = new NeuronGeneList();
                    foreach (NeuronGene n in offspring.NeuronGeneList)
                    {
                        if (n.NeuronType != NeuronType.Bias && n.NeuronType != NeuronType.Input
                                && !(n.ActivationFunction is ModuleInputNeuron)
                                && !(n.ActivationFunction is ModuleOutputNeuron))
                        {
                            potentialOutputs.Add(n);
                        }
                    }
                    if (potentialOutputs.Count < 1)
                        return offspring;

                    NeuronGene sourceNeuron = potentialInputs[Utilities.Next(potentialInputs.Count)];
                    NeuronGene targetNeuron = potentialOutputs[Utilities.Next(potentialOutputs.Count)];

                    // Check if a connection already exists between these two neurons.
                    uint sourceId = sourceNeuron.InnovationId;
                    uint targetId = targetNeuron.InnovationId;

                    if (!TestForExistingConnection(offspring, sourceId, targetId))
                    {
                        // Check if a matching mutation has already occured on another genome. 
                        // If so then re-use the connection ID.
                        ConnectionEndpointsStruct connectionKey = new ConnectionEndpointsStruct(sourceId, targetId);
                        ConnectionGene existingConnection = (ConnectionGene)newConnectionGeneTable[connectionKey];
                        ConnectionGene newConnectionGene;
                        if (existingConnection == null)
                        {	// Create a new connection with a new ID and add it to the Genome.
                            newConnectionGene = new ConnectionGene(idGenerator.NextInnovationId, sourceId, targetId,
                                (Utilities.NextDouble() * neatParams.connectionWeightRange / 4.0) - neatParams.connectionWeightRange / 8.0);

                            // Register the new connection with NewConnectionGeneTable.
                            newConnectionGeneTable.Add(connectionKey, newConnectionGene);

                            // Add the new gene to this genome. We have a new ID so we can safely append the gene to the end 
                            // of the list without risk of breaking the innovation ID order.
                            offspring.ConnectionGeneList.Add(newConnectionGene);
                        }
                        else
                        {	// Create a new connection, re-using the ID from existingConnection, and add it to the Genome.
                            newConnectionGene = new ConnectionGene(existingConnection.InnovationId, sourceId, targetId,
                                (Utilities.NextDouble() * neatParams.connectionWeightRange / 4.0) - neatParams.connectionWeightRange / 8.0);

                            // Add the new gene to this genome. We are re-using an ID so we must ensure the connection gene is
                            // inserted into the correct position (sorted by innovation ID).
                            offspring.ConnectionGeneList.InsertIntoPosition(newConnectionGene);
                        }

                        return offspring;
                    }
                }
            }

            // We couldn't find a valid connection to create. Instead of doing nothing lets perform connection
            // weight mutation.
            return Mutate_ConnectionWeights(offspring);
        }

        private NeatGenome Mutate_DeleteConnection(NeatGenome parentGenome)
        {
            NeatGenome offspring = new NeatGenome(parentGenome, idGenerator.NextGenomeId);

            if (offspring.ConnectionGeneList.Count == 0)
                return offspring;

            // Select a connection at random.
            int connectionToDeleteIdx = (int)Math.Floor(Utilities.NextDouble() * offspring.ConnectionGeneList.Count);
            ConnectionGene connectionToDelete = offspring.ConnectionGeneList[connectionToDeleteIdx];

            // Delete the connection.
            offspring.ConnectionGeneList.RemoveAt(connectionToDeleteIdx);

            // Remove any neurons that may have been left floating.
            if (IsNeuronRedundant(offspring, connectionToDelete.SourceNeuronId))
                offspring.NeuronGeneList.Remove(connectionToDelete.SourceNeuronId);

            // Recurrent connection has both end points at the same neuron!
            if (connectionToDelete.SourceNeuronId != connectionToDelete.TargetNeuronId)
                if (IsNeuronRedundant(offspring, connectionToDelete.TargetNeuronId))
                    offspring.NeuronGeneList.Remove(connectionToDelete.TargetNeuronId);

            return offspring;
        }

        /// <summary>
        /// We define a simple neuron structure as a neuron that has a single outgoing or single incoming connection.
        /// With such a structure we can easily eliminate the neuron and shift it's connections to an adjacent neuron.
        /// If the neuron's non-linearity was not being used then such a mutation is a simplification of the network
        /// structure that shouldn't adversly affect its functionality.
        /// </summary>
        private NeatGenome Mutate_DeleteSimpleNeuronStructure(NeatGenome parentGenome)
        {
            Hashtable neuronConnectionLookupTable = null;
            Hashtable neuronGeneTable = null;

            NeatGenome offspring = new NeatGenome(parentGenome, idGenerator.NextGenomeId);

            // We will use the NeuronConnectionLookupTable to find the simple structures.
            neuronConnectionLookupTable = EnsureNeuronConnectionLookupTable(offspring, neuronConnectionLookupTable, neuronGeneTable);

            // Build a list of candidate simple neurons to choose from.
            ArrayList simpleNeuronIdList = new ArrayList();

            foreach (NeuronConnectionLookup lookup in neuronConnectionLookupTable.Values)
            {
                // If we test the connection count with <=1 then we also pick up neurons that are in dead-end circuits, 
                // RemoveSimpleNeuron is then able to delete these neurons from the network structure along with any 
                // associated connections.
                // All neurons that are part of a module would appear to be dead-ended, but skip removing them anyway.
                if (lookup.neuronGene.NeuronType == NeuronType.Hidden
                            && !(lookup.neuronGene.ActivationFunction is ModuleInputNeuron)
                            && !(lookup.neuronGene.ActivationFunction is ModuleOutputNeuron))
                {
                    if ((lookup.incomingList.Count <= 1) || (lookup.outgoingList.Count <= 1))
                        simpleNeuronIdList.Add(lookup.neuronGene.InnovationId);
                }
            }

            // Are there any candiate simple neurons?
            if (simpleNeuronIdList.Count == 0)
            {	
                // No candidate neurons. As a fallback lets delete a connection.
                return Mutate_DeleteConnection(offspring);
            }

            // Pick a simple neuron at random.
            int idx = (int)Math.Floor(Utilities.NextDouble() * simpleNeuronIdList.Count);
            uint neuronId = (uint)simpleNeuronIdList[idx];
            return RemoveSimpleNeuron(offspring, neuronId, neuronConnectionLookupTable);
        }

        private Hashtable EnsureNeuronConnectionLookupTable(NeatGenome genome, Hashtable neuronConnectionLookupTable, Hashtable neuronGeneTable)
        {
            if (neuronConnectionLookupTable == null)
                return BuildNeuronConnectionLookupTable(genome, neuronGeneTable);
            else
                return neuronConnectionLookupTable;
        }

        private Hashtable BuildNeuronConnectionLookupTable(NeatGenome genome, Hashtable neuronGeneTable)
        {
            Hashtable neuronTable = EnsureNeuronTable(genome, neuronGeneTable);

            Hashtable neuronConnectionLookupTable = new Hashtable();
            foreach (ConnectionGene connectionGene in genome.ConnectionGeneList)
            {
                neuronConnectionLookupTable = BuildNeuronConnectionLookupTable_NewIncomingConnection(connectionGene.TargetNeuronId, connectionGene, neuronConnectionLookupTable, neuronGeneTable);
                neuronConnectionLookupTable = BuildNeuronConnectionLookupTable_NewOutgoingConnection(connectionGene.TargetNeuronId, connectionGene, neuronConnectionLookupTable, neuronGeneTable);
            }

            return neuronConnectionLookupTable;
        }

        private Hashtable EnsureNeuronTable(NeatGenome genome, Hashtable neuronGeneTable)
        {
            if (neuronGeneTable == null)
                return BuildNeuronTable(genome);
            else
                return neuronGeneTable;
        }

        private Hashtable BuildNeuronTable(NeatGenome genome)
        {
            Hashtable neuronGeneTable = new Hashtable();

            foreach (NeuronGene neuronGene in genome.NeuronGeneList)
                neuronGeneTable.Add(neuronGene.InnovationId, neuronGene);

            return neuronGeneTable;
        }

        private Hashtable BuildNeuronConnectionLookupTable_NewIncomingConnection(uint neuronId, ConnectionGene connectionGene, Hashtable neuronConnectionLookupTable, Hashtable neuronGeneTable)
        {
            // Is this neuron already known to the lookup table?
            NeuronConnectionLookup lookup = (NeuronConnectionLookup)neuronConnectionLookupTable[neuronId];
            if (lookup == null)
            {	
                // Creae a new lookup entry for this neuron Id.
                lookup = new NeuronConnectionLookup();
                lookup.neuronGene = (NeuronGene)neuronGeneTable[neuronId];
                neuronConnectionLookupTable.Add(neuronId, lookup);
            }

            // Register the connection with the NeuronConnectionLookup object.
            lookup.incomingList.Add(connectionGene);
            return neuronConnectionLookupTable;
        }

        private Hashtable BuildNeuronConnectionLookupTable_NewOutgoingConnection(uint neuronId, ConnectionGene connectionGene, Hashtable neuronConnectionLookupTable, Hashtable neuronGeneTable)
        {
            // Is this neuron already known to the lookup table?
            NeuronConnectionLookup lookup = (NeuronConnectionLookup)neuronConnectionLookupTable[neuronId];
            if (lookup == null)
            {	
                // Create a new lookup entry for this neuron Id.
                lookup = new NeuronConnectionLookup();
                lookup.neuronGene = (NeuronGene)neuronGeneTable[neuronId];
                neuronConnectionLookupTable.Add(neuronId, lookup);
            }

            // Register the connection with the NeuronConnectionLookup object.
            lookup.outgoingList.Add(connectionGene);
            return neuronConnectionLookupTable;
        }

        private bool TestForExistingConnection(NeatGenome genome, uint sourceId, uint targetId)
        {
            for (int connectionIdx = 0; connectionIdx < genome.ConnectionGeneList.Count; connectionIdx++)
            {
                ConnectionGene connectionGene = genome.ConnectionGeneList[connectionIdx];
                if (connectionGene.SourceNeuronId == sourceId && connectionGene.TargetNeuronId == targetId)
                    return true;
            }
            return false;
        }

        private NeatGenome RemoveSimpleNeuron(NeatGenome genome, uint neuronId, Hashtable neuronConnectionLookupTable)
        {
            // Create new connections that connect all of the incoming and outgoing neurons
            // that currently exist for the simple neuron. 
            NeuronConnectionLookup lookup = (NeuronConnectionLookup)neuronConnectionLookupTable[neuronId];
            foreach (ConnectionGene incomingConnection in lookup.incomingList)
            {
                foreach (ConnectionGene outgoingConnection in lookup.outgoingList)
                {
                    if (TestForExistingConnection(genome, incomingConnection.SourceNeuronId, outgoingConnection.TargetNeuronId))
                    {	
                        // Connection already exists.
                        continue;
                    }

                    // Test for matching connection within NewConnectionGeneTable.
                    ConnectionEndpointsStruct connectionKey = new ConnectionEndpointsStruct(incomingConnection.SourceNeuronId,
                                                                                            outgoingConnection.TargetNeuronId);
                    ConnectionGene existingConnection = (ConnectionGene)newConnectionGeneTable[connectionKey];
                    ConnectionGene newConnectionGene;
                    if (existingConnection == null)
                    {	
                        // No matching connection found. Create a connection with a new ID.
                        newConnectionGene = new ConnectionGene(idGenerator.NextInnovationId,
                            incomingConnection.SourceNeuronId,
                            outgoingConnection.TargetNeuronId,
                            (Utilities.NextDouble() * neatParams.connectionWeightRange) - neatParams.connectionWeightRange / 2.0);

                        // Register the new ID with NewConnectionGeneTable.
                        newConnectionGeneTable.Add(connectionKey, newConnectionGene);

                        // Add the new gene to the genome.
                        genome.ConnectionGeneList.Add(newConnectionGene);
                    }
                    else
                    {	// Matching connection found. Re-use its ID.
                        newConnectionGene = new ConnectionGene(existingConnection.InnovationId,
                            incomingConnection.SourceNeuronId,
                            outgoingConnection.TargetNeuronId,
                            (Utilities.NextDouble() * neatParams.connectionWeightRange) - neatParams.connectionWeightRange / 2.0);

                        // Add the new gene to the genome. Use InsertIntoPosition() to ensure we don't break the sort 
                        // order of the connection genes.
                        genome.ConnectionGeneList.InsertIntoPosition(newConnectionGene);
                    }
                }
            }

            // Delete the old connections.
            foreach (ConnectionGene incomingConnection in lookup.incomingList)
                genome.ConnectionGeneList.Remove(incomingConnection);

            foreach (ConnectionGene outgoingConnection in lookup.outgoingList)
            {
                // Filter out recurrent connections - they will have already been 
                // deleted in the loop through 'lookup.incomingList'.
                if (outgoingConnection.TargetNeuronId != neuronId)
                    genome.ConnectionGeneList.Remove(outgoingConnection);
            }

            // Delete the simple neuron - it no longer has any connections to or from it.
            genome.NeuronGeneList.Remove(neuronId);
            return genome;
        }

        private NeatGenome Mutate_ConnectionWeights(NeatGenome parentGenome)
        {
            NeatGenome offspring = new NeatGenome(parentGenome, idGenerator.NextGenomeId);

            // Determine the type of weight mutation to perform.
            int groupCount = neatParams.ConnectionMutationParameterGroupList.Count;
            double[] probabilties = new double[groupCount];
            for (int i = 0; i < groupCount; i++)
            {
                probabilties[i] = ((ConnectionMutationParameterGroup)neatParams.ConnectionMutationParameterGroupList[i]).ActivationProportion;
            }

            // Get a reference to the group we will be using.			
            ConnectionMutationParameterGroup paramGroup = (ConnectionMutationParameterGroup)neatParams.ConnectionMutationParameterGroupList[RouletteWheel.SingleThrow(probabilties)];

            // Perform mutations of the required type.
            if (paramGroup.SelectionType == ConnectionSelectionType.Proportional)
            {
                bool mutationOccured = false;
                int connectionCount = offspring.ConnectionGeneList.Count;
                for (int i = 0; i < connectionCount; i++)
                {
                    if (Utilities.NextDouble() < paramGroup.Proportion)
                    {
                        MutateConnectionWeight(offspring.ConnectionGeneList[i], neatParams, paramGroup);
                        mutationOccured = true;
                    }
                }
                if (!mutationOccured && connectionCount > 0)
                {	
                    // Perform at least one mutation. Pick a gene at random.
                    MutateConnectionWeight(offspring.ConnectionGeneList[(int)(Utilities.NextDouble() * connectionCount)],
                                            neatParams,
                                            paramGroup);
                }
            }
            else 
            {
                // Determine how many mutations to perform. At least one - if there are any genes.
                int connectionCount = offspring.ConnectionGeneList.Count;
                int mutations = Math.Min(connectionCount, Math.Max(1, paramGroup.Quantity));
                if (mutations == 0) return offspring;

                // The mutation loop. Here we pick an index at random and scan forward from that point
                // for the first non-mutated gene. This prevents any gene from being mutated more than once without
                // too much overhead. In fact it's optimal for small numbers of mutations where clashes are unlikely 
                // to occur.
                for (int i = 0; i < mutations; i++)
                {
                    // Pick an index at random.
                    int index = (int)(Utilities.NextDouble() * connectionCount);
                    ConnectionGene connectionGene = offspring.ConnectionGeneList[index];

                    // Scan forward and find the first non-mutated gene.
                    while (offspring.ConnectionGeneList[index].IsMutated)
                    {	// Increment index. Wrap around back to the start if we go off the end.
                        if (++index == connectionCount)
                            index = 0;
                    }

                    // Mutate the gene at 'index'.
                    MutateConnectionWeight(offspring.ConnectionGeneList[index], neatParams, paramGroup);
                    offspring.ConnectionGeneList[index].IsMutated = true;
                }
            }

            return offspring;
        }

        private void MutateConnectionWeight(ConnectionGene connectionGene, NeatParameters np, ConnectionMutationParameterGroup paramGroup)
        {
            switch (paramGroup.PerturbationType)
            {
                case ConnectionPerturbationType.JiggleEven:
                    {
                        connectionGene.Weight += (Utilities.NextDouble() * 2 - 1.0) * paramGroup.PerturbationFactor;

                        // Cap the connection weight. Large connections weights reduce the effectiveness of the search.
                        connectionGene.Weight = Math.Max(connectionGene.Weight, -np.connectionWeightRange / 2.0);
                        connectionGene.Weight = Math.Min(connectionGene.Weight, np.connectionWeightRange / 2.0);
                        break;
                    }
                case ConnectionPerturbationType.JiggleND:
                    {
                        connectionGene.Weight += RandLib.gennor(0, paramGroup.Sigma);

                        // Cap the connection weight. Large connections weights reduce the effectiveness of the search.
                        connectionGene.Weight = Math.Max(connectionGene.Weight, -np.connectionWeightRange / 2.0);
                        connectionGene.Weight = Math.Min(connectionGene.Weight, np.connectionWeightRange / 2.0);
                        break;
                    }
                case ConnectionPerturbationType.Reset:
                    {
                        // TODO: Precalculate connectionWeightRange / 2.
                        connectionGene.Weight = (Utilities.NextDouble() * np.connectionWeightRange) - np.connectionWeightRange / 2.0;
                        break;
                    }
                default:
                    {
                        throw new Exception("Unexpected ConnectionPerturbationType");
                    }
            }
        }

        /// <summary>
        /// If the neuron is a hidden neuron and no connections connect to it then it is redundant.
        /// No neuron is redundant that is part of a module (although the module itself might be found redundant separately).
        /// </summary>
        private bool IsNeuronRedundant(NeatGenome genome, uint neuronId)
        {
            NeuronGene neuronGene = genome.NeuronGeneList.GetNeuronById(neuronId);
            if (neuronGene.NeuronType != NeuronType.Hidden
                        || neuronGene.ActivationFunction is ModuleInputNeuron
                        || neuronGene.ActivationFunction is ModuleOutputNeuron)
                return false;

            return !IsNeuronConnected(genome, neuronId);
        }

        private bool IsNeuronConnected(NeatGenome genome, uint neuronId)
        {
            int bound = genome.ConnectionGeneList.Count;
            for (int i = 0; i < bound; i++)
            {
                ConnectionGene connectionGene = genome.ConnectionGeneList[i];
                if (connectionGene.SourceNeuronId == neuronId)
                    return true;

                if (connectionGene.TargetNeuronId == neuronId)
                    return true;
            }

            return false;
        }
    }
}