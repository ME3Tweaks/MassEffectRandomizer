﻿using AlotAddOnGUI.classes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassEffectRandomizer.Classes.RandomizationAlgorithms;
using Microsoft.WindowsAPICodePack.Taskbar;
using System.IO;
using System.Windows;
using System.Collections.Concurrent;
using MassEffectRandomizer.Classes.TLK;
using static MassEffectRandomizer.Classes.RandomizationAlgorithms.TalentEffectLevels;
using static MassEffectRandomizer.MainWindow;
using System.Xml.Serialization;
using System.Xml;
using System.Xml.Linq;

namespace MassEffectRandomizer.Classes
{
    class Randomizer
    {
        private static readonly string[] RandomClusterNameCollection = {
"Serpent Cluster","Zero","Artemis","Kamino","Kovac Nebula", "Akkala","Lanayru Verge","Kyramud","Tolase","Kirigiri","Ascension Sigma", "Epsilon","Rodin","Gilgamesh","Enkidu","Ventus","Agrias","Canopus","Tartarose","Dorgalua","Losstarot","Onyx Tau","Himura", "Baltoy","Canopy Xi"
};


        private static readonly string[] RandomSystemNameCollection = { "Lylat", "Cygnus Wing", "Omega-Xis", "Ophiuca", "Godot", "Gemini", "Cepheus", "Boreal", "Lambda Scorpii", "Polaris", "Corvus", "Atreides", "Mira", "Kerh-S", "Odyssey", "Xi Draconis", "System o’ Hags", "Sirius", "Osiris", "Forsaken", "Daibazaal", "Tamriel", "Cintra", "Redania", "Dunwall", "Ouroboros", "Alinos", "Chozodia", "Hollow Bastion", "Mac Anu", "Dol Dona", "Breg Epona", "Tartarga", "Rozarria", "Gondolin", "Nargothrond", "Numenor", "Beleriand", "Valinor", "Thedas", "Vulcan", "Magmoor", "Hulick", "Infinity", "Atlas", "Hypnos", "Janus", "Cosmic Wall", "Gra’tua Cuun", "Ghost" };

        private const string UPDATE_RANDOMIZING_TEXT = "UPDATE_RANDOMIZING_TEXT";
        private MainWindow mainWindow;
        private BackgroundWorker randomizationWorker;
        private ConcurrentDictionary<string, string> ModifiedFiles;
        public Randomizer(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
        }

        public void randomize()
        {
            randomizationWorker = new BackgroundWorker();
            randomizationWorker.DoWork += PerformRandomization;
            randomizationWorker.RunWorkerCompleted += Randomization_Completed;

            var seedStr = mainWindow.SeedTextBox.Text;
            if (!int.TryParse(seedStr, out int seed))
            {
                seed = new Random().Next();
                mainWindow.SeedTextBox.Text = seed.ToString();
            }
            randomizationWorker.RunWorkerAsync(seed);
            TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Indeterminate, mainWindow);
        }

