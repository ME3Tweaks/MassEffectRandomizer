using AlotAddOnGUI.classes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MassEffectRandomizer.Classes
{
    class Randomizer
    {
        private const string UPDATE_RANDOMIZING_TEXT = "UPDATE_RANDOMIZING_TEXT";
        private MainWindow mainWindow;
        private BackgroundWorker randomizationWorker;
        public Randomizer(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;


        }

        public void randomize()
        {
            randomizationWorker = new BackgroundWorker();
            randomizationWorker.DoWork += PerformRandomization;
            randomizationWorker.ProgressChanged += Randomization_ProgressChanged;
            randomizationWorker.WorkerReportsProgress = true;
            randomizationWorker.RunWorkerAsync();
        }

        private void Randomization_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ThreadCommand tc = (ThreadCommand)e.UserState;
            switch (tc.Command)
            {
                case UPDATE_RANDOMIZING_TEXT:
                    mainWindow.Textblock_CurrentTask.Text = (string)tc.Data;
                    break;
            }
        }

        private void PerformRandomization(object sender, DoWorkEventArgs e)
        {
            RandomizeGalaxyMap();
            randomizationWorker.ReportProgress(0, new ThreadCommand(UPDATE_RANDOMIZING_TEXT, "Done"));

        }

        private void RandomizeGalaxyMap()
        {
            Random random = new Random();
            ME1Package engine = new ME1Package(@"X:\Mass Effect Games HDD\Mass Effect\BioGame\CookedPC\Engine.u");
            foreach (IExportEntry export in engine.Exports)
            {
                switch (export.ObjectName)
                {
                    case "GalaxyMap_Cluster":
                        RandomizeClusters(export, random);
                        break;
                    case "GalaxyMap_System":
                        RandomizeSystems(export, random);
                        break;
                    case "GalaxyMap_Planet":
                        RandomizePlanets(export, random);
                        break;
                }
            }
            randomizationWorker.ReportProgress(0, new ThreadCommand(UPDATE_RANDOMIZING_TEXT, "Finishing Galaxy Map Randomizing"));

            engine.save();
        }

        /// <summary>
        /// Randomizes the highest-level galaxy map view. Values are between 0 and 1 for columns 1 and 2 (X,Y).
        /// </summary>
        /// <param name="export">2DA Export</param>
        /// <param name="random">Random nubmer generator</param>
        private void RandomizeClusters(IExportEntry export, Random random)
        {
            randomizationWorker.ReportProgress(0,new ThreadCommand(UPDATE_RANDOMIZING_TEXT, "Randomizing Galaxy Map - Clusters"));

            Bio2DA cluster2da = new Bio2DA(export);
            int[] colsToRandomize = { 1, 2 };
            for (int row = 0; row < cluster2da.rowNames.Count(); row++)
            {
                for (int i = 0; i < colsToRandomize.Count(); i++)
                {
                    //Console.WriteLine("[" + row + "][" + colsToRandomize[i] + "] value is " + BitConverter.ToSingle(cluster2da[row, colsToRandomize[i]].Data, 0));
                    float randvalue = random.NextFloat(0, 1);
                    Console.WriteLine("Cluster Randomizer [" + row + "][" + colsToRandomize[i] + "] value is now " + randvalue);
                    cluster2da[row, colsToRandomize[i]].Data = BitConverter.GetBytes(randvalue);
                }
            }
            cluster2da.Write2DAToExport();
        }

        /// <summary>
        /// Randomizes the mid-level galaxy map view. 
        /// </summary>
        /// <param name="export">2DA Export</param>
        /// <param name="random">Random nubmer generator</param>
        private void RandomizeSystems(IExportEntry export, Random random)
        {
            randomizationWorker.ReportProgress(0, new ThreadCommand(UPDATE_RANDOMIZING_TEXT, "Randomizing Galaxy Map - Systems"));

            Console.WriteLine("Randomizing Galaxy Map - Systems");
            Bio2DA system2da = new Bio2DA(export);
            int[] colsToRandomize = { 2, 3 };//X,Y
            for (int row = 0; row < system2da.rowNames.Count(); row++)
            {
                for (int i = 0; i < colsToRandomize.Count(); i++)
                {
                    //Console.WriteLine("[" + row + "][" + colsToRandomize[i] + "] value is " + BitConverter.ToSingle(system2da[row, colsToRandomize[i]].Data, 0));
                    float randvalue = random.NextFloat(0, 1);
                    Console.WriteLine("System Randomizer [" + row + "][" + colsToRandomize[i] + "] value is now " + randvalue);
                    system2da[row, colsToRandomize[i]].Data = BitConverter.GetBytes(randvalue);
                }
                //string value = system2da[row, 9].GetDisplayableValue();
                //Console.WriteLine("Scale: [" + row + "][9] value is " + value);
                float scalerandvalue = random.NextFloat(0.25, 2);
                Console.WriteLine("System Randomizer [" + row + "][9] value is now " + scalerandvalue);
                system2da[row, 9].Data = BitConverter.GetBytes(scalerandvalue);
                system2da[row, 9].Type = Bio2DACell.TYPE_FLOAT;
            }
            system2da.Write2DAToExport();
        }

        /// <summary>
        /// Randomizes the planet-level galaxy map view. 
        /// </summary>
        /// <param name="export">2DA Export</param>
        /// <param name="random">Random nubmer generator</param>
        private void RandomizePlanets(IExportEntry export, Random random)
        {
            randomizationWorker.ReportProgress(0, new ThreadCommand(UPDATE_RANDOMIZING_TEXT, "Randomizing Galaxy Map - Planets"));

            Console.WriteLine("Randomizing Galaxy Map - Planets");
            Bio2DA planet2da = new Bio2DA(export);
            int[] colsToRandomize = { 2, 3 };//X,Y
            for (int row = 0; row < planet2da.rowNames.Count(); row++)
            {
                for (int i = 0; i < planet2da.columnNames.Count(); i++)
                {
                    if (planet2da[row, i] != null && planet2da[row, i].Type == Bio2DACell.TYPE_FLOAT)
                    {
                        Thread.Sleep(200);
                        Console.WriteLine("[" + row + "][" + i + "]  (" + planet2da.columnNames[i] + ") value is " + BitConverter.ToSingle(planet2da[row, i].Data, 0));
                        float randvalue = random.NextFloat(0, 1);
                        if (i == 11)
                        {
                            randvalue = random.NextFloat(0.4, 2);
                        }
                        Console.WriteLine("Planets Randomizer [" + row + "][" + i + "] (" + planet2da.columnNames[i] + ") value is now " + randvalue);
                        planet2da[row, i].Data = BitConverter.GetBytes(randvalue);
                    }
                }
            }
            planet2da.Write2DAToExport();
        }

        static float NextFloat(Random random)
        {
            double mantissa = (random.NextDouble() * 2.0) - 1.0;
            double exponent = Math.Pow(2.0, random.Next(-3, 20));
            return (float)(mantissa * exponent);
        }
    }
}