        private void Randomization_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.NoProgress, mainWindow);
            mainWindow.CurrentOperationText = "Randomization complete";

            mainWindow.ProgressPanelVisible = System.Windows.Visibility.Collapsed;
            mainWindow.ButtonPanelVisible = System.Windows.Visibility.Visible;
            string backupPath = Utilities.GetGameBackupPath();
            string gamePath = Utilities.GetGamePath();
            if (backupPath != null)
            {
                foreach (KeyValuePair<string, string> kvp in ModifiedFiles)
                {
                    string filepathrel = kvp.Key.Substring(gamePath.Length + 1);

                    Debug.WriteLine($"copy /y \"{Path.Combine(backupPath, filepathrel)}\" \"{Path.Combine(gamePath, filepathrel)}\"");
                }
            }
        }

        private void PerformRandomization(object sender, DoWorkEventArgs e)
        {
            ME1UnrealObjectInfo.loadfromJSON();
            ModifiedFiles = new ConcurrentDictionary<string, string>(); //this will act as a Set since there is no ConcurrentSet
            Random random = new Random((int)e.Argument);

            //Load TLKs
            mainWindow.CurrentOperationText = "Loading TLKs";
            mainWindow.ProgressBarIndeterminate = true;
            string globalTLKPath = Path.Combine(Utilities.GetGamePath(), "BioGame", "CookedPC", "Packages", "Dialog", "GlobalTlk.upk");
            ME1Package globalTLK = new ME1Package(globalTLKPath);
            List<TalkFile> Tlks = new List<TalkFile>();
            foreach (IExportEntry exp in globalTLK.Exports)
            {
                if (exp.ClassName == "BioTlkFile")
                {
                    TalkFile tlk = new TalkFile(exp);
                    Tlks.Add(tlk);
                }
            }
            ////Test
            //ME1Package test = new ME1Package(@"D:\Origin Games\Mass Effect\BioGame\CookedPC\Maps\STA\DSG\BIOA_STA60_06_DSG.SFM");
            //var morphFaces = test.Exports.Where(x => x.ClassName == "BioMorphFace").ToList();
            //morphFaces.ForEach(x => RandomizeBioMorphFace(x, random));
            //test.save();
            //return;

            //Randomize ENGINE
            ME1Package engine = new ME1Package(Utilities.GetEngineFile());
            IExportEntry talentEffectLevels = null;

            foreach (IExportEntry export in engine.Exports)
            {
                switch (export.ObjectName)
                {
                    case "Music_Music":
                        if (mainWindow.RANDSETTING_MISC_MUSIC)
                        {
                            RandomizeMusic(export, random);
                        }
                        break;
                    case "UISounds_GuiMusic":
                        if (mainWindow.RANDSETTING_MISC_GUIMUSIC)
                        {
                            RandomizeGUISounds(export, random, "Randomizing GUI Sounds - Music", "music");
                        }
                        break;
                    case "UISounds_GuiSounds":
                        if (mainWindow.RANDSETTING_MISC_GUISFX)
                        {
                            RandomizeGUISounds(export, random, "Randomizing GUI Sounds - Sounds", "snd_gui");
                        }
                        break;
                    case "MovementTables_CreatureSpeeds":
                        if (mainWindow.RANDSETTING_MOVEMENT_CREATURESPEED)
                        {
                            RandomizeMovementSpeeds(export, random);
                        }
                        break;
                    case "GalaxyMap_Cluster":
                        if (mainWindow.RANDSETTING_GALAXYMAP_CLUSTERS)
                        {
                            RandomizeClusters(export, random,Tlks);
                        }
                        break;
                    case "GalaxyMap_System":
                        if (mainWindow.RANDSETTING_GALAXYMAP_SYSTEMS)
                        {
                            RandomizeSystems(export, random);
                        }
                        break;
                    case "GalaxyMap_Planet":
                        //DumpPlanetTexts(export, Tlks[0]);
                        //return;

                        if (mainWindow.RANDSETTING_GALAXYMAP_PLANETCOLOR)
                        {
                            RandomizePlanets(export, random);
                        }
                        if (mainWindow.RANDSETTING_GALAXYMAP_PLANETNAMEDESCRIPTION)
                        {
                            RandomizePlanetNameDescriptions(export, random, Tlks);
                        }
                        break;
                    case "Characters_StartingEquipment":
                        if (mainWindow.RANDSETTING_WEAPONS_STARTINGEQUIPMENT)
                        {
                            RandomizeStartingWeapons(export, random);
                        }
                        break;
                    case "Classes_ClassTalents":
                        if (mainWindow.RANDSETTING_TALENTS_SHUFFLECLASSTALENTS)
                        {
                            ShuffleClassTalentsAndPowers(export, random);
                        }
                        break;
                    case "LevelUp_ChallengeScalingVars":
                        //RandomizeLevelUpChallenge(export, random);
                        break;
                    case "Items_ItemEffectLevels":
                        if (mainWindow.RANDSETTING_WEAPONS_EFFECTLEVELS)
                        {
                            RandomizeWeaponStats(export, random);
                        }
                        break;
                    case "Characters_Character":
                        //Has internal checks for types
                        RandomizeCharacter(export, random);
                        break;
                    case "Talent_TalentEffectLevels":
                        if (mainWindow.RANDSETTING_TALENTS_STATS)
                        {
                            RandomizeTalentEffectLevels(export, Tlks, random);
                            talentEffectLevels = export;
                        }
                        break;
                }
            }

            if (talentEffectLevels != null && mainWindow.RANDSETTING_TALENTS_SHUFFLECLASSTALENTS)
            {

            }

            engine.save();
            //RANDOMIZE ENTRYMENU
            StructProperty iconicFemaleOffsets = null;
            ME1Package entrymenu = new ME1Package(Utilities.GetEntryMenuFile());
            foreach (IExportEntry export in entrymenu.Exports)
            {
                switch (export.ObjectName)
                {
                    case "FemalePregeneratedHeads":
                    case "MalePregeneratedHeads":
                    case "BaseMaleSliders":
                    case "BaseFemaleSliders":
                        if (mainWindow.RANDSETTING_CHARACTER_CHARCREATOR)
                        {
                            RandomizePregeneratedHead(export, random);
                        }
                        break;
                    default:
                        if ((export.ClassName == "Bio2DA" || export.ClassName == "Bio2DANumberedRows") && !export.ObjectName.Contains("Default") && mainWindow.RANDSETTING_CHARACTER_CHARCREATOR)
                        {
                            RandomizeCharacterCreator(random, export);
                        }
                        break;

                        //RandomizeGalaxyMap(random);
                        //RandomizeGUISounds(random);
                        //RandomizeMusic(random);
                        //RandomizeMovementSpeeds(random);
                        //RandomizeCharacterCreator(random);
                        //Dump2DAToExcel();
                }
                if (mainWindow.RANDSETTING_CHARACTER_ICONICFACE && export.ClassName == "BioMorphFace" && export.ObjectName.StartsWith("Player_"))
                {
                    RandomizeBioMorphFace(export, random, .2);
                }
            }

            //if (mainWindow.RANDSETTING_CHARACTER_ICONICFACE && iconicMaleExport != null && iconicFemaleStructProp != null)
            //{

            //}

            entrymenu.save();


            //RANDOMIZE FACES
            if (mainWindow.RANDSETTING_CHARACTER_HENCHFACE)
            {
                RandomizeBioMorphFaceWrapper(Utilities.GetGameFile(@"BioGame\CookedPC\Packages\GameObjects\Characters\Faces\BIOG_Hench_FAC.upk"), random); //Henchmen
                RandomizeBioMorphFaceWrapper(Utilities.GetGameFile(@"BioGame\CookedPC\Packages\BIOG_MORPH_FACE.upk"), random); //Iconic and player (Not sure if this does anything...
            }

            if (mainWindow.RANDSETTING_MISC_MAPFACES)
            {
                mainWindow.CurrentOperationText = "Getting list of files...";

                mainWindow.ProgressBarIndeterminate = true;
                string path = Path.Combine(Utilities.GetGamePath(), "BioGame", "CookedPC", "Maps");
                string bdtspath = Path.Combine(Utilities.GetGamePath(), "DLC", "DLC_UNC", "CookedPC", "Maps");
                string pspath = Path.Combine(Utilities.GetGamePath(), "DLC", "DLC_Vegas", "CookedPC", "Maps");

                string[] files = Directory.GetFiles(path, "*.sfm", SearchOption.AllDirectories).Where(x => !Path.GetFileName(x).ToLower().Contains("_loc_") && Path.GetFileName(x).ToLower().Contains("dsg")).ToArray();
                if (Directory.Exists(bdtspath))
                {
                    files = files.Concat(Directory.GetFiles(bdtspath, "*.sfm", SearchOption.AllDirectories).Where(x => !Path.GetFileName(x).ToLower().Contains("_loc_") && Path.GetFileName(x).ToLower().Contains("dsg"))).ToArray();
                }
                if (Directory.Exists(pspath))
                {
                    files = files.Concat(Directory.GetFiles(pspath, "*.sfm", SearchOption.AllDirectories).Where(x => !Path.GetFileName(x).ToLower().Contains("_loc_") && Path.GetFileName(x).ToLower().Contains("dsg"))).ToArray();
                }

                mainWindow.ProgressBarIndeterminate = false;
                mainWindow.ProgressBar_Bottom_Max = files.Count();
                mainWindow.ProgressBar_Bottom_Min = 0;
                double amount = mainWindow.RANDSETTING_MISC_MAPFACES_AMOUNT;
                for (int i = 0; i < files.Length; i++)
                {
                    //                    int progress = (int)((i / total) * 100);
                    mainWindow.CurrentProgressValue = i;
                    mainWindow.CurrentOperationText = "Randomizing faces in map files [" + i + "/" + files.Count() + "]";
                    if (!files[i].ToLower().Contains("entrymenu"))
                    {
                        ME1Package package = new ME1Package(files[i]);
                        bool hasChanges = false;
                        foreach (IExportEntry exp in package.Exports)
                        {
                            if (exp.ClassName == "BioMorphFace")
                            {
                                RandomizeBioMorphFace(exp, random, amount);
                                hasChanges = true;
                            }
                        }
                        if (hasChanges)
                        {
                            ModifiedFiles[package.FileName] = package.FileName;
                            package.save();
                        }
                    }
                }
            }

            //PAWN SIZES
            if (mainWindow.RANDSETTING_MISC_MAPPAWNSIZES)
            {
                mainWindow.CurrentOperationText = "Getting list of files...";

                mainWindow.ProgressBarIndeterminate = true;
                string path = Path.Combine(Utilities.GetGamePath(), "BioGame", "CookedPC", "Maps");
                string bdtspath = Path.Combine(Utilities.GetGamePath(), "DLC", "DLC_UNC", "CookedPC", "Maps");
                string pspath = Path.Combine(Utilities.GetGamePath(), "DLC", "DLC_Vegas", "CookedPC", "Maps");

                string[] files = Directory.GetFiles(path, "*.sfm", SearchOption.AllDirectories).Where(x => !Path.GetFileName(x).ToLower().Contains("_loc_") && Path.GetFileName(x).ToLower().Contains("dsg")).ToArray();
                if (Directory.Exists(bdtspath))
                {
                    files = files.Concat(Directory.GetFiles(bdtspath, "*.sfm", SearchOption.AllDirectories).Where(x => !Path.GetFileName(x).ToLower().Contains("_loc_") && Path.GetFileName(x).ToLower().Contains("dsg"))).ToArray();
                }
                if (Directory.Exists(pspath))
                {
                    files = files.Concat(Directory.GetFiles(pspath, "*.sfm", SearchOption.AllDirectories).Where(x => !Path.GetFileName(x).ToLower().Contains("_loc_") && Path.GetFileName(x).ToLower().Contains("dsg"))).ToArray();
                }

                mainWindow.ProgressBarIndeterminate = false;
                mainWindow.ProgressBar_Bottom_Max = files.Count();
                mainWindow.ProgressBar_Bottom_Min = 0;
                double amount = 0.4;
                for (int i = 0; i < files.Length; i++)
                {
                    //                    int progress = (int)((i / total) * 100);
                    mainWindow.CurrentProgressValue = i;
                    mainWindow.CurrentOperationText = "Randomizing pawn sizes in map files [" + i + "/" + files.Count() + "]";
                    if (!files[i].ToLower().Contains("entrymenu"))
                    {
                        ME1Package package = new ME1Package(files[i]);
                        bool hasChanges = false;
                        foreach (IExportEntry export in package.Exports)
                        {
                            if (export.ClassName == "BioPawn" && random.Next(4) == 0)
                            {
                                RandomizeBioPawnSize(export, random, amount);
                                hasChanges = true;
                            }
                        }
                        if (hasChanges)
                        {
                            ModifiedFiles[package.FileName] = package.FileName;
                            package.save();
                        }
                    }
                }
            }

            bool saveGlobalTLK = false;
            foreach (TalkFile tf in Tlks)
            {
                if (tf.Modified)
                {
                    mainWindow.CurrentOperationText = "Saving TLKs";
                    tf.saveToExport();
                }
                saveGlobalTLK = true;
            }
            if (saveGlobalTLK)
            {
                globalTLK.save();
            }
        }

        public string GetResourceFileText(string filename, string assemblyName)
        {
            string result = string.Empty;

            using (Stream stream =
                System.Reflection.Assembly.Load(assemblyName).GetManifestResourceStream($"{assemblyName}.{filename}"))
            {
                using (StreamReader sr = new StreamReader(stream))
                {
                    result = sr.ReadToEnd();
                }
            }
            return result;
        }

        private void RandomizePlanetNameDescriptions(IExportEntry export, Random random, List<TalkFile> Tlks)
        {
            mainWindow.CurrentOperationText = "Randomizing planet descriptions and names";
            string fileContents = Utilities.GetEmbeddedStaticFilesTextFile("planetinfo.xml");

            XElement rootElement = XElement.Parse(fileContents);
            var allMapRandomizationInfo = (from e in rootElement.Elements("RandomizedPlanetInfo")
                                           select new RandomizedPlanetInfo
                                           {
                                               PlanetName = (string)e.Element("PlanetName"),
                                               PlanetDescription = (string)e.Element("PlanetDescription"),
                                               IsMSV = (bool)e.Element("IsMSV"),
                                               IsAsteroidBelt = (bool)e.Element("IsAsteroidBelt"),
                                               PreventShuffle = (bool)e.Element("PreventShuffle"),
                                               RowID = (int)e.Element("RowID")
                                           }).ToList();

            var msvInfos = allMapRandomizationInfo.Where(x => x.IsMSV).ToList();
            var asteroidInfos = allMapRandomizationInfo.Where(x => x.IsAsteroidBelt).ToList();
            var planetInfos = allMapRandomizationInfo.Where(x => !x.IsAsteroidBelt && !x.IsMSV && !x.PreventShuffle).ToList();

            msvInfos.Shuffle(random);
            asteroidInfos.Shuffle(random);
            planetInfos.Shuffle(random);

            List<int> rowsToNotRandomlyReassign = new List<int>();

            IExportEntry systemsExport = export.FileRef.Exports.First(x => x.ObjectName == "GalaxyMap_System");
            Bio2DA systems2DA = new Bio2DA(systemsExport);
            Bio2DA planets2DA = new Bio2DA(export);


            List<string> shuffledSystemNames = new List<string>(RandomSystemNameCollection);
            shuffledSystemNames.Shuffle(random);

            int nameColumnSystems = systems2DA.GetColumnIndexByName("Name");
            for (int i = 0; i < systems2DA.RowNames.Count; i++)
            {
                string newName = shuffledSystemNames[0];
                shuffledSystemNames.RemoveAt(0);

                int tlkRef = systems2DA[i, nameColumnSystems].GetIntValue();
                foreach (TalkFile tf in Tlks)
                {
                    tf.replaceString(tlkRef, newName);
                }
            }



            Dictionary<int, int> systemIdToTlkNameMap = new Dictionary<int, int>();
            //Used for dynamic lookup when building TLK
            for (int i = 0; i < systems2DA.RowNames.Count(); i++)
            {
                systemIdToTlkNameMap[int.Parse(systems2DA.RowNames[i])] = systems2DA[i, 4].GetIntValue();
            }

            int nameCol = planets2DA.GetColumnIndexByName("Name");
            int descCol = planets2DA.GetColumnIndexByName("Description");

            for (int i = 0; i < planets2DA.RowNames.Count; i++)
            {
                int systemId = planets2DA[i, 1].GetIntValue();
                string systemName = Tlks[0].findDataById(systemIdToTlkNameMap[systemId]);

                int nameReference = planets2DA[i, nameCol].GetIntValue();
                string currentNAme = Tlks[0].findDataById(nameReference);
                Bio2DACell descriptionRefCell = planets2DA[i, descCol];

                int descriptionReference = descriptionRefCell == null ? 0 : descriptionRefCell.GetIntValue();

                //var rowIndex = int.Parse(planets2DA.RowNames[i]);
                var info = allMapRandomizationInfo.FirstOrDefault(x => x.RowID == i);
                if (info != null)
                {
                    //found original info
                    RandomizedPlanetInfo rpi = null;
                    if (info.PreventShuffle)
                    {
                        rpi = info;
                        //Do not use shuffled

                    }
                    else
                    {
                        if (info.IsMSV)
                        {
                            rpi = msvInfos[0];
                            msvInfos.RemoveAt(0);
                        }
                        else if (info.IsAsteroidBelt)
                        {
                            rpi = asteroidInfos[0];
                            asteroidInfos.RemoveAt(0);
                        }
                        else
                        {
                            rpi = planetInfos[0];
                            planetInfos.RemoveAt(0);
                        }
                    }

                    string planetName = rpi.PlanetName;
                    //if (rename plot missions) planetName = rpi.PlanetName2
                    var description = rpi.PlanetDescription;
                    if (description != null)
                    {
                        description = description.Replace("%SYSTEMNAME%", systemName);
                        description = description.Replace("%PLANETNAME%", planetName);
                    }

                    foreach (TalkFile tf in Tlks)
                    {
                        Debug.WriteLine("Setting planet name on row index (not rowname!) " + i + " to " + rpi.PlanetName);
                        tf.replaceString(nameReference, rpi.PlanetName);
                        if (descriptionReference != 0 && description != null)
                        {
                            int truncated = Math.Min(description.Length, 25);
                            Debug.WriteLine("   Setting planet description to " + description.Substring(0, truncated));
                            tf.replaceString(descriptionReference, description);
                        }
                    }
                }
            }
        }

        private void DumpPlanetTexts(IExportEntry export, TalkFile tf)
        {
            Bio2DA planets = new Bio2DA(export);
            var planetInfos = new List<RandomizedPlanetInfo>();

            int nameRefcolumn = planets.GetColumnIndexByName("Name");
            int descColumn = planets.GetColumnIndexByName("Description");

            for (int i = 0; i < planets.RowNames.Count; i++)
            {
                RandomizedPlanetInfo rpi = new RandomizedPlanetInfo();
                rpi.PlanetName = tf.findDataById(planets[i, nameRefcolumn].GetIntValue()).Trim('"');

                var descCell = planets[i, descColumn];
                if (descCell != null)
                {
                    rpi.PlanetDescription = tf.findDataById(planets[i, 7].GetIntValue()).Trim('"');
                }
                rpi.RowID = i;
                planetInfos.Add(rpi);
            }

            using (StringWriter writer = new StringWriter())
            {
                XmlSerializer xs = new XmlSerializer(typeof(List<RandomizedPlanetInfo>));
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.OmitXmlDeclaration = true;

                XmlSerializerNamespaces namespaces = new XmlSerializerNamespaces();
                namespaces.Add(string.Empty, string.Empty);

                XmlWriter xmlWriter = XmlWriter.Create(writer, settings);
                xs.Serialize(xmlWriter, planetInfos, namespaces);

                File.WriteAllText(@"C:\users\mgame\desktop\planetinfo.xml", FormatXml(writer.ToString()));
            }
        }

        string FormatXml(string xml)
        {
            try
            {
                XDocument doc = XDocument.Parse(xml);
                return doc.ToString();
            }
            catch (Exception)
            {
                // Handle and throw if fatal exception here; don't just ignore them
                return xml;
            }
        }

        private void RandomizeBioPawnSize(IExportEntry export, Random random, double amount)
        {
            var props = export.GetProperties();
            StructProperty sp = props.GetProp<StructProperty>("DrawScale3D");
            if (sp == null)
            {
                //Debug.WriteLine("=== READING EXISTING VALUEs...");
                //we need to insert it
                int propStart = export.GetPropertyStart(); //get old start
                int propEnd = export.propsEnd(); //get old end

                List<byte> data = export.Data.Skip(propStart).Take(propEnd - propStart).ToList();
                var newBytes = PropertyCollection.GetBytesForNewVectorProperty(export.FileRef, "DrawScale3D");
                data.InsertRange(data.Count - 8, newBytes);
                //Debug.WriteLine("=== READING NEW VALUEs...");

                //var newproperties = PropertyCollection.ReadProps(export.FileRef, new MemoryStream(data.ToArray()), export.ClassName, true, true);

                var stream = new MemoryStream(data.ToArray());
                stream.Seek(0, SeekOrigin.Current);

                props = PropertyCollection.ReadProps(export.FileRef, stream, export.ClassName, true, true, export.ObjectName);
                sp = props.GetProp<StructProperty>("DrawScale3D");
            }

            if (sp != null)
            {
                //Debug.WriteLine("Randomizing morph face " + Path.GetFileName(export.FileRef.FileName) + " " + export.UIndex + " " + export.GetFullPath + " vPos");
                FloatProperty x = sp.GetProp<FloatProperty>("X");
                FloatProperty y = sp.GetProp<FloatProperty>("Y");
                FloatProperty z = sp.GetProp<FloatProperty>("Z");
                x.Value = x.Value * random.NextFloat(1 - amount, 1 + amount);
                y.Value = y.Value * random.NextFloat(1 - amount, 1 + amount);
                z.Value = z.Value * random.NextFloat(1 - amount, 1 + amount);
            }

            export.WriteProperties(props);
            export.GetProperties(true);
            //ArrayProperty<StructProperty> m_aMorphFeatures = props.GetProp<ArrayProperty<StructProperty>>("m_aMorphFeatures");
            //if (m_aMorphFeatures != null)
            //{
            //    foreach (StructProperty morphFeature in m_aMorphFeatures)
            //    {
            //        FloatProperty offset = morphFeature.GetProp<FloatProperty>("Offset");
            //        if (offset != null)
            //        {
            //            //Debug.WriteLine("Randomizing morph face " + Path.GetFileName(export.FileRef.FileName) + " " + export.UIndex + " " + export.GetFullPath + " offset");
            //            offset.Value = offset.Value * random.NextFloat(1 - (amount / 3), 1 + (amount / 3));
            //        }
            //    }
            //}
        }

        /// <summary>
        /// Randomizes bio morph faces in a specified file. Will check if file exists first
        /// </summary>
        /// <param name="file"></param>
        /// <param name="random"></param>
        private void RandomizeBioMorphFaceWrapper(string file, Random random)
        {
            if (File.Exists(file))
            {
                ME1Package package = new ME1Package(file);
                {
                    foreach (IExportEntry export in package.Exports)
                    {
                        if (export.ClassName == "BioMorphFace")
                        {
                            RandomizeBioMorphFace(export, random);
                        }
                    }
                }
                ModifiedFiles[package.FileName] = package.FileName;
                package.save();
            }
        }

        private void RandomizeMovementSpeeds(IExportEntry export, Random random)
        {
            mainWindow.CurrentOperationText = "Randomizing Movement Speeds";

            Bio2DA cluster2da = new Bio2DA(export);
            int[] colsToRandomize = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 15, 16, 17, 18, 19 };
            for (int row = 0; row < cluster2da.RowNames.Count(); row++)
            {
                for (int i = 0; i < colsToRandomize.Count(); i++)
                {
                    //Console.WriteLine("[" + row + "][" + colsToRandomize[i] + "] value is " + BitConverter.ToSingle(cluster2da[row, colsToRandomize[i]].Data, 0));
                    int randvalue = random.Next(10, 1200);
                    Console.WriteLine("Movement Speed Randomizer [" + row + "][" + colsToRandomize[i] + "] value is now " + randvalue);
                    cluster2da[row, colsToRandomize[i]].Data = BitConverter.GetBytes(randvalue);
                    cluster2da[row, colsToRandomize[i]].Type = Bio2DACell.TYPE_INT;
                }
            }
            cluster2da.Write2DAToExport();
        }

        //private void RandomizeGalaxyMap(Random random)
        //{
        //    ME1Package engine = new ME1Package(Utilities.GetEngineFile());

        //    foreach (IExportEntry export in engine.Exports)
        //    {
        //        switch (export.ObjectName)
        //        {
        //            case "GalaxyMap_Cluster":
        //                //RandomizeClusters(export, random);
        //                break;
        //            case "GalaxyMap_System":
        //                //RandomizeSystems(export, random);
        //                break;
        //            case "GalaxyMap_Planet":
        //                //RandomizePlanets(export, random);
        //                break;
        //            case "Characters_StartingEquipment":
        //                //RandomizeStartingWeapons(export, random);
        //                break;
        //            case "Classes_ClassTalents":
        //                int shuffleattempts = 0;
        //                bool reattemptTalentShuffle = false;
        //                while (reattemptTalentShuffle)
        //                {
        //                    if (shuffleattempts > 0)
        //                    {
        //                        mainWindow.CurrentOperationText = "Randomizing Class Talents... Attempt #" + (shuffleattempts + 1)));
        //                    }
        //                    reattemptTalentShuffle = !RandomizeTalentLists(export, random); //true if shuffle is OK, false if it failed
        //                    shuffleattempts++;
        //                }
        //                break;
        //            case "LevelUp_ChallengeScalingVars":
        //                //RandomizeLevelUpChallenge(export, random);
        //                break;
        //            case "Items_ItemEffectLevels":
        //                RandomizeWeaponStats(export, random);
        //                break;
        //            case "Characters_Character":
        //                RandomizeCharacter(export, random);
        //                break;
        //        }
        //    }
        //    mainWindow.CurrentOperationText = "Finishing Galaxy Map Randomizing"));

        //    engine.save();
        //}

        private void RandomizeCharacter(IExportEntry export, Random random)
        {
            bool hasChanges = false;
            int[] humanLightArmorManufacturers = { 373, 374, 375, 379, 383, 451 };
            int[] bioampManufacturers = { 341, 342, 343, 345, 410, 496, 497, 498, 526 };
            int[] omnitoolManufacturers = { 362, 363, 364, 366, 411, 499, 500, 501, 527 };
            List<string> actorTypes = new List<string>();
            actorTypes.Add("BIOG_HumanFemale_Hench_C.hench_humanFemale");
            actorTypes.Add("BIOG_HumanMale_Hench_C.hench_humanmale");
            actorTypes.Add("BIOG_Asari_Hench_C.hench_asari");
            actorTypes.Add("BIOG_Krogan_Hench_C.hench_krogan");
            actorTypes.Add("BIOG_Turian_Hench_C.hench_turian");
            actorTypes.Add("BIOG_Quarian_Hench_C.hench_quarian");
            //actorTypes.Add("BIOG_Jenkins_Hench_C.hench_jenkins");

            Bio2DA character2da = new Bio2DA(export);
            for (int row = 0; row < character2da.RowNames.Count(); row++)
            {
                //Console.WriteLine("[" + row + "][" + colsToRandomize[i] + "] value is " + BitConverter.ToSingle(cluster2da[row, colsToRandomize[i]].Data, 0));


                if (mainWindow.RANDSETTING_CHARACTER_HENCH_ARCHETYPES)
                {
                    if (character2da[row, 0].GetDisplayableValue().StartsWith("hench") && !character2da[row, 0].GetDisplayableValue().Contains("jenkins"))
                    {
                        //Henchman
                        int indexToChoose = random.Next(actorTypes.Count);
                        var actorNameVal = actorTypes[indexToChoose];
                        actorTypes.RemoveAt(indexToChoose);
                        Console.WriteLine("Character Randomizer HENCH ARCHETYPE [" + row + "][2] value is now " + actorNameVal);
                        character2da[row, 2].Data = BitConverter.GetBytes((ulong)export.FileRef.findName(actorNameVal));
                        hasChanges = true;
                    }
                }

                if (mainWindow.RANDSETTING_CHARACTER_INVENTORY)
                {
                    int randvalue = random.Next(humanLightArmorManufacturers.Length);
                    int manf = humanLightArmorManufacturers[randvalue];
                    Console.WriteLine("Character Randomizer ARMOR [" + row + "][21] value is now " + manf);
                    character2da[row, 21].Data = BitConverter.GetBytes(manf);

                    if (character2da[row, 24] != null)
                    {
                        randvalue = random.Next(bioampManufacturers.Length);
                        manf = bioampManufacturers[randvalue];
                        Console.WriteLine("Character Randomizer BIOAMP [" + row + "][24] value is now " + manf);
                        character2da[row, 24].Data = BitConverter.GetBytes(manf);
                        hasChanges = true;
                    }

                    if (character2da[row, 29] != null)
                    {
                        randvalue = random.Next(omnitoolManufacturers.Length);
                        manf = omnitoolManufacturers[randvalue];
                        Console.WriteLine("Character Randomizer OMNITOOL [" + row + "][29] value is now " + manf);
                        character2da[row, 29].Data = BitConverter.GetBytes(manf);
                        hasChanges = true;
                    }
                }
            }
            if (hasChanges)
            {
                Debug.WriteLine("Writing Character_Character to export");
                character2da.Write2DAToExport();
            }
        }

        /// <summary>
        /// Randomizes the highest-level galaxy map view. Values are between 0 and 1 for columns 1 and 2 (X,Y).
        /// </summary>
        /// <param name="export">2DA Export</param>
        /// <param name="random">Random number generator</param>
        private void RandomizeClusters(IExportEntry export, Random random, List<TalkFile> Tlks)
        {
            mainWindow.CurrentOperationText = "Randomizing Galaxy Map - Clusters";

            Bio2DA cluster2da = new Bio2DA(export);
            int nameColIndex = cluster2da.GetColumnIndexByName("Name");
            int xColIndex = cluster2da.GetColumnIndexByName("X");
            int yColIndex = cluster2da.GetColumnIndexByName("Y");

            List<string> shuffledClusterNames = new List<string>(RandomClusterNameCollection);
            shuffledClusterNames.Shuffle(random);

            for (int row = 0; row < cluster2da.RowNames.Count(); row++)
            {
                //Randomize X,Y
                float randvalue = random.NextFloat(0, 1);
                cluster2da[row, xColIndex].Data = BitConverter.GetBytes(randvalue);
                randvalue = random.NextFloat(0, 1);
                cluster2da[row, yColIndex].Data = BitConverter.GetBytes(randvalue);

                //Randomize Name
                string name = shuffledClusterNames[0];
                shuffledClusterNames.RemoveAt(0);

                int tlkRef = cluster2da[row, nameColIndex].GetIntValue();
                foreach (TalkFile tf in Tlks)
                {
                    tf.replaceString(tlkRef, name);
                }
            }
            cluster2da.Write2DAToExport();
        }


        /// <summary>
        /// Randomizes the mid-level galaxy map view. 
        /// </summary>
        /// <param name="export">2DA Export</param>
        /// <param name="random">Random number generator</param>
        private void RandomizeSystems(IExportEntry export, Random random)
        {
            mainWindow.CurrentOperationText = "Randomizing Galaxy Map - Systems";

            Console.WriteLine("Randomizing Galaxy Map - Systems");
            Bio2DA system2da = new Bio2DA(export);
            int[] colsToRandomize = { 2, 3 };//X,Y
            for (int row = 0; row < system2da.RowNames.Count(); row++)
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
        /// <param name="random">Random number generator</param>
        private void RandomizePlanets(IExportEntry export, Random random)
        {
            mainWindow.CurrentOperationText = "Randomizing Galaxy Map - Planets";

            Console.WriteLine("Randomizing Galaxy Map - Planets");
            Bio2DA planet2da = new Bio2DA(export);
            int[] colsToRandomize = { 2, 3 };//X,Y
            for (int row = 0; row < planet2da.RowNames.Count(); row++)
            {
                for (int i = 0; i < planet2da.ColumnNames.Count(); i++)
                {
                    if (planet2da[row, i] != null && planet2da[row, i].Type == Bio2DACell.TYPE_FLOAT)
                    {
                        Console.WriteLine("[" + row + "][" + i + "]  (" + planet2da.ColumnNames[i] + ") value is " + BitConverter.ToSingle(planet2da[row, i].Data, 0));
                        float randvalue = random.NextFloat(0, 1);
                        if (i == 11)
                        {
                            randvalue = random.NextFloat(2.5, 8.0);
                        }
                        Console.WriteLine("Planets Randomizer [" + row + "][" + i + "] (" + planet2da.ColumnNames[i] + ") value is now " + randvalue);
                        planet2da[row, i].Data = BitConverter.GetBytes(randvalue);
                    }
                }
            }
            planet2da.Write2DAToExport();
        }

        /// <summary>
        /// Randomizes the planet-level galaxy map view. 
        /// </summary>
        /// <param name="export">2DA Export</param>
        /// <param name="random">Random number generator</param>
        private void RandomizeWeaponStats(IExportEntry export, Random random)
        {
            mainWindow.CurrentOperationText = "Randomizing Items - Weapon Stats";


            Console.WriteLine("Randomizing Items - Item Effect Levels");
            Bio2DA itemeffectlevels2da = new Bio2DA(export);
            for (int row = 0; row < itemeffectlevels2da.RowNames.Count(); row++)
            {
                Bio2DACell propertyCell = itemeffectlevels2da[row, 2];
                if (propertyCell != null)
                {
                    int gameEffect = propertyCell.GetIntValue();
                    switch (gameEffect)
                    {
                        case 15:
                            //GE_Weap_Damage
                            ItemEffectLevels.Randomize_GE_Weap_Damage(itemeffectlevels2da, row, random);
                            break;
                        case 17:
                            //GE_Weap_RPS
                            ItemEffectLevels.Randomize_GE_Weap_RPS(itemeffectlevels2da, row, random);
                            break;
                        case 447:
                            //GE_Weap_Projectiles
                            ItemEffectLevels.Randomize_GE_Weap_PhysicsForce(itemeffectlevels2da, row, random);
                            break;
                        case 1199:
                            //GE_Weap_HeatPerShot
                            ItemEffectLevels.Randomize_GE_Weap_HeatPerShot(itemeffectlevels2da, row, random);
                            break;
                        case 1201:
                            //GE_Weap_HeatLossRate
                            ItemEffectLevels.Randomize_GE_Weap_HeatLossRate(itemeffectlevels2da, row, random);
                            break;
                        case 1259:
                            //GE_Weap_HeatLossRateOH
                            ItemEffectLevels.Randomize_GE_Weap_HeatLossRateOH(itemeffectlevels2da, row, random);
                            break;
                    }
                }
            }
            itemeffectlevels2da.Write2DAToExport();
        }



        /// <summary>
        /// Randomizes the 4 guns you get at the start of the game.
        /// </summary>
        /// <param name="export">2DA Export</param>
        /// <param name="random">Random number generator</param>
        private void RandomizeStartingWeapons(IExportEntry export, Random random)
        {
            /* These are the valid values, invalid ones are removed. They might include some ones not normally accessible but are fully functional
            324	Manf_Armax_Weap
            325	Manf_Devlon_Weap
            326	Manf_Elkoss_Weap
            327	Manf_HK_Weap
            412	Manf_Elanus_Weap
            436	Manf_Geth_Weap
            502	Manf_Spectre01_Weap
            503	Manf_Spectre02_Weap
            504	Manf_Spectre03_Weap
            525	Manf_Haliat_Weap
            582	Manf_Ariake_Weap
            583	Manf_Rosen_Weap
            584	Manf_Kassa_Weap
            598	Manf_Batarian_Weap
            599	Manf_Cerberus_Weap
            600	Manf_Jorman_Weap
            601	Manf_HKShadow_Weap*/

            mainWindow.CurrentOperationText = "Randomizing Starting Weapons";
            bool randomizeLevels = true; //will use better later
            Console.WriteLine("Randomizing Starting Weapons");
            Bio2DA startingitems2da = new Bio2DA(export);
            int[] rowsToRandomize = { 0, 1, 2, 3 };
            int[] manufacturers = { 324, 325, 326, 327, 412, 436, 502, 503, 504, 525, 582, 583, 584, 598, 599, 600, 601 };
            foreach (int row in rowsToRandomize)
            {
                //Columns:
                //0: Item Class - you must have 1 of each or game will crash when swapping to that slot and cutscenes will be super bugged
                //1: Item Sophistication (Level?)
                //2: Manufacturer
                if (randomizeLevels)
                {
                    startingitems2da[row, 2].Data = BitConverter.GetBytes(random.Next(1, 10));
                }
                startingitems2da[row, 2].Data = BitConverter.GetBytes(manufacturers[random.Next(manufacturers.Length)]);
            }
            startingitems2da.Write2DAToExport();
        }

        /// <summary>
        /// Randomizes the talent list
        /// </summary>
        /// <param name="export">2DA Export</param>
        /// <param name="random">Random number generator</param>
        private bool ShuffleClassTalentsAndPowers(IExportEntry export, Random random)
        {
            //List of talents... i think. Taken from talent_talenteffectlevels
            //int[] talentsarray = { 0, 7, 14, 15, 21, 28, 29, 30, 35, 42, 49, 50, 56, 57, 63, 64, 84, 86, 91, 93, 98, 99, 108, 109, 119, 122, 126, 128, 131, 132, 134, 137, 138, 141, 142, 145, 146, 149, 150, 153, 154, 157, 158, 163, 164, 165, 166, 167, 168, 169, 170, 171, 174, 175, 176, 177, 178, 180, 182, 184, 186, 188, 189, 190, 192, 193, 194, 195, 196, 198, 199, 200, 201, 202, 203, 204, 205, 206, 207, 208, 209, 210, 211, 212, 213, 215, 216, 217, 218, 219, 220, 221, 222, 223, 224, 225, 226, 227, 228, 229, 231, 232, 233, 234, 235, 236, 237, 238, 239, 240, 243, 244, 245, 246, 247, 248, 249, 250, 251, 252, 253, 254, 255, 256, 257, 258, 259, 260, 261, 262, 263, 264, 265, 266, 267, 268, 269, 270, 271, 272, 273, 274, 275, 276, 277, 278, 279, 280, 281, 282, 284, 285, 286, 287, 288, 289, 290, 291, 292, 293, 294, 295, 296, 297, 298, 299, 300, 301, 302, 303, 305, 306, 307, 310, 312, 313, 315, 317, 318, 320, 321, 322, 323, 324, 325, 326, 327, 328, 329, 330, 331, 332 };
            List<int> talentidstoassign = new List<int>();
            Bio2DA classtalents = new Bio2DA(export);
            mainWindow.CurrentOperationText = "Randomizing Class talents";

            //108 = Charm
            //109 = Intimidate
            //229 = Setup_Player -> Spectre Training
            //228 = Setup_Player_Squad
            int[] powersToNotReassign = { 108, 109 };
            var powersToReassignPlayerMaster = new List<int>();
            var powersToReassignSquadMaster = new List<int>();

            int isVisibleCol = classtalents.GetColumnIndexByName("IsVisible");

            //Get powers list
            for (int row = 0; row < classtalents.RowNames.Count(); row++)
            {
                var classId = classtalents[row, 0].GetIntValue();
                int talentId = classtalents[row, 1].GetIntValue();
                if (powersToNotReassign.Contains(talentId)) { continue; }
                var visibleInt = classtalents[row, isVisibleCol].GetIntValue();
                if (visibleInt != 0)
                {
                    if (classId == 10)
                    {
                        continue; //QA Cheat Class
                    }
                    if (classId < 6)
                    {
                        //Player class
                        powersToReassignPlayerMaster.Add(talentId);
                    }
                    else
                    {
                        //squadmate class
                        powersToReassignSquadMaster.Add(talentId);
                    }
                }
            }

            //if (mainWindow.RANDSETTING_TALENTS_SHUFFLE_ALLOWSQUADMATEUNITY)
            //{
            //    //Add 2 possible chances to get unity
            //    powersToReassignSquadMaster.Add(259);
            //    powersToReassignSquadMaster.Add(259);
            //}

            //REASSIGN POWERS
            int reassignmentAttemptsRemaining = 200;
            bool attemptingReassignment = true;
            while (attemptingReassignment)
            {
                reassignmentAttemptsRemaining--;
                if (reassignmentAttemptsRemaining < 0) { attemptingReassignment = false; }

                var playerReassignmentList = new List<int>();
                playerReassignmentList.AddRange(powersToReassignPlayerMaster);
                var squadReassignmentList = new List<int>();
                squadReassignmentList.AddRange(powersToReassignSquadMaster);

                playerReassignmentList.Shuffle(random);
                squadReassignmentList.Shuffle(random);

                int previousClassId = -1;
                for (int row = 0; row < classtalents.RowNames.Count(); row++)
                {
                    var classId = classtalents[row, 0].GetIntValue();
                    int existingTalentId = classtalents[row, 1].GetIntValue();
                    if (powersToNotReassign.Contains(existingTalentId)) { continue; }
                    var visibleInt = classtalents[row, isVisibleCol].GetIntValue();
                    if (visibleInt != 0)
                    {
                        if (classId == 10)
                        {
                            continue; //QA Cheat Class
                        }
                        if (classId < 6)
                        {
                            //Player class
                            int talentId = playerReassignmentList[0];
                            playerReassignmentList.RemoveAt(0);
                            classtalents[row, 1].SetData(talentId);
                        }
                        else
                        {
                            //if (previousClassId != classId)
                            //{
                            //    Debug.WriteLine("reassigning to specture training " + row);
                            //    previousClassId = classId;
                            //    classtalents[row, 1].SetData(259);
                            //}
                            //else
                            //{
                            //squadmate class
                            int talentId = squadReassignmentList[0];
                            squadReassignmentList.RemoveAt(0);
                            classtalents[row, 1].SetData(talentId);
                            // }
                        }
                    }
                }

                //Validate

                break;
            }

            if (reassignmentAttemptsRemaining < 0)
            {
                Debugger.Break();
                return false;
            }

            //Patch out Destroyer Tutorial as it may cause a softlock as it checks for kaidan throw
            ME1Package Pro10_08_Dsg = new ME1Package(Path.Combine(Utilities.GetGamePath(), "BioGame", "CookedPC", "Maps", "PRO", "DSG", "BIOA_PRO10_08_DSG.SFM"));
            IExportEntry GDInvulnerabilityCounter = (IExportEntry)Pro10_08_Dsg.getEntry(13521);
            var invulnCount = GDInvulnerabilityCounter.GetProperty<IntProperty>("IntValue");
            if (invulnCount != null && invulnCount.Value != 0)
            {
                invulnCount.Value = 0;
                GDInvulnerabilityCounter.WriteProperty(invulnCount);
                Pro10_08_Dsg.save();
            }


            //REASSIGN UNLOCK REQUIREMENTS
            Debug.WriteLine("Reassigned talents");
            classtalents.Write2DAToExport();

            return true;



















            //OLD CODE
            for (int row = 0; row < classtalents.RowNames.Count(); row++)
            {
                int baseclassid = classtalents[row, 0].GetIntValue();
                if (baseclassid == 10)
                {
                    continue;
                }
                int isvisible = classtalents[row, 6].GetIntValue();
                if (isvisible == 0)
                {
                    continue;
                }
                talentidstoassign.Add(classtalents[row, 1].GetIntValue());
            }

            int i = 0;
            int spectretrainingid = 259;
            //while (i < 60)
            //{
            //    talentidstoassign.Add(spectretrainingid); //spectre training
            //    i++;
            //}

            //bool randomizeLevels = false; //will use better later
            Console.WriteLine("Randomizing Class talent list");

            int currentClassNum = -1;
            List<int> powersAssignedToThisClass = new List<int>();
            List<int> rowsNeedingPrereqReassignments = new List<int>(); //some powers require a prereq, this will ensure all powers are unlockable for this randomization
            List<int> talentidsNeedingReassignment = new List<int>(); //used only to filter out the list of bad choices, e.g. don't depend on self.
            List<int> powersAssignedAsPrereq = new List<int>(); //only assign 1 prereq to a power tree
            for (int row = 0; row < classtalents.RowNames.Count(); row++)
            {
                int baseclassid = classtalents[row, 0].GetIntValue();
                if (baseclassid == 10)
                {
                    continue;
                }
                if (currentClassNum != baseclassid)
                //this block only executes when we are changing classes in the list, so at this point
                //we have all of the info loaded about the class (e.g. all powers that have been assigned)
                {
                    if (powersAssignedToThisClass.Count() > 0)
                    {
                        List<int> possibleAllowedPrereqs = powersAssignedToThisClass.Except(talentidsNeedingReassignment).ToList();

                        //reassign prereqs now that we have a list of powers
                        foreach (int prereqrow in rowsNeedingPrereqReassignments)
                        {
                            int randomindex = -1;
                            int prereq = -1;
                            //while (true)
                            //{
                            randomindex = random.Next(possibleAllowedPrereqs.Count());
                            prereq = possibleAllowedPrereqs[randomindex];
                            //powersAssignedAsPrereq.Add(prereq);
                            classtalents[prereqrow, 8].Data = BitConverter.GetBytes(prereq);
                            classtalents[prereqrow, 9].Data = BitConverter.GetBytes(random.Next(5) + 4);
                            Console.WriteLine("Class " + baseclassid + "'s power on row " + row + " now depends on " + classtalents[prereqrow, 8].GetIntValue() + " at level " + classtalents[prereqrow, 9].GetIntValue());
                            //}
                        }
                    }
                    rowsNeedingPrereqReassignments.Clear();
                    powersAssignedToThisClass.Clear();
                    powersAssignedAsPrereq.Clear();
                    currentClassNum = baseclassid;

                }
                int isvisible = classtalents[row, 6].GetIntValue();
                if (isvisible == 0)
                {
                    continue;
                }

                if (classtalents[row, 8] != null)
                {
                    //prereq
                    rowsNeedingPrereqReassignments.Add(row);
                }

                if (classtalents[row, 1] != null) //talentid
                {
                    //Console.WriteLine("[" + row + "][" + 1 + "]  (" + classtalents.columnNames[1] + ") value originally is " + classtalents[row, 1].GetDisplayableValue());

                    int randomindex = -1;
                    int talentindex = -1;
                    int reassignattemptsremaining = 250; //attempt 250 random attempts.
                    while (true)
                    {
                        reassignattemptsremaining--;
                        if (reassignattemptsremaining <= 0)
                        {
                            //this isn't going to work.
                            return false;
                        }
                        randomindex = random.Next(talentidstoassign.Count());
                        talentindex = talentidstoassign[randomindex];
                        if (baseclassid <= 5 && talentindex == spectretrainingid)
                        {
                            continue;
                        }
                        if (!powersAssignedToThisClass.Contains(talentindex))
                        {
                            break;
                        }
                    }

                    talentidstoassign.RemoveAt(randomindex);
                    classtalents[row, 1].Data = BitConverter.GetBytes(talentindex);
                    powersAssignedToThisClass.Add(talentindex);
                    //Console.WriteLine("[" + row + "][" + 1 + "]  (" + classtalents.columnNames[1] + ") value is now " + classtalents[row, 1].GetDisplayableValue());
                }
                //if (randomizeLevels)
                //{
                //classtalents[row, 1].Data = BitConverter.GetBytes(random.Next(1, 12));
                //}
            }
            classtalents.Write2DAToExport();
            return true;
        }

        private static string[] TalentEffectsToRandomize_THROW = { "GE_TKThrow_CastingTime", "GE_TKThrow_Kickback", "GE_TKThrow_CooldownTime", "GE_TKThrow_ImpactRadius", "GE_TKThrow_Force" };
        private static string[] TalentEffectsToRandomize_LIFT = { "GE_TKLift_Force", "GE_TKLift_EffectDuration", "GE_TKLift_ImpactRadius", "GE_TKLift_CooldownTime" };
        private void RandomizeTalentEffectLevels(IExportEntry export, List<TalkFile> Tlks, Random random)
        {
            mainWindow.CurrentOperationText = "Randomizing Talent and Power stats";
            Bio2DA talentEffectLevels = new Bio2DA(export);
            const int gameEffectLabelCol = 18;

            for (int i = 0; i < talentEffectLevels.RowNames.Count; i++)
            {
                //for each row
                int talentId = talentEffectLevels[i, 0].GetIntValue();
                string rowEffect = talentEffectLevels[i, gameEffectLabelCol].GetDisplayableValue();

                if (talentId == 49 && TalentEffectsToRandomize_THROW.Contains(rowEffect))
                {
                    //THROW = 49 
                    List<int> boostedLevels = new List<int>();
                    boostedLevels.Add(7);
                    boostedLevels.Add(12);
                    switch (rowEffect)
                    {
                        case "GE_TKThrow_Force":
                            Debug.WriteLine("Randomizing GK_TKThrow_Force");
                            TalentEffectLevels.RandomizeRow_FudgeEndpointsEvenDistribution(talentEffectLevels, i, 4, 12, .45, boostedLevels, random, maxValue: 2500f);
                            continue;
                        case "GE_TKThrow_CastingTime":
                            Debug.WriteLine("GE_TKThrow_CastingTime");
                            TalentEffectLevels.RandomizeRow_FudgeEndpointsEvenDistribution(talentEffectLevels, i, 4, 12, .15, boostedLevels, random, directionsAllowed: RandomizationDirection.DownOnly, minValue: .3f);
                            continue;
                        case "GE_TKThrow_Kickback":
                            Debug.WriteLine("GE_TKThrow_Kickback");
                            TalentEffectLevels.RandomizeRow_FudgeEndpointsEvenDistribution(talentEffectLevels, i, 4, 12, .1, boostedLevels, random, minValue: 0.05f);
                            continue;
                        case "GE_TKThrow_CooldownTime":
                            Debug.WriteLine("GE_TKThrow_CooldownTime");
                            TalentEffectLevels.RandomizeRow_FudgeEndpointsEvenDistribution(talentEffectLevels, i, 4, 12, .22, boostedLevels, random, minValue: 5);
                            continue;
                        case "GE_TKThrow_ImpactRadius":
                            Debug.WriteLine("GE_TKThrow_ImpactRadius");
                            TalentEffectLevels.RandomizeRow_FudgeEndpointsEvenDistribution(talentEffectLevels, i, 4, 12, .4, boostedLevels, random, minValue: 100, maxValue: 1200f);
                            continue;
                    }
                }
                else if (talentId == 50 && TalentEffectsToRandomize_LIFT.Contains(rowEffect))
                {
                    List<int> boostedLevels = new List<int>();
                    boostedLevels.Add(7);
                    boostedLevels.Add(12);
                    switch (rowEffect)
                    {
                        //LIFT = 50
                        case "GE_TKLift_Force":
                            Debug.WriteLine("GE_TKLift_Force");
                            TalentEffectLevels.RandomizeRow_FudgeEndpointsEvenDistribution(talentEffectLevels, i, 4, 12, .25, boostedLevels, random, directionsAllowed: RandomizationDirection.UpOnly, minValue: .3f, maxValue: 3500f);
                            continue;
                        case "GE_TKLift_EffectDuration":
                            Debug.WriteLine("GE_TKLift_EffectDuration");
                            TalentEffectLevels.RandomizeRow_FudgeEndpointsEvenDistribution(talentEffectLevels, i, 4, 12, .1, boostedLevels, random, minValue: 1f, directionsAllowed: RandomizationDirection.DownOnly);
                            continue;
                        case "GE_TKLift_ImpactRadius":
                            Debug.WriteLine("GE_TKLift_ImpactRadius");
                            TalentEffectLevels.RandomizeRow_FudgeEndpointsEvenDistribution(talentEffectLevels, i, 4, 12, .22, boostedLevels, random, minValue: 5, maxValue: 4500);
                            continue;
                        case "GE_TKLift_CooldownTime":
                            Debug.WriteLine("GE_TKLift_CooldownTime");
                            TalentEffectLevels.RandomizeRow_FudgeEndpointsEvenDistribution(talentEffectLevels, i, 4, 12, .4, boostedLevels, random, minValue: 15f, maxValue: 60f);
                            continue;
                    }
                }
            }
            talentEffectLevels.Write2DAToExport();
            UpdateTalentStrings(export, Tlks);
        }

        private void UpdateTalentStrings(IExportEntry talentEffectLevelsExport, List<TalkFile> talkFiles)
        {
            IExportEntry talentGUIExport = talentEffectLevelsExport.FileRef.Exports.First(x => x.ObjectName == "Talent_TalentGUI");
            Bio2DA talentGUI2DA = new Bio2DA(talentGUIExport);
            Bio2DA talentEffectLevels2DA = new Bio2DA(talentEffectLevelsExport);
            const int columnPatternStart = 4;
            const int numColumnsPerLevelGui = 4;
            int statTableLevelStartColumn = 4; //Level 1 in TalentEffectLevels
            for (int i = 0; i < talentGUI2DA.RowNames.Count; i++)
            {
                if (int.TryParse(talentGUI2DA.RowNames[i], out int talentID))
                {
                    for (int level = 0; level < 12; level++)
                    {
                        switch (talentID)
                        {
                            case 49: //Throw
                                {
                                    var guitlkcolumn = columnPatternStart + 2 + (level * numColumnsPerLevelGui);
                                    int stringId = talentGUI2DA[i, guitlkcolumn].GetIntValue();

                                    string basicFormat = "%HEADER%\n\nThrows enemies away from the caster with a force of %TOKEN1% Newtons\n\nRadius: %TOKEN2% m\nTime To Cast: %TOKEN3% sec\nRecharge Time: %TOKEN4% sec\nAccuracy Cost: %TOKEN5%%";
                                    int token1row = 175; //Force
                                    int token2row = 173; //impact radius
                                    int token3row = 170; //Casting time
                                    int token4row = 172; //Cooldown
                                    int token5row = 171; //Accuracy cost

                                    string force = talentEffectLevels2DA[token1row, level + statTableLevelStartColumn].GetTlkDisplayableValue();
                                    string radius = talentEffectLevels2DA[token2row, level + statTableLevelStartColumn].GetTlkDisplayableValue(isMeters: true);
                                    string time = talentEffectLevels2DA[token3row, level + statTableLevelStartColumn].GetTlkDisplayableValue();
                                    string cooldown = talentEffectLevels2DA[token4row, level + statTableLevelStartColumn].GetTlkDisplayableValue();
                                    string cost = talentEffectLevels2DA[token5row, level + statTableLevelStartColumn].GetTlkDisplayableValue(isPercent: true);

                                    string header = "Throw";
                                    if (level > 6) { header = "Advanced Throw"; }
                                    if (level >= 11) { header = "Master Throw"; }

                                    string formatted = FormatString(basicFormat, header, force, radius, time, cooldown, cost);
                                    talkFiles.ForEach(x => x.replaceString(stringId, formatted));
                                }
                                break;
                            case 50: //Lift
                                {
                                    var guitlkcolumn = columnPatternStart + 2 + (level * numColumnsPerLevelGui);
                                    int stringId = talentGUI2DA[i, guitlkcolumn].GetIntValue();

                                    string basicFormat = "%HEADER%\n\nLifts everything within %TOKEN1% m of the target into the air, rendering enemies immobile and unable to attack. Drops them when it expires.\n\nDuration: %TOKEN2% sec\nRecharge Time: %TOKEN3% sec\nAccuracy Cost: %TOKEN4%%\nLift Force: %TOKEN5% Newtons";
                                    int token1row = 175; //impact radius
                                    int token2row = 189; //duration
                                    int token3row = 186; //recharge
                                    int token4row = 185; //accuacy cost
                                    int token5row = 190; //lift force

                                    string radius = talentEffectLevels2DA[token1row, level + statTableLevelStartColumn].GetTlkDisplayableValue();
                                    string duration = talentEffectLevels2DA[token2row, level + statTableLevelStartColumn].GetTlkDisplayableValue();
                                    string cooldown = talentEffectLevels2DA[token3row, level + statTableLevelStartColumn].GetTlkDisplayableValue();
                                    string cost = talentEffectLevels2DA[token4row, level + statTableLevelStartColumn].GetTlkDisplayableValue(isPercent: true);
                                    string force = talentEffectLevels2DA[token5row, level + statTableLevelStartColumn].GetTlkDisplayableValue();


                                    string header = "Lift";
                                    if (level > 6) { header = "Advanced Lift"; }
                                    if (level >= 11) { header = "Master Lift"; }

                                    string formatted = FormatString(basicFormat, header, radius, duration, cooldown, cost, force);
                                    talkFiles.ForEach(x => x.replaceString(stringId, formatted));
                                }
                                break;
                        }
                    }
                }
            }
        }

        private string FormatString(string unformattedStr, string header, params string[] tokens)
        {
            string retStr = unformattedStr;
            retStr = retStr.Replace("%HEADER%", header);
            for (int i = 1; i <= tokens.Length; i++)
            {
                string token = tokens[i - 1];
                retStr = retStr.Replace($"%TOKEN{i}%", token);
            }
            return retStr;
        }

        /// <summary>
        /// Randomizes the challenge scaling variables used by enemies
        /// </summary>
        /// <param name="export">2DA Export</param>
        /// <param name="random">Random number generator</param>
        private void RandomizeLevelUpChallenge(IExportEntry export, Random random)
        {
            mainWindow.CurrentOperationText = "Randomizing Class talents list";
            bool randomizeLevels = false; //will use better later
            Console.WriteLine("Randomizing Class talent list");
            Bio2DA challenge2da = new Bio2DA(export);



            for (int row = 0; row < challenge2da.RowNames.Count(); row++)
            {
                for (int col = 0; col < challenge2da.ColumnNames.Count(); col++)
                    if (challenge2da[row, col] != null)
                    {
                        Console.WriteLine("[" + row + "][" + col + "]  (" + challenge2da.ColumnNames[col] + ") value originally is " + challenge2da[row, 1].GetDisplayableValue());
                        //int randomindex = random.Next(talents.Count());
                        //int talentindex = talents[randomindex];
                        //talents.RemoveAt(randomindex);
                        float multiplier = random.NextFloat(0.7, 1.3);
                        if (col % 2 == 0)
                        {
                            //Fraction
                            Bio2DACell cell = challenge2da[row, col];
                            if (cell.Type == Bio2DACell.TYPE_FLOAT)
                            {
                                challenge2da[row, col].Data = BitConverter.GetBytes(challenge2da[row, col].GetFloatValue() * multiplier);
                            }
                            else
                            {
                                challenge2da[row, col].Data = BitConverter.GetBytes(challenge2da[row, col].GetIntValue() * multiplier);
                                challenge2da[row, col].Type = Bio2DACell.TYPE_FLOAT;
                            }
                        }
                        else
                        {
                            //Level Offset
                            challenge2da[row, col].Data = BitConverter.GetBytes((int)(challenge2da[row, col].GetIntValue() * multiplier));
                        }
                        Console.WriteLine("[" + row + "][" + col + "]  (" + challenge2da.ColumnNames[col] + ") value is now " + challenge2da[row, 1].GetDisplayableValue());
                    }
                if (randomizeLevels)
                {
                    challenge2da[row, 1].Data = BitConverter.GetBytes(random.Next(1, 12));
                }
            }
            challenge2da.Write2DAToExport();
        }

        /// <summary>
        /// Randomizes the character creator
        /// </summary>
        /// <param name="random">Random number generator</param>
        private void RandomizeCharacterCreator(Random random, IExportEntry export)
        {
            mainWindow.CurrentOperationText = "Randomizing Charactor Creator";
            //if (headrandomizerclasses.Contains(export.ObjectName))
            //{
            //    RandomizePregeneratedHead(export, random);
            //    continue;
            //}
            Bio2DA export2da = new Bio2DA(export);
            bool hasChanges = false;
            for (int row = 0; row < export2da.RowNames.Count(); row++)
            {
                float numberedscalar = 0;
                for (int col = 0; col < export2da.ColumnNames.Count(); col++)
                {
                    Bio2DACell cell = export2da[row, col];

                    //Extent
                    if (export2da.ColumnNames[col] == "Extent" || export2da.ColumnNames[col] == "Rand_Extent")
                    {
                        float multiplier = random.NextFloat(0.5, 6);
                        Console.WriteLine("[" + row + "][" + col + "]  (" + export2da.ColumnNames[col] + ") value originally is " + export2da[row, col].GetDisplayableValue());

                        if (cell.Type == Bio2DACell.TYPE_FLOAT)
                        {
                            cell.Data = BitConverter.GetBytes(cell.GetFloatValue() * multiplier);
                            hasChanges = true;
                        }
                        else
                        {
                            cell.Data = BitConverter.GetBytes(cell.GetIntValue() * multiplier);
                            cell.Type = Bio2DACell.TYPE_FLOAT;
                            hasChanges = true;
                        }
                        Console.WriteLine("[" + row + "][" + col + "]  (" + export2da.ColumnNames[col] + ") value now is " + cell.GetDisplayableValue());
                        continue;
                    }

                    //Hair Scalars
                    if (export.ObjectName.Contains("MorphHair") && row > 0 && col >= 4 && col <= 8)
                    {

                        float scalarval = random.NextFloat(0, 1);
                        if (col == 5)
                        {
                            numberedscalar = scalarval;
                        }
                        else if (col > 5)
                        {
                            scalarval = numberedscalar;
                        }
                        // Bio2DACell cellX = cell;
                        Console.WriteLine("[" + row + "][" + col + "]  (" + export2da.ColumnNames[col] + ") value originally is " + cell.GetDisplayableValue());
                        cell.Data = BitConverter.GetBytes(scalarval);
                        cell.Type = Bio2DACell.TYPE_FLOAT;
                        Console.WriteLine("[" + row + "][" + col + "]  (" + export2da.ColumnNames[col] + ") value now is " + cell.GetDisplayableValue());
                        hasChanges = true;
                        continue;
                    }

                    //Skin Tone
                    if (cell != null && cell.Type == Bio2DACell.TYPE_NAME)
                    {
                        if (export.ObjectName.Contains("Skin_Tone") && !mainWindow.RANDSETTING_CHARACTER_CHARCREATOR_SKINTONE)
                        {
                            continue; //skip
                        }
                        string value = cell.GetDisplayableValue();
                        if (value.StartsWith("RGB("))
                        {
                            //Make new item
                            string rgbNewName = GetRandomColorRBGStr(random);
                            int newValue = export.FileRef.FindNameOrAdd(rgbNewName);
                            cell.Data = BitConverter.GetBytes((ulong)newValue); //name is 8 bytes
                            hasChanges = true;
                        }
                    }

                    string columnName = export2da.GetColumnNameByIndex(col);
                    if (columnName.Contains("Scalar") && cell != null && cell.Type != Bio2DACell.TYPE_NAME)
                    {
                        float currentValue = float.Parse(cell.GetDisplayableValue());
                        cell.Data = BitConverter.GetBytes(currentValue * random.NextFloat(0.5, 2));
                        cell.Type = Bio2DACell.TYPE_FLOAT;
                        hasChanges = true;
                    }

                    //if (export.ObjectName.Contains("Skin_Tone") && mainWindow.RANDSETTING_CHARACTER_CHARCREATOR_SKINTONE && row > 0 && col >= 1 && col <= 5)
                    //{
                    //    if (export.ObjectName.Contains("Female"))
                    //    {
                    //        if (col < 5)
                    //        {
                    //            //Females have one less column
                    //            string rgbNewName = GetRandomColorRBGStr(random);
                    //            int newValue = export.FileRef.FindNameOrAdd(rgbNewName);
                    //            export2da[row, col].Data = BitConverter.GetBytes((ulong)newValue); //name is 8 bytes
                    //            hasChanges = true;
                    //        }
                    //    }
                    //    else
                    //    {
                    //        string rgbNewName = GetRandomColorRBGStr(random);
                    //        int newValue = export.FileRef.FindNameOrAdd(rgbNewName);
                    //        export2da[row, col].Data = BitConverter.GetBytes((ulong)newValue); //name is 8 bytes
                    //        hasChanges = true;
                    //    }
                    //}
                }
            }
            if (hasChanges)
            {
                export2da.Write2DAToExport();
            }
        }

        private void RandomizeBioMorphFace(IExportEntry export, Random random, double amount = 0.3)
        {
            var props = export.GetProperties();
            ArrayProperty<StructProperty> m_aMorphFeatures = props.GetProp<ArrayProperty<StructProperty>>("m_aMorphFeatures");
            if (m_aMorphFeatures != null)
            {
                foreach (StructProperty morphFeature in m_aMorphFeatures)
                {
                    FloatProperty offset = morphFeature.GetProp<FloatProperty>("Offset");
                    if (offset != null)
                    {
                        //Debug.WriteLine("Randomizing morph face " + Path.GetFileName(export.FileRef.FileName) + " " + export.UIndex + " " + export.GetFullPath + " offset");
                        offset.Value = offset.Value * random.NextFloat(1 - (amount / 3), 1 + (amount / 3));
                    }
                }
            }

            ArrayProperty<StructProperty> m_aFinalSkeleton = props.GetProp<ArrayProperty<StructProperty>>("m_aFinalSkeleton");
            if (m_aFinalSkeleton != null)
            {
                foreach (StructProperty offsetBonePos in m_aFinalSkeleton)
                {
                    StructProperty vPos = offsetBonePos.GetProp<StructProperty>("vPos");
                    if (vPos != null)
                    {
                        //Debug.WriteLine("Randomizing morph face " + Path.GetFileName(export.FileRef.FileName) + " " + export.UIndex + " " + export.GetFullPath + " vPos");
                        FloatProperty x = vPos.GetProp<FloatProperty>("X");
                        FloatProperty y = vPos.GetProp<FloatProperty>("Y");
                        FloatProperty z = vPos.GetProp<FloatProperty>("Z");
                        x.Value = x.Value * random.NextFloat(1 - amount, 1 + amount);
                        y.Value = y.Value * random.NextFloat(1 - amount, 1 + amount);
                        z.Value = z.Value * random.NextFloat(1 - (amount / .85), 1 + (amount / .85));
                    }
                }
            }
            export.WriteProperties(props);
        }

        private void RandomizePregeneratedHead(IExportEntry export, Random random)
        {
            int[] floatSliderIndexesToRandomize = { 5, 6, 7, 8, 9, 10, 11, 13, 14, 15, 16, 17, 19, 20, 21, 22, 24, 25, 26, 27, 29, 30 };
            Dictionary<int, int> columnMaxDictionary = new Dictionary<int, int>();
            columnMaxDictionary[1] = 7; //basehead
            columnMaxDictionary[2] = 6; //skintone
            columnMaxDictionary[3] = 3; //archetype
            columnMaxDictionary[4] = 14; //scar
            columnMaxDictionary[12] = 8; //eyeshape
            columnMaxDictionary[18] = 13; //iriscolor +1
            columnMaxDictionary[23] = 10; //mouthshape
            columnMaxDictionary[28] = 13; //noseshape
            columnMaxDictionary[31] = 14; //beard
            columnMaxDictionary[32] = 7; //brows +1
            columnMaxDictionary[33] = 9; //hair
            columnMaxDictionary[34] = 8; //haircolor
            columnMaxDictionary[35] = 8; //facialhaircolor

            if (export.ObjectName.Contains("Female"))
            {
                floatSliderIndexesToRandomize = new int[] { 5, 6, 7, 8, 9, 10, 11, 13, 14, 15, 16, 17, 19, 20, 21, 22, 24, 25, 26, 27, 29, 30 };
                columnMaxDictionary.Clear();
                //there are female specific values that must be used
                columnMaxDictionary[1] = 10; //basehead
                columnMaxDictionary[2] = 6; //skintone
                columnMaxDictionary[3] = 3; //archetype
                columnMaxDictionary[4] = 11; //scar
                columnMaxDictionary[12] = 10; //eyeshape
                columnMaxDictionary[18] = 13; //iriscolor +1
                columnMaxDictionary[23] = 10; //mouthshape
                columnMaxDictionary[28] = 12; //noseshape
                columnMaxDictionary[31] = 8; //haircolor
                columnMaxDictionary[32] = 10; //hair
                columnMaxDictionary[33] = 17; //brows
                columnMaxDictionary[34] = 7; //browcolor
                columnMaxDictionary[35] = 7; //blush
                columnMaxDictionary[36] = 8; //lipcolor
                columnMaxDictionary[37] = 8; //eyemakeupcolor

            }
            Bio2DA export2da = new Bio2DA(export);
            for (int row = 0; row < export2da.RowNames.Count(); row++)
            {
                foreach (int col in floatSliderIndexesToRandomize)
                {
                    export2da[row, col].Data = BitConverter.GetBytes(random.NextFloat(0, 2));
                }
            }

            for (int row = 0; row < export2da.RowNames.Count(); row++)
            {
                foreach (KeyValuePair<int, int> entry in columnMaxDictionary)
                {
                    int col = entry.Key;
                    Console.WriteLine("[" + row + "][" + col + "]  (" + export2da.ColumnNames[col] + ") value originally is " + export2da[row, col].GetDisplayableValue());

                    export2da[row, col].Data = BitConverter.GetBytes(random.Next(0, entry.Value) + 1);
                    export2da[row, col].Type = Bio2DACell.TYPE_INT;
                    Console.WriteLine("Character Creator Randomizer [" + row + "][" + col + "] (" + export2da.ColumnNames[col] + ") value is now " + export2da[row, col].GetDisplayableValue());

                }
            }
            Console.WriteLine("Writing export " + export.ObjectName);
            export2da.Write2DAToExport();
        }

        /// <summary>
        /// Randomizes the the music table
        /// </summary>
        /// <param name="export">2DA Export</param>
        /// <param name="random">Random number generator</param>
        private void RandomizeMusic(IExportEntry export, Random random, string randomizingtext = null)
        {
            if (randomizingtext == null)
            {
                randomizingtext = "Randomizing Music";
            }
            mainWindow.CurrentOperationText = randomizingtext;
            Console.WriteLine(randomizingtext);
            Bio2DA music2da = new Bio2DA(export);
            List<byte[]> names = new List<byte[]>();
            int[] colsToRandomize = { 0, 5, 6, 7, 8, 9, 10, 11, 12 };
            for (int row = 0; row < music2da.RowNames.Count(); row++)
            {
                foreach (int col in colsToRandomize)
                {
                    if (music2da[row, col] != null && music2da[row, col].Type == Bio2DACell.TYPE_NAME)
                    {
                        if (!music2da[row, col].GetDisplayableValue().StartsWith("music"))
                        {
                            continue;
                        }
                        names.Add(music2da[row, col].Data.TypedClone());
                    }
                }
            }

            for (int row = 0; row < music2da.RowNames.Count(); row++)
            {
                foreach (int col in colsToRandomize)
                {
                    if (music2da[row, col] != null && music2da[row, col].Type == Bio2DACell.TYPE_NAME)
                    {
                        if (!music2da[row, col].GetDisplayableValue().StartsWith("music"))
                        {
                            continue;
                        }
                        Console.WriteLine("[" + row + "][" + col + "]  (" + music2da.ColumnNames[col] + ") value originally is " + music2da[row, col].GetDisplayableValue());
                        int r = random.Next(names.Count);
                        byte[] pnr = names[r];
                        names.RemoveAt(r);
                        music2da[row, col].Data = pnr;
                        Console.WriteLine("Music Randomizer [" + row + "][" + col + "] (" + music2da.ColumnNames[col] + ") value is now " + music2da[row, col].GetDisplayableValue());

                    }
                }
            }
            music2da.Write2DAToExport();
        }

        /// <summary>
        /// Randomizes the sounds and music in GUIs. This is shared between two tables as it contains the same indexing and table format
        /// </summary>
        /// <param name="export">2DA Export</param>
        /// <param name="random">Random number generator</param>
        private void RandomizeGUISounds(IExportEntry export, Random random, string randomizingtext = null, string requiredprefix = null)
        {
            if (randomizingtext == null)
            {
                randomizingtext = "Randomizing UI - Sounds";
            }
            mainWindow.CurrentOperationText = randomizingtext;
            Console.WriteLine(randomizingtext);
            Bio2DA guisounds2da = new Bio2DA(export);
            int[] colsToRandomize = { 0 };//sound name
            List<byte[]> names = new List<byte[]>();

            if (requiredprefix != "music")
            {

                for (int row = 0; row < guisounds2da.RowNames.Count(); row++)
                {
                    if (guisounds2da[row, 0] != null && guisounds2da[row, 0].Type == Bio2DACell.TYPE_NAME)
                    {
                        if (requiredprefix != null && !guisounds2da[row, 0].GetDisplayableValue().StartsWith(requiredprefix))
                        {
                            continue;
                        }
                        names.Add(guisounds2da[row, 0].Data.TypedClone());
                    }
                }
            }
            else
            {
                for (int n = 0; n < export.FileRef.Names.Count; n++)
                {
                    string name = export.FileRef.Names[n];
                    if (name.StartsWith("music.mus"))
                    {
                        Int64 nameval = n;
                        names.Add(BitConverter.GetBytes(nameval));
                    }
                }
            }

            for (int row = 0; row < guisounds2da.RowNames.Count(); row++)
            {
                if (guisounds2da[row, 0] != null && guisounds2da[row, 0].Type == Bio2DACell.TYPE_NAME)
                {
                    if (requiredprefix != null && !guisounds2da[row, 0].GetDisplayableValue().StartsWith(requiredprefix))
                    {
                        continue;
                    }
                    Thread.Sleep(20);
                    Console.WriteLine("[" + row + "][" + 0 + "]  (" + guisounds2da.ColumnNames[0] + ") value originally is " + guisounds2da[row, 0].GetDisplayableValue());
                    int r = random.Next(names.Count);
                    byte[] pnr = names[r];
                    names.RemoveAt(r);
                    guisounds2da[row, 0].Data = pnr;
                    Console.WriteLine("Sounds - GUI Sounds Randomizer [" + row + "][" + 0 + "] (" + guisounds2da.ColumnNames[0] + ") value is now " + guisounds2da[row, 0].GetDisplayableValue());

                }
            }
            guisounds2da.Write2DAToExport();
        }

        static float NextFloat(Random random)
        {
            double mantissa = (random.NextDouble() * 2.0) - 1.0;
            double exponent = Math.Pow(2.0, random.Next(-3, 20));
            return (float)(mantissa * exponent);
        }

        static string GetRandomColorRBGStr(Random random)
        {
            return $"RGB({random.Next(255)},{random.Next(255)},{random.Next(255)})";
        }
    }
}
