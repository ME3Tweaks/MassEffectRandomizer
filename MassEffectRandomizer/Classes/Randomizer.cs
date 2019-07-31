using AlotAddOnGUI.classes;
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
using Serilog;
using Object = System.Object;
using System.Reflection;
using System.Globalization;
using StringComparison = System.StringComparison;

namespace MassEffectRandomizer.Classes
{
    class Randomizer
    {
        private const string UPDATE_RANDOMIZING_TEXT = "UPDATE_RANDOMIZING_TEXT";
        private MainWindow mainWindow;
        private BackgroundWorker randomizationWorker;
        private ConcurrentDictionary<string, string> ModifiedFiles;
        private SortedSet<string> faceFxBoneNames = new SortedSet<string>();

        public Randomizer(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
            scottishVowelOrdering = null; //will be set when needed.
            mapNamesToFaceFxRandomizationLists = new Dictionary<string, List<string>>();
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

            foreach (var v in faceFxBoneNames)
            {
                Debug.WriteLine(v);
            }
        }

        private void PerformRandomization(object sender, DoWorkEventArgs e)
        {
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
                //TODO: Use BioTlkFileSet or something to only do INT
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

            //RANDOMIZE TEXTS
            if (mainWindow.RANDSETTING_MISC_GAMEOVERTEXT)
            {
                mainWindow.CurrentOperationText = "Randoming Game Over text";
                string fileContents = Utilities.GetEmbeddedStaticFilesTextFile("gameovertexts.xml");
                XElement rootElement = XElement.Parse(fileContents);
                var gameoverTexts = rootElement.Elements("gameovertext").Select(x => x.Value).ToList();
                var gameOverText = gameoverTexts[random.Next(gameoverTexts.Count)];
                foreach (TalkFile tlk in Tlks)
                {
                    tlk.replaceString(157152, gameOverText);
                }
            }

            //Randomize BIOC_BASE
            ME1Package bioc_base = new ME1Package(Path.Combine(Utilities.GetGamePath(), "BioGame", "CookedPC", "BIOC_Base.u"));
            bool bioc_base_changed = false;
            if (mainWindow.RANDSETTING_MOVEMENT_MAKO)
            {
                RandomizeMako(bioc_base, random);
                bioc_base_changed = true;
            }

            if (bioc_base_changed)
            {
                ModifiedFiles[bioc_base.FileName] = bioc_base.FileName;
                bioc_base.save();
            }




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
                            RandomizeClustersXY(export, random, Tlks);
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
                        if (mainWindow.RANDSETTING_WEAPONS_EFFECTLEVELS || mainWindow.RANDSETTING_MOVEMENT_MAKO)
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

            if (engine.ShouldSave)
            {
                engine.save();
                ModifiedFiles[engine.FileName] = engine.FileName;

            }

            //RANDOMIZE ENTRYMENU
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
                            RandomizeCharacterCreator2DA(random, export);
                        }

                        break;

                        //RandomizeGalaxyMap(random);
                        //RandomizeGUISounds(random);
                        //RandomizeMusic(random);
                        //RandomizeMovementSpeeds(random);
                        //RandomizeCharacterCreator2DA(random);
                        //Dump2DAToExcel();
                }

                if (mainWindow.RANDSETTING_CHARACTER_ICONICFACE && export.ClassName == "BioMorphFace" && export.ObjectName.StartsWith("Player_"))
                {
                    RandomizeBioMorphFace(export, random, .2);
                }
            }

            if (mainWindow.RANDSETTING_CHARACTER_CHARCREATOR)
            {
                RandomizeCharacterCreatorSingular(random, Tlks);
            }

            if (mainWindow.RANDSETTING_MISC_SPLASH)
            {
                RandomizeSplash(random, entrymenu);
            }

            if (entrymenu.ShouldSave)
            {
                entrymenu.save();
                ModifiedFiles[entrymenu.FileName] = entrymenu.FileName;
            }


            //RANDOMIZE FACES
            if (mainWindow.RANDSETTING_CHARACTER_HENCHFACE)
            {
                RandomizeBioMorphFaceWrapper(Utilities.GetGameFile(@"BioGame\CookedPC\Packages\GameObjects\Characters\Faces\BIOG_Hench_FAC.upk"), random); //Henchmen
                RandomizeBioMorphFaceWrapper(Utilities.GetGameFile(@"BioGame\CookedPC\Packages\BIOG_MORPH_FACE.upk"), random); //Iconic and player (Not sure if this does anything...
            }

            //Map file randomizer
            if (RunMapRandomizerPass)
            {
                mainWindow.CurrentOperationText = "Getting list of files...";

                mainWindow.ProgressBarIndeterminate = true;
                string path = Path.Combine(Utilities.GetGamePath(), "BioGame", "CookedPC", "Maps");
                string bdtspath = Path.Combine(Utilities.GetGamePath(), "DLC", "DLC_UNC", "CookedPC", "Maps");
                string pspath = Path.Combine(Utilities.GetGamePath(), "DLC", "DLC_Vegas", "CookedPC", "Maps");

                var filesEnum = Directory.GetFiles(path, "*.sfm", SearchOption.AllDirectories);
                string[] files = null;
                if (!mainWindow.RANDSETTING_WACK_FACEFX)
                {
                    files = filesEnum.Where(x => !Path.GetFileName(x).ToLower().Contains("_loc_")).ToArray();
                }
                else
                {
                    files = filesEnum.ToArray();
                }

                if (Directory.Exists(bdtspath))
                {
                    files = files.Concat(Directory.GetFiles(bdtspath, "*.sfm", SearchOption.AllDirectories)).ToArray();
                }

                if (Directory.Exists(pspath))
                {
                    files = files.Concat(Directory.GetFiles(pspath, "*.sfm", SearchOption.AllDirectories)).ToArray();
                }

                mainWindow.ProgressBarIndeterminate = false;
                mainWindow.ProgressBar_Bottom_Max = files.Count();
                mainWindow.ProgressBar_Bottom_Min = 0;
                double morphFaceRandomizationAmount = mainWindow.RANDSETTING_MISC_MAPFACES_AMOUNT;
                double faceFXRandomizationAmount = mainWindow.RANDSETTING_WACK_FACEFX_AMOUNT;
                string[] mapBaseNamesToNotRandomize = { "entrymenu", "biog_uiworld" };
                for (int i = 0; i < files.Length; i++)
                {
                    bool loggedFilename = false;
                    mainWindow.CurrentProgressValue = i;
                    mainWindow.CurrentOperationText = "Randomizing map files [" + i + "/" + files.Count() + "]";
                    var mapBaseName = Path.GetFileNameWithoutExtension(files[i]).ToLower();
                    //Debug.WriteLine(mapBaseName);
                    //if (mapBaseName != "bioa_nor10_03_dsg") continue;
                    if (!mapBaseNamesToNotRandomize.Any(x => x.StartsWith(mapBaseName)))
                    {
                        //if (!mapBaseName.StartsWith("bioa_sta")) continue;
                        bool hasLogged = false;
                        ME1Package package = new ME1Package(files[i]);
                        if (RunMapRandomizerPassAllExports)
                        {
                            foreach (IExportEntry exp in package.Exports)
                            {
                                if (mainWindow.RANDSETTING_MISC_MAPFACES && exp.ClassName == "BioMorphFace")
                                {
                                    //Face randomizer
                                    if (!loggedFilename)
                                    {
                                        Log.Information("Randomizing map file: " + files[i]);
                                        loggedFilename = true;
                                    }

                                    RandomizeBioMorphFace(exp, random, morphFaceRandomizationAmount);
                                    package.ShouldSave = true;
                                }
                                else if (mainWindow.RANDSETTING_MISC_HAZARDS && exp.ClassName == "SequenceReference")
                                {
                                    //Hazard Randomizer
                                    var seqRef = exp.GetProperty<ObjectProperty>("oSequenceReference");
                                    if (seqRef != null && exp.FileRef.isUExport(seqRef.Value))
                                    {
                                        IExportEntry possibleHazSequence = exp.FileRef.getUExport(seqRef.Value);
                                        var objName = possibleHazSequence.GetProperty<StrProperty>("ObjName");
                                        if (objName != null && objName == "REF_HazardSystem")
                                        {
                                            if (!loggedFilename)
                                            {
                                                Log.Information("Randomizing map file: " + files[i]);
                                                loggedFilename = true;
                                            }

                                            RandomizeHazard(exp, random);
                                            package.ShouldSave = true;
                                        }
                                    }
                                }
                                else if (exp.ClassName == "BioPawn")
                                {
                                    if (mainWindow.RANDSETTING_MISC_MAPPAWNSIZES && random.Next(4) == 0)
                                    {
                                        if (!loggedFilename)
                                        {
                                            Log.Information("Randomizing map file: " + files[i]);
                                            loggedFilename = true;
                                        }

                                        //Pawn size randomizer
                                        RandomizeBioPawnSize(exp, random, 0.4);
                                    }

                                    if (mainWindow.RANDSETTING_PAWN_MATERIALCOLORS)
                                    {
                                        if (!loggedFilename)
                                        {
                                            Log.Information("Randomizing map file: " + files[i]);
                                            loggedFilename = true;
                                        }

                                        RandomizePawnMaterialInstances(exp, random);
                                    }
                                }
                                else if (mainWindow.RANDSETTING_MISC_INTERPS && exp.ClassName == "InterpTrackMove" /* && random.Next(4) == 0*/)
                                {
                                    if (!loggedFilename)
                                    {
                                        Log.Information("Randomizing map file: " + files[i]);
                                        loggedFilename = true;
                                    }

                                    //Interpolation randomizer
                                    RandomizeInterpTrackMove(exp, random, morphFaceRandomizationAmount);
                                    package.ShouldSave = true;
                                }
                                else if (mainWindow.RANDSETTING_WACK_FACEFX && exp.ClassName == "FaceFXAnimSet")
                                {
                                    if (!loggedFilename)
                                    {
                                        Log.Information("Randomizing map file: " + files[i]);
                                        loggedFilename = true;
                                    }

                                    //Method contains SHouldSave in it (due to try catch).
                                    RandomizeFaceFX(exp, random, (int)faceFXRandomizationAmount);
                                }
                            }
                        }

                        if (mainWindow.RANDSETTING_MISC_ENEMYAIDISTANCES)
                        {
                            RandomizeAINames(package, random);
                        }



                        if (mainWindow.RANDSETTING_GALAXYMAP_PLANETNAMEDESCRIPTION && package.LocalTalkFiles.Any())
                        {
                            if (!loggedFilename)
                            {
                                Log.Information("Randomizing map file: " + files[i]);
                                loggedFilename = true;
                            }
                            UpdateGalaxyMapReferencesForTLKs(package.LocalTalkFiles, false);
                        }

                        if (mainWindow.RANDSETTING_WACK_SCOTTISH && package.LocalTalkFiles.Any())
                        {
                            if (!loggedFilename)
                            {
                                Log.Information("Randomizing map file: " + files[i]);
                                loggedFilename = true;
                            }

                            MakeTextPossiblyScottish(package.LocalTalkFiles, random, false);
                        }

                        foreach (var talkFile in package.LocalTalkFiles.Where(x => x.Modified))
                        {
                            talkFile.saveToExport();
                        }

                        if (package.ShouldSave || package.TlksModified)
                        {
                            ModifiedFiles[package.FileName] = package.FileName;
                            package.save();
                        }
                    }
                }
            }

            if (mainWindow.RANDSETTING_WACK_OPENINGCUTSCENE)
            {
                mainWindow.CurrentOperationText = "Randomizing opening cutscene";
                RandomizeOpeningCrawl(random, Tlks);
                RandomizeOpeningSequence(random);
                Log.Information("Applying fly-into-earth interp modification");
                ME1Package p = new ME1Package(Utilities.GetGameFile(@"BioGame\CookedPC\Maps\NOR\LAY\BIOA_NOR10_13_LAY.SFM"));
                p.getUExport(220).Data = Utilities.GetEmbeddedStaticFilesBinaryFile("exportreplacements.InterpMoveTrack_EarthCardIntro_220.bin");
                Log.Information("Applying shepard-faces-camera modification");
                p.getUExport(219).Data = Utilities.GetEmbeddedStaticFilesBinaryFile("exportreplacements.InterpMoveTrack_PlayerFaceCameraIntro_219.bin");
                p.save();
            }

            if (mainWindow.RANDSETTING_GALAXYMAP_PLANETNAMEDESCRIPTION)
            {
                Log.Information("Apply galaxy map background transparency fix");
                ME1Package p = new ME1Package(Utilities.GetGameFile(@"BioGame\CookedPC\Maps\NOR\DSG\BIOA_NOR10_03_DSG.SFM"));
                p.getUExport(1655).Data = Utilities.GetEmbeddedStaticFilesBinaryFile("exportreplacements.PC_GalaxyMap_BGFix_1655.bin");
                p.save();
                ModifiedFiles[p.FileName] = p.FileName;
            }

            if (mainWindow.RANDSETTING_WACK_SCOTTISH)
            {
                MakeTextPossiblyScottish(Tlks, random, true);
                //UpdateGalaxyMapReferencesForTLKs(package.LocalTalkFiles, false);
            }


            bool saveGlobalTLK = false;
            foreach (TalkFile tf in Tlks)
            {
                if (tf.Modified)
                {
                    //string xawText = tf.findDataById(138077); //Earth.
                    //Debug.WriteLine($"------------AFTER REPLACEMENT----{tf.export.ObjectName}------------------");
                    //Debug.WriteLine("New description:\n" + xawText);
                    //Debug.WriteLine("----------------------------------");
                    //Debugger.Break(); //Xawin
                    mainWindow.CurrentOperationText = "Saving TLKs";
                    ModifiedFiles[tf.export.FileRef.FileName] = tf.export.FileRef.FileName;
                    tf.saveToExport();
                }

                saveGlobalTLK = true;
            }

            if (saveGlobalTLK)
            {
                globalTLK.save();
            }

            AddMERSplash(random);
        }

        private void RandomizePawnMaterialInstances(IExportEntry exp, Random random)
        {
            //Don't know if this works

            //var hairMesh = exp.GetProperty<ObjectProperty>("m_oHairMesh");
            var hairMeshObj = exp.GetProperty<ObjectProperty>("m_oHairMesh");
            if (hairMeshObj != null)
            {
                var headMesh = exp.FileRef.getUExport(hairMeshObj.Value);
                var materials = headMesh.GetProperty<ArrayProperty<ObjectProperty>>("Materials");
                if (materials != null)
                {
                    foreach (var materialObj in materials)
                    {
                        //MAterialInstanceConstant
                        IExportEntry material = exp.FileRef.getUExport(materialObj.Value);
                        var props = material.GetProperties();

                        {
                            var scalars = props.GetProp<ArrayProperty<StructProperty>>("ScalarParameterValues");
                            var vectors = props.GetProp<ArrayProperty<StructProperty>>("VectorParameterValues");
                            if (scalars != null)
                            {
                                for (int i = 0; i < scalars.Count; i++)
                                {
                                    var scalar = scalars[i];
                                    var parameter = scalar.GetProp<NameProperty>("ParameterName");
                                    var currentValue = scalar.GetProp<FloatProperty>("ParameterValue");
                                    if (currentValue > 1)
                                    {
                                        scalar.GetProp<FloatProperty>("ParameterValue").Value = random.NextFloat(0, currentValue * 1.3);
                                    }
                                    else
                                    {
                                        Debug.WriteLine("Randomizing parameter " + scalar.GetProp<NameProperty>("ParameterName"));
                                        scalar.GetProp<FloatProperty>("ParameterValue").Value = random.NextFloat(0, 1);
                                    }
                                }

                                foreach (var vector in vectors)
                                {
                                    var paramValue = vector.GetProp<StructProperty>("ParameterValue");
                                    RandomizeTint(random, paramValue, false);
                                }
                            }
                        }
                        material.WriteProperties(props);
                    }
                }
            }
        }

        private void RandomizeSplash(Random random, ME1Package entrymenu)
        {
            IExportEntry planetMaterial = entrymenu.getUExport(1316);
            var props = planetMaterial.GetProperties();

            {
                var scalars = props.GetProp<ArrayProperty<StructProperty>>("ScalarParameterValues");
                var vectors = props.GetProp<ArrayProperty<StructProperty>>("VectorParameterValues");
                scalars[0].GetProp<FloatProperty>("ParameterValue").Value = random.NextFloat(0, 1.0); //Horizon Atmosphere Intensity
                if (random.Next(4) == 0)
                {
                    scalars[2].GetProp<FloatProperty>("ParameterValue").Value = random.NextFloat(0, 0.7); //Atmosphere Min (how gas-gianty it looks)
                }
                else
                {
                    scalars[2].GetProp<FloatProperty>("ParameterValue").Value = 0; //Atmosphere Min (how gas-gianty it looks)
                }

                scalars[3].GetProp<FloatProperty>("ParameterValue").Value = random.NextFloat(.5, 1.5); //Atmosphere Tiling U
                scalars[4].GetProp<FloatProperty>("ParameterValue").Value = random.NextFloat(.5, 1.5); //Atmosphere Tiling V
                scalars[4].GetProp<FloatProperty>("ParameterValue").Value = random.NextFloat(.5, 4); //Atmosphere Speed
                scalars[5].GetProp<FloatProperty>("ParameterValue").Value = random.NextFloat(0.5, 12); //Atmosphere Fall off...? seems like corona intensity

                foreach (var vector in vectors)
                {
                    var paramValue = vector.GetProp<StructProperty>("ParameterValue");
                    RandomizeTint(random, paramValue, false);
                }
            }
            planetMaterial.WriteProperties(props);

            //Corona
            IExportEntry coronaMaterial = entrymenu.getUExport(1317);
            props = coronaMaterial.GetProperties();
            {
                var scalars = props.GetProp<ArrayProperty<StructProperty>>("ScalarParameterValues");
                var vectors = props.GetProp<ArrayProperty<StructProperty>>("VectorParameterValues");
                scalars[0].GetProp<FloatProperty>("ParameterValue").Value = random.NextFloat(0.01, 0.05); //Bloom
                scalars[1].GetProp<FloatProperty>("ParameterValue").Value = random.NextFloat(1, 10); //Opacity
                RandomizeTint(random, vectors[0].GetProp<StructProperty>("ParameterValue"), false);
            }
            coronaMaterial.WriteProperties(props);

            //CameraPan
            IExportEntry cameraInterpData = entrymenu.getUExport(946);
            var interpLength = cameraInterpData.GetProperty<FloatProperty>("InterpLength");
            float animationLength = random.NextFloat(60, 120);
            ;
            interpLength.Value = animationLength;
            cameraInterpData.WriteProperty(interpLength);

            IExportEntry cameraInterpTrackMove = entrymenu.getUExport(967);
            cameraInterpTrackMove.Data = Utilities.GetEmbeddedStaticFilesBinaryFile("exportreplacements.InterpTrackMove967_EntryMenu_CameraPan.bin");
            props = cameraInterpTrackMove.GetProperties(forceReload: true);
            var posTrack = props.GetProp<StructProperty>("PosTrack");
            bool ZUp = false;
            if (posTrack != null)
            {
                var points = posTrack.GetProp<ArrayProperty<StructProperty>>("Points");
                float startx = random.NextFloat(-5100, -4800);
                float starty = random.NextFloat(13100, 13300);
                float startz = random.NextFloat(-39950, -39400);

                startx = -4930;
                starty = 13212;
                startz = -39964;

                float peakx = random.NextFloat(-5100, -4800);
                float peaky = random.NextFloat(13100, 13300);
                float peakz = random.NextFloat(-39990, -39920); //crazy small Z values here for some reason.
                ZUp = peakz > startz;

                if (points != null)
                {
                    int i = 0;
                    foreach (StructProperty s in points)
                    {
                        var outVal = s.GetProp<StructProperty>("OutVal");
                        if (outVal != null)
                        {
                            FloatProperty x = outVal.GetProp<FloatProperty>("X");
                            FloatProperty y = outVal.GetProp<FloatProperty>("Y");
                            FloatProperty z = outVal.GetProp<FloatProperty>("Z");
                            if (i != 1) x.Value = startx;
                            y.Value = i == 1 ? peaky : starty;
                            z.Value = i == 1 ? peakz : startz;
                        }

                        if (i > 0)
                        {
                            s.GetProp<FloatProperty>("InVal").Value = i == 1 ? (animationLength / 2) : animationLength;
                        }

                        i++;
                    }
                }
            }

            var eulerTrack = props.GetProp<StructProperty>("EulerTrack");
            if (eulerTrack != null)
            {
                var points = eulerTrack.GetProp<ArrayProperty<StructProperty>>("Points");
                //float startx = random.NextFloat(, -4800);
                float startPitch = random.NextFloat(25, 35);
                float startYaw = random.NextFloat(-195, -160);

                //startx = 1.736f;
                //startPitch = 31.333f;
                //startYaw = -162.356f;

                float peakx = 1.736f; //Roll
                float peakPitch = ZUp ? random.NextFloat(0, 30) : random.NextFloat(-15, 10); //Pitch
                float peakYaw = random.NextFloat(-315, -150);
                if (points != null)
                {
                    int i = 0;
                    foreach (StructProperty s in points)
                    {
                        var outVal = s.GetProp<StructProperty>("OutVal");
                        if (outVal != null)
                        {
                            FloatProperty x = outVal.GetProp<FloatProperty>("X");
                            FloatProperty y = outVal.GetProp<FloatProperty>("Y");
                            FloatProperty z = outVal.GetProp<FloatProperty>("Z");
                            //x.Value = i == 1 ? peakx : startx;
                            y.Value = i == 1 ? peakPitch : startPitch;
                            z.Value = i == 1 ? peakYaw : startYaw;
                        }

                        if (i > 0)
                        {
                            s.GetProp<FloatProperty>("InVal").Value = i == 1 ? (animationLength / 2) : animationLength;
                        }

                        i++;
                    }

                }
            }

            cameraInterpTrackMove.WriteProperties(props);

            var fovCurve = entrymenu.getUExport(964);
            fovCurve.Data = Utilities.GetEmbeddedStaticFilesBinaryFile("exportreplacements.InterpTrackMove964_EntryMenu_CameraFOV.bin");
            props = fovCurve.GetProperties(forceReload: true);
            //var pi = props.GetProp<ArrayProperty<StructProperty>>("Points");
            //var pi2 = props.GetProp<ArrayProperty<StructProperty>>("Points")[1].GetProp<FloatProperty>("OutVal");
            props.GetProp<StructProperty>("FloatTrack").GetProp<ArrayProperty<StructProperty>>("Points")[1].GetProp<FloatProperty>("OutVal").Value = random.NextFloat(65, 90); //FOV
            props.GetProp<StructProperty>("FloatTrack").GetProp<ArrayProperty<StructProperty>>("Points")[1].GetProp<FloatProperty>("InVal").Value = random.NextFloat(1, animationLength - 1);
            props.GetProp<StructProperty>("FloatTrack").GetProp<ArrayProperty<StructProperty>>("Points")[2].GetProp<FloatProperty>("InVal").Value = animationLength;
            fovCurve.WriteProperties(props);

            var menuTransitionAnimation = entrymenu.getUExport(968);
            props = menuTransitionAnimation.GetProperties();
            props.AddOrReplaceProp(new EnumProperty("IMF_RelativeToInitial", "EInterpTrackMoveFrame", MEGame.ME1, "MoveFrame"));
            props.GetProp<StructProperty>("EulerTrack").GetProp<ArrayProperty<StructProperty>>("Points")[0].GetProp<StructProperty>("OutVal").GetProp<FloatProperty>("X").Value = 0;
            props.GetProp<StructProperty>("EulerTrack").GetProp<ArrayProperty<StructProperty>>("Points")[0].GetProp<StructProperty>("OutVal").GetProp<FloatProperty>("Y").Value = 0;
            props.GetProp<StructProperty>("EulerTrack").GetProp<ArrayProperty<StructProperty>>("Points")[0].GetProp<StructProperty>("OutVal").GetProp<FloatProperty>("Z").Value = 0;

            props.GetProp<StructProperty>("EulerTrack").GetProp<ArrayProperty<StructProperty>>("Points")[1].GetProp<StructProperty>("OutVal").GetProp<FloatProperty>("X").Value = random.NextFloat(-180, 180);
            props.GetProp<StructProperty>("EulerTrack").GetProp<ArrayProperty<StructProperty>>("Points")[1].GetProp<StructProperty>("OutVal").GetProp<FloatProperty>("Y").Value = random.NextFloat(-180, 180);
            props.GetProp<StructProperty>("EulerTrack").GetProp<ArrayProperty<StructProperty>>("Points")[1].GetProp<StructProperty>("OutVal").GetProp<FloatProperty>("Z").Value = random.NextFloat(-180, 180);

            menuTransitionAnimation.WriteProperties(props);

            var dbStandard = entrymenu.getUExport(730);
            props = dbStandard.GetProperties();
            props.GetProp<ArrayProperty<StructProperty>>("OutputLinks")[1].GetProp<ArrayProperty<StructProperty>>("Links")[1].GetProp<ObjectProperty>("LinkedOp").Value = 2926; //Bioware logo
            dbStandard.WriteProperties(props);
        }


        private static readonly int[] GalaxyMapImageIdsThatArePlotReserved = { 1, 7, 8, 116, 117, 118, 119, 120, 121, 122, 123, 124 }; //Plot or Sol planets
        private static readonly int[] GalaxyMapImageIdsThatAreAsteroidReserved = { 70 }; //Asteroids
        private static readonly int[] GalaxyMapImageIdsThatAreFactoryReserved = { 6 }; //Asteroids
        private static readonly int[] GalaxyMapImageIdsThatAreMSVReserved = { 76, 79, 82, 85 }; //MSV Ships
        private static readonly int[] GalaxyMapImageIdsToNeverRandomize = { 127, 128 }; //no idea what these are

        private void RandomizePlanetImages(Random random, Dictionary<int, RandomizedPlanetInfo> planetsRowToRPIMapping, Bio2DA planets2DA)
        {
            mainWindow.CurrentOperationText = "Updating galaxy map images";
            mainWindow.ProgressBarIndeterminate = false;
            ME1Package galaxyMapImagesBasegame = new ME1Package(Utilities.GetGameFile(@"BioGame\CookedPC\Packages\GUI\GUI_SF_GalaxyMap.upk")); //lol demiurge, what were you doing?
            ME1Package ui2DAPackage = new ME1Package(Utilities.GetGameFile(@"BioGame\CookedPC\Packages\2DAs\BIOG_2DA_UI_X.upk")); //lol demiurge, what were you doing?
            IExportEntry galaxyMapImages2DAExport = ui2DAPackage.getUExport(8);
            var galaxyMapImages2DA = new Bio2DA(galaxyMapImages2DAExport);

            Dictionary<string, List<string>> galaxyMapGroupResources = new Dictionary<string, List<string>>();
            var resourceItems = Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(x => x.StartsWith("MassEffectRandomizer.staticfiles.galaxymapimages.")).ToList();
            var uniqueNames = new SortedSet<string>();

            //Get unique image group categories
            foreach (string str in resourceItems)
            {
                string[] parts = str.Split('.');
                if (parts.Length == 6)
                {
                    uniqueNames.Add(parts[3]);
                }
            }

            //Build group lists
            foreach (string groupname in uniqueNames)
            {
                galaxyMapGroupResources[groupname] = resourceItems.Where(x => x.StartsWith("MassEffectRandomizer.staticfiles.galaxymapimages." + groupname)).ToList();
                galaxyMapGroupResources[groupname].Shuffle(random);
            }

            //Get all exports for images
            var mapImageExports = galaxyMapImagesBasegame.Exports.Where(x => x.ObjectName.StartsWith("galMap")).ToList(); //Original galaxy map images
            var planet2daRowsWithImages = new List<int>();
            int imageIndexCol = planets2DA.GetColumnIndexByName("ImageIndex");
            int descriptionCol = planets2DA.GetColumnIndexByName("Description");
            int nextAddedImageIndex = int.Parse(galaxyMapImages2DA.RowNames.Last()); //we increment from here.
            //int nextGalaxyMap2DAImageRowIndex = 0; //THIS IS C# BASED

            //var mappedRPIs = planetsRowToRPIMapping.Values.ToList();

            mainWindow.ProgressBar_Bottom_Max = planets2DA.RowCount;

            Debug.WriteLine("----------------DICTIONARY OF PLANET INFO MAPPINGS:============");
            foreach (var kvp in planetsRowToRPIMapping)
            {
                //textBox3.Text += ("Key = {0}, Value = {1}", kvp.Key, kvp.Value);
                Debug.WriteLine("Key = {0}, Value = {1}", kvp.Key, kvp.Value.PlanetName);
            }
            Debug.WriteLine("----------------------------------------------------------------");
            List<int> assignedImageIndexes = new List<int>(); //This is used to generate new indexes for items that vanilla share values with (like MSV ships)
            for (int i = 0; i < planets2DA.RowCount; i++)
            {
                mainWindow.CurrentProgressValue = i;
                if (planets2DA[i, descriptionCol] == null || planets2DA[i, descriptionCol].GetIntValue() == 0)
                {
                    Debug.WriteLine("Skipping tlk -1 or blank row: (0-indexed) " + i);
                    continue; //Skip this row, its an asteroid belt (or liara's dig site)
                }

                //var assignedRPI = mappedRPIs[i];
                int rowName = i;
                //int.Parse(planets2DA.RowNames[i]);
                //Debug.WriteLine("Getting RPI via row #: " + planets2DA.RowNames[i] + ", using dictionary key " + rowName);

                if (planetsRowToRPIMapping.TryGetValue(rowName, out RandomizedPlanetInfo assignedRPI))
                {

                    if (galaxyMapGroupResources.TryGetValue(assignedRPI.ImageGroup.ToLower(), out var newImagePool))
                    {
                        string newImageResource = null;
                        if (newImagePool.Count > 0)
                        {
                            newImageResource = newImagePool[0];
                            if (assignedRPI.ImageGroup.ToLower() != "error")
                            {
                                //We can use error multiple times.
                                newImagePool.RemoveAt(0);
                            }
                        }
                        else
                        {
                            Debug.WriteLine("Not enough images in group " + assignedRPI.ImageGroup + " to continue randomization. Skipping row " + rowName);
                            continue;
                        }

                        Bio2DACell imageIndexCell = planets2DA[i, imageIndexCol];
                        if (imageIndexCell == null)
                        {
                            //Generating new cell that used to be blank - not sure if we should do this.
                            imageIndexCell = new Bio2DACell(Bio2DACell.Bio2DADataType.TYPE_INT, BitConverter.GetBytes(++nextAddedImageIndex));
                            planets2DA[i, imageIndexCol] = imageIndexCell;
                        }
                        else if (imageIndexCell.GetIntValue() < 0 || assignedImageIndexes.Contains(imageIndexCell.GetIntValue()))
                        {
                            //Generating new image value
                            imageIndexCell.DisplayableValue = (++nextAddedImageIndex).ToString();
                        }

                        assignedImageIndexes.Add(imageIndexCell.GetIntValue());

                        var newImageSwf = newImagePool;
                        IExportEntry matchingExport = null;

                        int uiTableRowName = imageIndexCell.GetIntValue();
                        int rowIndex = galaxyMapImages2DA.GetRowIndexByName(uiTableRowName.ToString());
                        if (rowIndex == -1)
                        {
                            //Create export and row first
                            matchingExport = mapImageExports[0].Clone();
                            matchingExport.indexValue = 0;
                            string objectName = "galMapMER" + nextAddedImageIndex;
                            matchingExport.idxObjectName = galaxyMapImagesBasegame.FindNameOrAdd(objectName);
                            galaxyMapImagesBasegame.addExport(matchingExport);
                            int newRowIndex = galaxyMapImages2DA.AddRow(nextAddedImageIndex.ToString());
                            int nameIndex = ui2DAPackage.FindNameOrAdd("GUI_SF_GalaxyMap." + objectName);
                            galaxyMapImages2DA[newRowIndex, "imageResource"] = new Bio2DACell(Bio2DACell.Bio2DADataType.TYPE_NAME, BitConverter.GetBytes((long)nameIndex));
                        }
                        else
                        {
                            string swfImageExportObjectName = galaxyMapImages2DA[rowIndex, "imageResource"].DisplayableValue;
                            //get object name of export inside of GUI_SF_GalaxyMap.upk
                            swfImageExportObjectName = swfImageExportObjectName.Substring(swfImageExportObjectName.IndexOf('.') + 1); //TODO: Need to deal with name instances for Pinnacle Station DLC. Because it's too hard for them to type a new name.
                            //Fetch export
                            matchingExport = mapImageExports.FirstOrDefault(x => x.ObjectName == swfImageExportObjectName);
                        }

                        if (matchingExport != null)
                        {
                            ReplaceSWFFromResource(matchingExport, newImageResource);
                        }
                        else
                        {
                            Debugger.Break();
                        }
                    }
                }
                else
                {
                    string nameTextForRow = planets2DA[i, 5].DisplayableValue;
                    Debug.WriteLine("Skipped row: " + rowName + ", " + nameTextForRow);
                }
            }

            galaxyMapImages2DA.Write2DAToExport();
            ui2DAPackage.save();
            ModifiedFiles[ui2DAPackage.FileName] = ui2DAPackage.FileName;
            galaxyMapImagesBasegame.save();
            ModifiedFiles[galaxyMapImagesBasegame.FileName] = galaxyMapImagesBasegame.FileName;
            planets2DA.Write2DAToExport(); //should save later
        }

        private void GalaxyMapValidationPass(Dictionary<int, RandomizedPlanetInfo> rowRPIMapping, Bio2DA planets2DA)
        {
            mainWindow.CurrentOperationText = "Running tests on galaxy map images";
            mainWindow.ProgressBarIndeterminate = false;
            mainWindow.ProgressBar_Bottom_Max = rowRPIMapping.Keys.Count;

            ME1Package galaxyMapImagesBasegame = new ME1Package(Utilities.GetGameFile(@"BioGame\CookedPC\Packages\GUI\GUI_SF_GalaxyMap.upk")); //lol demiurge, what were you doing?
            ME1Package ui2DAPackage = new ME1Package(Utilities.GetGameFile(@"BioGame\CookedPC\Packages\2DAs\BIOG_2DA_UI_X.upk")); //lol demiurge, what were you doing?

            IExportEntry galaxyMapImages2DAExport = ui2DAPackage.getUExport(8);
            var galaxyMapImages2DA = new Bio2DA(galaxyMapImages2DAExport);
            mainWindow.CurrentProgressValue = 0;

            foreach (int i in rowRPIMapping.Keys)
            {
                mainWindow.CurrentProgressValue++;

                //For every row in planets 2DA table
                if (planets2DA[i, "Description"] != null && planets2DA[i, "Description"].DisplayableValue != "-1")
                {
                    int imageRowReference = planets2DA[i, "ImageIndex"].GetIntValue();
                    if (imageRowReference == -1) continue; //We don't have enough images yet to pass this hurdle
                    //Use this value to find value in UI table
                    int rowIndex = galaxyMapImages2DA.GetRowIndexByName(imageRowReference.ToString());
                    string exportName = galaxyMapImages2DA[rowIndex, 0].DisplayableValue;
                    exportName = exportName.Substring(exportName.LastIndexOf('.') + 1);
                    //Use this value to find the export in GUI_SF file
                    var export = galaxyMapImagesBasegame.Exports.FirstOrDefault(x => x.ObjectName == exportName);
                    if (export == null)
                    {
                        Debugger.Break();
                    }
                    else
                    {
                        string path = export.GetProperty<StrProperty>("SourceFilePath").Value;
                        path = path.Substring(path.LastIndexOf(' ') + 1);
                        string[] parts = path.Split('.');
                        if (parts.Length == 6)
                        {
                            string swfImageGroup = parts[3];
                            var assignedRPI = rowRPIMapping[i];
                            if (assignedRPI.ImageGroup.ToLower() != swfImageGroup)
                            {
                                Debug.WriteLine("WRONG IMAGEGROUP ASSIGNED!");
                                Debugger.Break();
                            }
                        }
                        else
                        {
                            Debug.WriteLine("Source comment not correct format, might not yet be assigned: " + path);
                        }
                    }

                }
                else
                {
                    //Debugger.Break();
                }
            }
        }

        private void ReplaceSWFFromResource(IExportEntry exp, string swfResourcePath)
        {
            Debug.WriteLine($"Replacing {Path.GetFileName(exp.FileRef.FileName)} {exp.UIndex} {exp.ObjectName} SWF with {swfResourcePath}");
            var bytes = Utilities.GetEmbeddedStaticFilesBinaryFile(swfResourcePath, true);
            var props = exp.GetProperties();

            var rawData = props.GetProp<ArrayProperty<ByteProperty>>("Data");
            //Write SWF data
            rawData.Values = bytes.Select(b => new ByteProperty(b)).ToList();

            //Write SWF metadata
            props.AddOrReplaceProp(new StrProperty("MASS EFFECT RANDOMIZER - " + Path.GetFileName(swfResourcePath), "SourceFilePath"));
            props.AddOrReplaceProp(new StrProperty(DateTime.Now.ToString(), "SourceFileTimestamp"));
            StrProperty sourceFileTimestamp = props.GetProp<StrProperty>("SourceFileTimestamp");
            sourceFileTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            exp.WriteProperties(props);
        }

        private Dictionary<string, List<string>> mapNamesToFaceFxRandomizationLists;

        private void RandomizeFaceFX(IExportEntry exp, Random random, int amount)
        {
            try
            {
                ME1FaceFXAnimSet animSet = new ME1FaceFXAnimSet(exp);
                for (int i = 0; i < animSet.Data.Data.Count(); i++)
                {
                    var faceFxline = animSet.Data.Data[i];
                    //if (true)
                    if (random.Next(10 - amount) == 0)
                    {
                        //Randomize the names used for animation
                        List<int> usedIndexes = faceFxline.animations.Select(x => x.index).ToList();
                        usedIndexes.Shuffle(random);
                        for (int j = 0; j < faceFxline.animations.Length; j++)
                        {
                            faceFxline.animations[j].index = usedIndexes[j];
                        }
                    }
                    else
                    {
                        //Randomize the points
                        for (int j = 0; j < faceFxline.points.Length; j++)
                        {
                            var currentWeight = faceFxline.points[j].weight;
                            switch (amount)
                            {
                                case 1: //A few broken bones
                                    faceFxline.points[j].weight += random.NextFloat(-.25, .25);
                                    break;
                                case 2: //A significant amount of broken bones
                                    faceFxline.points[j].weight += random.NextFloat(-.5, .5);
                                    break;
                                case 3: //That's not how the face is supposed to work
                                    faceFxline.points[j].weight = random.NextFloat(Math.Max(-1 * Math.Abs(currentWeight) - 1, -1), Math.Min(Math.Abs(currentWeight) + 1, 1));
                                    break;
                                case 4: //Extreme
                                    faceFxline.points[j].weight = random.NextFloat(-20, 20);
                                    break;
                            }
                        }
                    }

                    //Debugging only: Get list of all animation names
                    //for (int j = 0; j < faceFxline.animations.Length; j++)
                    //{
                    //    var animationName = animSet.Header.Names[faceFxline.animations[j].index]; //animation name
                    //    faceFxBoneNames.Add(animationName);
                    //}
                }

                Log.Information("Randomized FaceFX for export " + exp.UIndex);
                animSet.Save();
                exp.FileRef.ShouldSave = true;
            }
            catch (Exception e)
            {
                //Do nothing for now.
            }
        }

        public void AddMERSplash(Random random)
        {
            ME1Package entrymenu = new ME1Package(Utilities.GetEntryMenuFile());

            //Connect attract to BWLogo
            var attractMovie = entrymenu.getUExport(729);
            var props = attractMovie.GetProperties();
            var movieName = props.GetProp<StrProperty>("m_sMovieName");
            movieName.Value = "merintro";
            props.GetProp<ArrayProperty<StructProperty>>("OutputLinks")[1].GetProp<ArrayProperty<StructProperty>>("Links")[0].GetProp<ObjectProperty>("LinkedOp").Value = 732; //Bioware logo
            attractMovie.WriteProperties(props);

            //Rewrite ShowSplash to BWLogo to point to merintro instead
            var showSplash = entrymenu.getUExport(736);
            props = showSplash.GetProperties();
            props.GetProp<ArrayProperty<StructProperty>>("OutputLinks")[0].GetProp<ArrayProperty<StructProperty>>("Links")[1].GetProp<ObjectProperty>("LinkedOp").Value = 729; //attractmovie logo
            showSplash.WriteProperties(props);

            //Visual only (for debugging): Remove connection to 

            //Update inputs to point to merintro comparebool
            var guiinput = entrymenu.getUExport(738);
            props = guiinput.GetProperties();
            foreach (var outlink in props.GetProp<ArrayProperty<StructProperty>>("OutputLinks"))
            {
                outlink.GetProp<ArrayProperty<StructProperty>>("Links")[0].GetProp<ObjectProperty>("LinkedOp").Value = 2936; //Comparebool
            }

            guiinput.WriteProperties(props);

            var playerinput = entrymenu.getUExport(739);
            props = playerinput.GetProperties();
            foreach (var outlink in props.GetProp<ArrayProperty<StructProperty>>("OutputLinks"))
            {
                var links = outlink.GetProp<ArrayProperty<StructProperty>>("Links");
                foreach (var link in links)
                {
                    link.GetProp<ObjectProperty>("LinkedOp").Value = 2936; //Comparebool
                }
            }

            playerinput.WriteProperties(props);

            //Clear old unused inputs for attract
            guiinput = entrymenu.getUExport(737);
            props = guiinput.GetProperties();
            foreach (var outlink in props.GetProp<ArrayProperty<StructProperty>>("OutputLinks"))
            {
                outlink.GetProp<ArrayProperty<StructProperty>>("Links").Clear();
            }

            guiinput.WriteProperties(props);

            playerinput = entrymenu.getUExport(740);
            props = playerinput.GetProperties();
            foreach (var outlink in props.GetProp<ArrayProperty<StructProperty>>("OutputLinks"))
            {
                outlink.GetProp<ArrayProperty<StructProperty>>("Links").Clear();
            }

            playerinput.WriteProperties(props);

            //Connect CompareBool outputs
            var mercomparebool = entrymenu.getUExport(2936);
            props = mercomparebool.GetProperties();
            var outlinks = props.GetProp<ArrayProperty<StructProperty>>("OutputLinks");
            //True
            var outlink1 = outlinks[0].GetProp<ArrayProperty<StructProperty>>("Links");
            StructProperty newLink = null;
            if (outlink1.Count == 0)
            {
                PropertyCollection p = new PropertyCollection();
                p.Add(new ObjectProperty(2938, "LinkedOp"));
                p.Add(new IntProperty(0, "InputLinkIdx"));
                p.Add(new NoneProperty());
                newLink = new StructProperty("SeqOpOutputInputLink", p);
                outlink1.Add(newLink);
            }
            else
            {
                newLink = outlink1[0];
            }

            newLink.GetProp<ObjectProperty>("LinkedOp").Value = 2938;

            //False
            var outlink2 = outlinks[1].GetProp<ArrayProperty<StructProperty>>("Links");
            newLink = null;
            if (outlink2.Count == 0)
            {
                PropertyCollection p = new PropertyCollection();
                p.Add(new ObjectProperty(2934, "LinkedOp"));
                p.Add(new IntProperty(0, "InputLinkIdx"));
                p.Add(new NoneProperty());
                newLink = new StructProperty("SeqOpOutputInputLink", p);
                outlink2.Add(newLink);
            }
            else
            {
                newLink = outlink2[0];
            }

            newLink.GetProp<ObjectProperty>("LinkedOp").Value = 2934;

            mercomparebool.WriteProperties(props);

            //Update output of setbool to next comparebool, point to shared true value
            var setBool = entrymenu.getUExport(2934);
            props = setBool.GetProperties();
            props.GetProp<ArrayProperty<StructProperty>>("OutputLinks")[0].GetProp<ArrayProperty<StructProperty>>("Links")[0].GetProp<ObjectProperty>("LinkedOp").Value = 729; //CompareBool (step 2)
            props.GetProp<ArrayProperty<StructProperty>>("VariableLinks")[1].GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables")[0].Value = 2952; //Shared True
            setBool.WriteProperties(props);


            //Default setbool should be false, not true
            var boolValueForMERSkip = entrymenu.getUExport(2955);
            var bValue = boolValueForMERSkip.GetProperty<IntProperty>("bValue");
            bValue.Value = 0;
            boolValueForMERSkip.WriteProperty(bValue);

            //Extract MER Intro
            string[] merIntros = { "merintro_a.bik", "merintro_b.bik", "merintro_c.bik" };
            string merToExtract = merIntros[random.Next(3)];
            Utilities.ExtractInternalFile("merintros." + merToExtract, Utilities.GetGameFile(@"BioGame\CookedPC\Movies\merintro.bik"), true);

            entrymenu.save();
            //Add to fileindex
            var fileIndex = Utilities.GetGameFile(@"BioGame\CookedPC\FileIndex.txt");
            var filesInIndex = File.ReadAllLines(fileIndex).ToList();
            if (!filesInIndex.Any(x => x == @"Movies\MERIntro.bik"))
            {
                filesInIndex.Add(@"Movies\MERIntro.bik");
                File.WriteAllLines(fileIndex, filesInIndex);
            }

            ModifiedFiles[entrymenu.FileName] = entrymenu.FileName;
        }

        private static string[] hazardTypes = { "Cold", "Heat", "Toxic", "Radiation", "Vacuum" };

        private void RandomizeHazard(IExportEntry export, Random random)
        {
            Log.Information("Randomizing hazard sequence objects for " + export.UIndex + ": " + export.GetIndexedFullPath);
            var variableLinks = export.GetProperty<ArrayProperty<StructProperty>>("VariableLinks");
            if (variableLinks != null)
            {
                foreach (var variableLink in variableLinks)
                {
                    var expectedType = export.FileRef.getEntry(variableLink.GetProp<ObjectProperty>("ExpectedType").Value).ObjectName;
                    var linkedVariable = export.FileRef.getUExport(variableLink.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables")[0].Value); //hoochie mama that is one big statement.

                    switch (expectedType)
                    {
                        case "SeqVar_Name":
                            //Hazard type
                            var hazardTypeProp = linkedVariable.GetProperty<NameProperty>("NameValue");
                            hazardTypeProp.Value = hazardTypes[random.Next(hazardTypes.Length)];
                            Log.Information(" >> Hazard type: " + hazardTypeProp.Value);
                            linkedVariable.WriteProperty(hazardTypeProp);
                            break;
                        case "SeqVar_Bool":
                            //Force helmet
                            var hazardHelmetProp = new IntProperty(random.Next(2), "bValue");
                            Log.Information(" >> Force helmet on: " + hazardHelmetProp.Value);
                            linkedVariable.WriteProperty(hazardHelmetProp);
                            break;
                        case "SeqVar_Int":
                            //Hazard level
                            var hazardLevelProp = new IntProperty(random.Next(4), "IntValue");
                            if (random.Next(8) == 0) //oof, for the player
                            {
                                hazardLevelProp.Value = 4;
                            }

                            Log.Information(" >> Hazard level: " + hazardLevelProp.Value);
                            linkedVariable.WriteProperty(hazardLevelProp);
                            break;
                    }
                }
            }
        }

        private void scaleHeadMesh(IExportEntry meshRef, float headScale)
        {
            Log.Information("Randomizing headmesh for " + meshRef.GetIndexedFullPath);
            var drawScale = meshRef.GetProperty<FloatProperty>("Scale");
            var drawScale3D = meshRef.GetProperty<StructProperty>("Scale3D");
            if (drawScale != null)
            {
                drawScale.Value = headScale * drawScale.Value;
                meshRef.WriteProperty(drawScale);
            }
            else if (drawScale3D != null)
            {
                PropertyCollection p = drawScale3D.Properties;
                p.AddOrReplaceProp(new FloatProperty(headScale, "X"));
                p.AddOrReplaceProp(new FloatProperty(headScale, "Y"));
                p.AddOrReplaceProp(new FloatProperty(headScale, "Z"));
                meshRef.WriteProperty(drawScale3D);
            }
            else
            {
                FloatProperty scale = new FloatProperty(headScale, "Scale");
                /*
                PropertyCollection p = new PropertyCollection();
                p.AddOrReplaceProp(new FloatProperty(headScale, "X"));
                p.AddOrReplaceProp(new FloatProperty(headScale, "Y"));
                p.AddOrReplaceProp(new FloatProperty(headScale, "Z"));
                meshRef.WriteProperty(new StructProperty("Vector", p, "Scale3D", true));*/
                meshRef.WriteProperty(scale);
            }
        }

        private void RandomizeInterpTrackMove(IExportEntry export, Random random, double amount)
        {
            Log.Information("Randomizing movement interpolations for " + export.UIndex + ": " + export.GetIndexedFullPath);
            var props = export.GetProperties();
            var posTrack = props.GetProp<StructProperty>("PosTrack");
            if (posTrack != null)
            {
                var points = posTrack.GetProp<ArrayProperty<StructProperty>>("Points");
                if (points != null)
                {
                    foreach (StructProperty s in points)
                    {
                        var outVal = s.GetProp<StructProperty>("OutVal");
                        if (outVal != null)
                        {
                            FloatProperty x = outVal.GetProp<FloatProperty>("X");
                            FloatProperty y = outVal.GetProp<FloatProperty>("Y");
                            FloatProperty z = outVal.GetProp<FloatProperty>("Z");
                            x.Value = x.Value * random.NextFloat(1 - amount, 1 + amount);
                            y.Value = y.Value * random.NextFloat(1 - amount, 1 + amount);
                            z.Value = z.Value * random.NextFloat(1 - amount, 1 + amount);
                        }
                    }
                }
            }

            var eulerTrack = props.GetProp<StructProperty>("EulerTrack");
            if (eulerTrack != null)
            {
                var points = eulerTrack.GetProp<ArrayProperty<StructProperty>>("Points");
                if (points != null)
                {
                    foreach (StructProperty s in points)
                    {
                        var outVal = s.GetProp<StructProperty>("OutVal");
                        if (outVal != null)
                        {
                            FloatProperty x = outVal.GetProp<FloatProperty>("X");
                            FloatProperty y = outVal.GetProp<FloatProperty>("Y");
                            FloatProperty z = outVal.GetProp<FloatProperty>("Z");
                            if (x.Value != 0)
                            {
                                x.Value = x.Value * random.NextFloat(1 - amount * 3, 1 + amount * 3);
                            }
                            else
                            {
                                x.Value = random.NextFloat(0, 360);
                            }

                            if (y.Value != 0)
                            {
                                y.Value = y.Value * random.NextFloat(1 - amount * 3, 1 + amount * 3);
                            }
                            else
                            {
                                y.Value = random.NextFloat(0, 360);
                            }

                            if (z.Value != 0)
                            {
                                z.Value = z.Value * random.NextFloat(1 - amount * 3, 1 + amount * 3);
                            }
                            else
                            {
                                z.Value = random.NextFloat(0, 360);
                            }
                        }
                    }
                }
            }

            export.WriteProperties(props);
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


        private void RandomizeMako(ME1Package package, Random random)
        {
            IExportEntry SVehicleSimTank = package.Exports[23314];
            var props = SVehicleSimTank.GetProperties();
            StructProperty torqueCurve = SVehicleSimTank.GetProperty<StructProperty>("m_TorqueCurve");
            ArrayProperty<StructProperty> points = torqueCurve.GetProp<ArrayProperty<StructProperty>>("Points");
            var minOut = random.Next(4000, 5600);
            var maxOut = random.Next(6000, 22000);
            var stepping = (maxOut - minOut) / 3; //starts at 0 with 3 upgrades
            for (int i = 0; i < points.Count; i++)
            {
                float newVal = minOut + (stepping * i);
                Log.Information($"Setting MakoTorque[{i}] to {newVal}");
                points[i].GetProp<FloatProperty>("OutVal").Value = newVal;
            }

            SVehicleSimTank.WriteProperty(torqueCurve);

            if (random.Next(1) == 0) //DEBUG ONLY!
            {
                //Reverse the steering to back wheels
                //Front
                IExportEntry LFWheel = package.Exports[36984];
                IExportEntry RFWheel = package.Exports[36987];
                //Rear
                IExportEntry LRWheel = package.Exports[36986];
                IExportEntry RRWheel = package.Exports[36989];

                var LFSteer = LFWheel.GetProperty<FloatProperty>("SteerFactor");
                var LRSteer = LRWheel.GetProperty<FloatProperty>("SteerFactor");
                var RFSteer = RFWheel.GetProperty<FloatProperty>("SteerFactor");
                var RRSteer = RRWheel.GetProperty<FloatProperty>("SteerFactor");

                LFSteer.Value = 0f;
                LRSteer.Value = 2f;
                RFSteer.Value = 0f;
                RRSteer.Value = 2f;

                LFWheel.WriteProperty(LFSteer);
                RFWheel.WriteProperty(RFSteer);
                LRWheel.WriteProperty(LRSteer);
                RRWheel.WriteProperty(RRSteer);
            }

            //Randomize the jumpjets
            IExportEntry BioVehicleBehaviorBase = package.Exports[23805];
            var behaviorProps = BioVehicleBehaviorBase.GetProperties();
            foreach (UProperty prop in behaviorProps)
            {
                if (prop.Name.Name.StartsWith("m_fThrusterScalar"))
                {
                    var floatprop = prop as FloatProperty;
                    floatprop.Value = random.NextFloat(.1, 6);
                }
            }

            BioVehicleBehaviorBase.WriteProperties(behaviorProps);
        }

        private void RandomizePlanetNameDescriptions(IExportEntry export, Random random, List<TalkFile> Tlks)
        {
            mainWindow.CurrentOperationText = "Applying entropy to galaxy map";
            string fileContents = Utilities.GetEmbeddedStaticFilesTextFile("planetinfo.xml");

            XElement rootElement = XElement.Parse(fileContents);
            var allMapRandomizationInfo = (from e in rootElement.Elements("RandomizedPlanetInfo")
                                           select new RandomizedPlanetInfo
                                           {
                                               PlanetName = (string)e.Element("PlanetName"),
                                               PlanetName2 = (string)e.Element("PlanetName2"), //Original name (plot planets only)
                                               PlanetDescription = (string)e.Element("PlanetDescription"),
                                               IsMSV = (bool)e.Element("IsMSV"),
                                               IsAsteroidBelt = (bool)e.Element("IsAsteroidBelt"),
                                               IsAsteroid = e.Element("IsAsteroid") != null && (bool)e.Element("IsAsteroid"),
                                               PreventShuffle = (bool)e.Element("PreventShuffle"),
                                               RowID = (int)e.Element("RowID"),
                                               MapBaseNames = e.Elements("MapBaseNames")
                                                   .Select(r => r.Value).ToList(),
                                               DLC = e.Element("DLC")?.Value,
                                               ImageGroup = e.Element("ImageGroup")?.Value
                                           }).ToList();

            fileContents = Utilities.GetEmbeddedStaticFilesTextFile("galaxymapclusters.xml");
            rootElement = XElement.Parse(fileContents);
            var suffixedClusterNames = rootElement.Elements("suffixedclustername").Select(x => x.Value).ToList(); //Used for assignments
            var suffixedClusterNamesForPreviousLookup = rootElement.Elements("suffixedclustername").Select(x => x.Value).ToList(); //Used to lookup previous assignments 
            VanillaSuffixedClusterNames = rootElement.Elements("originalsuffixedname").Select(x => x.Value).ToList();
            var nonSuffixedClusterNames = rootElement.Elements("nonsuffixedclustername").Select(x => x.Value).ToList();
            suffixedClusterNames.Shuffle(random);
            nonSuffixedClusterNames.Shuffle(random);

            fileContents = Utilities.GetEmbeddedStaticFilesTextFile("galaxymapsystems.xml");
            rootElement = XElement.Parse(fileContents);
            var shuffledSystemNames = rootElement.Elements("systemname").Select(x => x.Value).ToList();
            shuffledSystemNames.Shuffle(random);


            var everything = new List<string>();
            everything.AddRange(suffixedClusterNames);
            everything.AddRange(allMapRandomizationInfo.Select(x => x.PlanetName));
            everything.AddRange(allMapRandomizationInfo.Where(x => x.PlanetName2 != null).Select(x => x.PlanetName2));
            everything.AddRange(shuffledSystemNames);
            everything.AddRange(nonSuffixedClusterNames);

            foreach (var name1 in everything)
            {
                foreach (var name2 in everything)
                {
                    if (name1.Contains(name2) && name1 != name2)
                    {
                        //Debugger.Break();
                    }
                }
            }

            var msvInfos = allMapRandomizationInfo.Where(x => x.IsMSV).ToList();
            var asteroidInfos = allMapRandomizationInfo.Where(x => x.IsAsteroid).ToList();
            var asteroidBeltInfos = allMapRandomizationInfo.Where(x => x.IsAsteroidBelt).ToList();
            var planetInfos = allMapRandomizationInfo.Where(x => !x.IsAsteroidBelt && !x.IsAsteroid && !x.IsMSV && !x.PreventShuffle).ToList();

            msvInfos.Shuffle(random);
            asteroidInfos.Shuffle(random);
            planetInfos.Shuffle(random);

            List<int> rowsToNotRandomlyReassign = new List<int>();

            IExportEntry systemsExport = export.FileRef.Exports.First(x => x.ObjectName == "GalaxyMap_System");
            IExportEntry clustersExport = export.FileRef.Exports.First(x => x.ObjectName == "GalaxyMap_Cluster");
            IExportEntry areaMapExport = export.FileRef.Exports.First(x => x.ObjectName == "AreaMap_AreaMap");
            IExportEntry plotPlanetExport = export.FileRef.Exports.First(x => x.ObjectName == "GalaxyMap_PlotPlanet");
            IExportEntry mapExport = export.FileRef.Exports.First(x => x.ObjectName == "GalaxyMap_Map");

            Bio2DA systems2DA = new Bio2DA(systemsExport);
            Bio2DA clusters2DA = new Bio2DA(clustersExport);
            Bio2DA planets2DA = new Bio2DA(export);
            Bio2DA areaMap2DA = new Bio2DA(areaMapExport);
            Bio2DA plotPlanet2DA = new Bio2DA(plotPlanetExport);
            Bio2DA levelMap2DA = new Bio2DA(mapExport);

            //These dictionaries hold the mappings between the old names and new names and will be used in the 
            //map file pass as references to these are also contained in the localized map TLKs.
            systemNameMapping = new Dictionary<string, string>();
            clusterNameMapping = new Dictionary<string, SuffixedCluster>();
            planetNameMapping = new Dictionary<string, string>();


            //Cluster Names
            int nameColumnClusters = clusters2DA.GetColumnIndexByName("Name");
            //Used for resolving %SYSTEMNAME% in planet description and localization VO text
            Dictionary<int, SuffixedCluster> clusterIdToClusterNameMap = new Dictionary<int, SuffixedCluster>();

            for (int i = 0; i < clusters2DA.RowNames.Count; i++)
            {
                int tlkRef = clusters2DA[i, nameColumnClusters].GetIntValue();

                string oldClusterName = "";
                foreach (TalkFile tf in Tlks)
                {
                    oldClusterName = tf.findDataById(tlkRef);
                    if (oldClusterName != "No Data")
                    {
                        SuffixedCluster suffixedCluster = null;
                        if (VanillaSuffixedClusterNames.Contains(oldClusterName) || suffixedClusterNamesForPreviousLookup.Contains(oldClusterName))
                        {
                            suffixedClusterNamesForPreviousLookup.Remove(oldClusterName);
                            suffixedCluster = new SuffixedCluster(suffixedClusterNames[0], true);
                            suffixedClusterNames.RemoveAt(0);
                        }
                        else
                        {
                            suffixedCluster = new SuffixedCluster(nonSuffixedClusterNames[0], false);
                            nonSuffixedClusterNames.RemoveAt(0);
                        }

                        clusterNameMapping[oldClusterName] = suffixedCluster;
                        clusterIdToClusterNameMap[int.Parse(clusters2DA.RowNames[i])] = suffixedCluster;
                        break;
                    }
                }
            }

            //SYSTEMS
            //Used for resolving %SYSTEMNAME% in planet description and localization VO text
            Dictionary<int, (SuffixedCluster clustername, string systemname)> systemIdToSystemNameMap = new Dictionary<int, (SuffixedCluster clustername, string systemname)>();


            int nameColumnSystems = systems2DA.GetColumnIndexByName("Name");
            int clusterColumnSystems = systems2DA.GetColumnIndexByName("Cluster");
            for (int i = 0; i < systems2DA.RowNames.Count; i++)
            {

                string newSystemName = shuffledSystemNames[0];
                shuffledSystemNames.RemoveAt(0);
                int tlkRef = systems2DA[i, nameColumnSystems].GetIntValue();
                int clusterTableRow = systems2DA[i, clusterColumnSystems].GetIntValue();


                string oldSystemName = "";
                foreach (TalkFile tf in Tlks)
                {
                    oldSystemName = tf.findDataById(tlkRef);
                    if (oldSystemName != "No Data")
                    {
                        //tf.replaceString(tlkRef, newSystemName);
                        systemNameMapping[oldSystemName] = newSystemName;
                        systemIdToSystemNameMap[int.Parse(systems2DA.RowNames[i])] = (clusterIdToClusterNameMap[clusterTableRow], newSystemName);
                        break;
                    }
                }
            }

            //PLANETS
            int nameCol = planets2DA.GetColumnIndexByName("Name");
            int descCol = planets2DA.GetColumnIndexByName("Description");

            //mainWindow.CurrentProgressValue = 0;
            //mainWindow.ProgressBar_Bottom_Max = planets2DA.RowCount;
            //mainWindow.ProgressBarIndeterminate = false;
            var rowRPIMap = new Dictionary<int, RandomizedPlanetInfo>();
            for (int i = 0; i < planets2DA.RowCount; i++)
            {
                //mainWindow.CurrentProgressValue = i;
                int systemId = planets2DA[i, 1].GetIntValue();
                (SuffixedCluster clusterName, string systemName) systemClusterName = systemIdToSystemNameMap[systemId];

                Bio2DACell descriptionRefCell = planets2DA[i, descCol];
                int descriptionReference = descriptionRefCell?.GetIntValue() ?? 0;

                //var rowIndex = int.Parse(planets2DA.RowNames[i]);
                var info = allMapRandomizationInfo.FirstOrDefault(x => x.RowID == i);
                if (info != null)
                {
                    if (info.IsAsteroidBelt)
                    {
                        continue; //we don't care.
                    }
                    //found original info
                    RandomizedPlanetInfo rpi = null;
                    if (info.PreventShuffle)
                    {
                        //Shuffle with items of same rowindex.
                        //Todo post launch.


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
                        else if (info.IsAsteroid)
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


                    rowRPIMap[i] = rpi; //Map row in this table to the assigned RPI
                    string newPlanetName = rpi.PlanetName;
                    if (mainWindow.RANDSETTING_GALAXYMAP_PLANETNAMEDESCRIPTION_PLOTPLANET && rpi.PlanetName2 != null)
                    {
                        newPlanetName = rpi.PlanetName2;
                    }

                    //if (rename plot missions) planetName = rpi.PlanetName2
                    var description = rpi.PlanetDescription;
                    if (description != null)
                    {
                        SuffixedCluster clusterName = systemClusterName.clusterName;
                        string clusterString = systemClusterName.clusterName.ClusterName;
                        if (!clusterName.Suffixed)
                        {
                            clusterString += " cluster";
                        }
                        description = description.Replace("%CLUSTERNAME%", clusterString).Replace("%SYSTEMNAME%", systemClusterName.systemName).Replace("%PLANETNAME%", newPlanetName).TrimLines();
                    }

                    //var landableMapID = planets2DA[i, planets2DA.GetColumnIndexByName("Map")].GetIntValue();
                    int planetNameTlkId = planets2DA[i, nameCol].GetIntValue();

                    //Replace planet description here, as it won't be replaced in the overall pass
                    foreach (TalkFile tf in Tlks)
                    {
                        //Debug.WriteLine("Setting planet name on row index (not rowname!) " + i + " to " + newPlanetName);
                        string originalPlanetName = tf.findDataById(planetNameTlkId);
                        if ( /*newPlanetName == originalPlanetName && !rpi.PreventShuffle || */originalPlanetName == "No Data")
                        {
                            continue;
                        }

                        //tf.replaceString(planetNameTlkId, newPlanetName); //done in global references pass.
                        planetNameMapping[originalPlanetName] = newPlanetName;

                        //if (originalPlanetName == "Ilos") Debugger.Break();
                        if (descriptionReference != 0 && description != null)
                        {
                            tf.TlksIdsToNotUpdate.Add(descriptionReference);
                            Log.Information($"New planet: {newPlanetName}");
                            //if (descriptionReference == 138077)
                            //{
                            //    Debug.WriteLine($"------------SUBSTITUTING----{tf.export.ObjectName}------------------");
                            //    Debug.WriteLine($"{originalPlanetName} -> {newPlanetName}");
                            //    Debug.WriteLine("New description:\n" + description);
                            //    Debug.WriteLine("----------------------------------");
                            //    Debugger.Break(); //Xawin
                            //}
                            tf.replaceString(descriptionReference, description);
                            //break;
                        }
                    }
                }
                else
                {
                    Log.Error("No randomization data for galaxy map planet 2da, row id " + i);
                }
            }

            //UpdateGalaxyMapReferencesForTLKs(Tlks, true); //Update TLKs.
            //RandomizePlanetImages(random, rowRPIMap, planets2DA);
            //GalaxyMapValidationPass(rowRPIMap, planets2DA);
        }


        static readonly List<char> englishVowels = new List<char>(new char[] { 'a', 'e', 'i', 'o', 'u' });

        /// <summary>
        /// Swap the vowels around
        /// </summary>
        /// <param name="Tlks"></param>
        private void MakeTextPossiblyScottish(List<TalkFile> Tlks, Random random, bool updateProgressbar)
        {
            if (scottishVowelOrdering == null)
            {
                scottishVowelOrdering = new List<char>(new char[] { 'a', 'e', 'i', 'o', 'u' });
                scottishVowelOrdering.Shuffle(random);
            }

            int currentTlkIndex = 0;
            foreach (TalkFile tf in Tlks)
            {
                currentTlkIndex++;
                int max = tf.StringRefs.Count();
                int current = 0;
                if (updateProgressbar)
                {
                    mainWindow.CurrentOperationText = $"Applying Scottish accent [{currentTlkIndex}/{Tlks.Count()}]";
                    mainWindow.ProgressBar_Bottom_Max = tf.StringRefs.Length;
                    mainWindow.ProgressBarIndeterminate = false;
                }

                foreach (var sref in tf.StringRefs)
                {
                    current++;
                    if (tf.TlksIdsToNotUpdate.Contains(sref.StringID)) continue; //This string has already been updated and should not be modified.
                    if (updateProgressbar)
                    {
                        mainWindow.CurrentProgressValue = current;
                    }

                    if (!string.IsNullOrWhiteSpace(sref.Data))
                    {
                        string originalString = sref.Data;
                        if (originalString.Length == 1)
                        {
                            continue; //Don't modify I, A
                        }

                        string[] words = originalString.Split(' ');
                        for (int j = 0; j < words.Length; j++)
                        {
                            string word = words[j];
                            if (word.Length == 1)
                            {
                                continue; //Don't modify I, A
                            }

                            char[] newStringAsChars = word.ToArray();
                            for (int i = 0; i < word.Length; i++)
                            {
                                var vowelIndex = englishVowels.IndexOf(word[i]);
                                if (vowelIndex >= 0)
                                {
                                    if (i + 1 < word.Length && englishVowels.Contains(word[i + 1]))
                                    {
                                        continue; //don't modify dual vowel first letters.
                                    }
                                    else
                                    {
                                        newStringAsChars[i] = scottishVowelOrdering[(char)vowelIndex];
                                    }
                                }
                            }

                            words[j] = new string(newStringAsChars);
                        }

                        string rebuiltStr = string.Join(" ", words);
                        tf.replaceString(sref.StringID, rebuiltStr);
                    }
                }
            }
        }

        private void UpdateGalaxyMapReferencesForTLKs(List<TalkFile> Tlks, bool updateProgressbar)
        {
            int currentTlkIndex = 0;
            foreach (TalkFile tf in Tlks)
            {
                currentTlkIndex++;
                int max = tf.StringRefs.Count();
                int current = 0;
                if (updateProgressbar) //this will only be fired on basegame tlk's since they're the only ones that update the progerssbar.
                {
                    mainWindow.CurrentOperationText = $"Applying entropy to galaxy map [{currentTlkIndex}/{Tlks.Count()}]";
                    mainWindow.ProgressBar_Bottom_Max = tf.StringRefs.Length;
                    mainWindow.ProgressBarIndeterminate = false;
                    //text fixes.
                    //TODO: CHECK IF ORIGINAL VALUE IS BIOWARE - IF IT ISN'T ITS ALREADY BEEN UPDATED.
                    string testStr = tf.findDataById(179694);
                    if (testStr == "")
                    {
                        tf.replaceString(179694, "Head to the Armstrong Nebula to investigate what the geth are up to."); //Remove cluster after Nebula to ensure the text pass works without cluster cluster.

                    }
                    testStr = tf.findDataById(156006);
                    testStr = tf.findDataById(136011);

                    tf.replaceString(156006, "Go to the Newton System in the Kepler Verge and find the one remaining scientist assigned to the secret project.");
                    tf.replaceString(136011, "The geth have begun setting up a number of small outposts in the Armstrong Nebula of the Skyllian Verge. You must eliminate these outposts before the incursion becomes a full-scale invasion.");
                }

                //This is inefficient but not much I can do it about it.
                foreach (var sref in tf.StringRefs)
                {
                    current++;
                    if (tf.TlksIdsToNotUpdate.Contains(sref.StringID)) continue; //This string has already been updated and should not be modified.
                    if (updateProgressbar)
                    {
                        mainWindow.CurrentProgressValue = current;
                    }

                    if (!string.IsNullOrWhiteSpace(sref.Data))
                    {
                        string originalString = sref.Data;
                        string newString = sref.Data;
                        foreach (var planetMapping in planetNameMapping)
                        {

                            //Update TLK references to this planet.
                            bool originalPlanetNameIsSingleWord = !planetMapping.Key.Contains(" ");

                            if (originalPlanetNameIsSingleWord)
                            {
                                //This is to filter out things like Inti resulting in Intimidate
                                if (originalString.ContainsWord(planetMapping.Key) /*&& newString.ContainsWord(planetMapping.Key)*/) //second statement is disabled as its the same at this point in execution.
                                {
                                    //Do a replace if the whole word is matched only (no partial matches on words).
                                    newString = newString.Replace(planetMapping.Key, planetMapping.Value);
                                }
                            }
                            else
                            {
                                //Planets with spaces in the names won't (hopefully) match on Contains.
                                if (originalString.Contains(planetMapping.Key) && newString.Contains(planetMapping.Key))
                                {
                                    newString = newString.Replace(planetMapping.Key, planetMapping.Value);
                                }
                            }
                        }


                        foreach (var systemMapping in systemNameMapping)
                        {
                            //Update TLK references to this system.
                            bool originalSystemNameIsSingleWord = !systemMapping.Key.Contains(" ");
                            if (originalSystemNameIsSingleWord)
                            {
                                //This is to filter out things like Inti resulting in Intimidate
                                if (originalString.ContainsWord(systemMapping.Key) && newString.ContainsWord(systemMapping.Key))
                                {
                                    //Do a replace if the whole word is matched only (no partial matches on words).
                                    newString = newString.Replace(systemMapping.Key, systemMapping.Value);
                                }
                            }
                            else
                            {
                                //System with spaces in the names won't (hopefully) match on Contains.
                                if (originalString.Contains(systemMapping.Key) && newString.Contains(systemMapping.Key))
                                {
                                    newString = newString.Replace(systemMapping.Key, systemMapping.Value);
                                }
                            }
                        }



                        string test1 = "The geth must be stopped. Go to the Kepler Verge and stop them!";
                        string test2 = "Protect the heart of the Artemis Tau cluster!";

                        // >> test1 Detect types that end with Verge or Nebula, or types that end with an adjective.
                        // >> >> Determine if new name ends with Verge or Nebula or other terms that have a specific ending type that is an adjective of the area. (Castle for example)
                        // >> >> >> True: Do an exact replacement
                        // >> >> >> False: Check if the match is 100% matching on the whole thing. If it is, just replace the string. If it is not, replace the string but append the word "cluster".

                        // >> test 2 Determine if cluster follows the name of the item being replaced.
                        // >> >> Scan for the original key + cluster appended.
                        // >> >> >> True: If the new item includes an ending adjective, replace the whold thing with the word cluster included.
                        // >> >> >> False: If the new item doesn't end with an adjective, replace only the exact original key.

                        foreach (var clusterMapping in clusterNameMapping)
                        {
                            //Update TLK references to this cluster.
                            bool originalclusterNameIsSingleWord = !clusterMapping.Key.Contains(" ");
                            if (originalclusterNameIsSingleWord)
                            {
                                //Go to the Kepler Verge and end the threat.
                                //Old = Kepler Verge, New = Zoltan Homeworlds
                                if (originalString.ContainsWord(clusterMapping.Key) && newString.ContainsWord(clusterMapping.Key)) //
                                {

                                    //Terribly inefficent
                                    if (originalString.Contains("I'm asking you because the Normandy can get on-site quickly and quietly."))
                                        Debugger.Break();
                                    if (clusterMapping.Value.SuffixedWithCluster && !clusterMapping.Value.Suffixed)
                                    {
                                        //Replacing string like Local Cluster
                                        newString = newString.ReplaceInsensitive(clusterMapping.Key + " Cluster", clusterMapping.Value.ClusterName); //Go to the Voyager Cluster and... 
                                    }
                                    else
                                    {
                                        //Replacing string like Artemis Tau
                                        newString = newString.ReplaceInsensitive(clusterMapping.Key + " Cluster", clusterMapping.Value.ClusterName + " cluster"); //Go to the Voyager Cluster and... 
                                    }

                                    newString = newString.Replace(clusterMapping.Key, clusterMapping.Value.ClusterName); //catch the rest of the items.
                                    Debug.WriteLine(newString);
                                }
                            }
                            else
                            {
                                if (newString.Contains(clusterMapping.Key, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    //Terribly inefficent

                                    if (clusterMapping.Value.SuffixedWithCluster || clusterMapping.Value.Suffixed)
                                    {
                                        //Local Cluster
                                        if (VanillaSuffixedClusters.Contains(clusterMapping.Key))
                                        {
                                            newString = newString.ReplaceInsensitive(clusterMapping.Key, clusterMapping.Value.ClusterName); //Go to the Voyager Cluster and... 
                                        }
                                        else
                                        {
                                            newString = newString.ReplaceInsensitive(clusterMapping.Key + " Cluster", clusterMapping.Value.ClusterName); //Go to the Voyager Cluster and... 
                                        }
                                    }
                                    else
                                    {
                                        //Artemis Tau
                                        if (VanillaSuffixedClusters.Contains(clusterMapping.Key.ToLower()))
                                        {
                                            newString = newString.ReplaceInsensitive(clusterMapping.Key, clusterMapping.Value.ClusterName + " cluster"); //Go to the Voyager Cluster and... 
                                        }
                                        else
                                        {
                                            newString = newString.ReplaceInsensitive(clusterMapping.Key + " Cluster", clusterMapping.Value.ClusterName + " cluster"); //Go to the Voyager Cluster and... 
                                        }
                                    }

                                    newString = newString.ReplaceInsensitive(clusterMapping.Key, clusterMapping.Value.ClusterName); //catch the rest of the items.
                                    Debug.WriteLine(newString);
                                }
                            }
                        }

                        if (originalString != newString)
                        {
                            tf.replaceString(sref.StringID, newString);
                        }
                    }
                }
            }
        }

        public static void DumpPlanetTexts(IExportEntry export, TalkFile tf)
        {
            Bio2DA planets = new Bio2DA(export);
            var planetInfos = new List<RandomizedPlanetInfo>();

            int nameRefcolumn = planets.GetColumnIndexByName("Name");
            int descColumn = planets.GetColumnIndexByName("Description");

            for (int i = 0; i < planets.RowNames.Count; i++)
            {
                RandomizedPlanetInfo rpi = new RandomizedPlanetInfo();
                rpi.PlanetName = tf.findDataById(planets[i, nameRefcolumn].GetIntValue());

                var descCell = planets[i, descColumn];
                if (descCell != null)
                {
                    rpi.PlanetDescription = tf.findDataById(planets[i, 7].GetIntValue());
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

        static string FormatXml(string xml)
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

        private void RandomizeOpeningCrawl(Random random, List<TalkFile> Tlks)
        {
            Log.Information($"Randomizing opening crawl text");

            string fileContents = Utilities.GetEmbeddedStaticFilesTextFile("openingcrawls.xml");

            XElement rootElement = XElement.Parse(fileContents);
            var crawls = (from e in rootElement.Elements("CrawlText")
                          select new OpeningCrawl()
                          {
                              CrawlText = e.Value,
                              RequiresFaceRandomizer = e.Element("requiresfacerandomizer") != null && ((bool)e.Element("requiresfacerandomizer"))
                          }).ToList();
            crawls = crawls.Where(x => x.CrawlText != "").ToList();

            if (!mainWindow.RANDSETTING_MISC_MAPFACES)
            {
                crawls = crawls.Where(x => !x.RequiresFaceRandomizer).ToList();
            }

            string crawl = crawls[random.Next(crawls.Count)].CrawlText;
            crawl = crawl.TrimLines();
            //For length testing.
            //crawl = "It is a period of civil war. Rebel spaceships, striking from a hidden base, " +
            //        "have won their first victory against the evil Galactic Empire. During the battle, Rebel spies " +
            //        "managed to steal secret plans to the Empire's ultimate weapon, the DEATH STAR, an armored space station " +
            //        "with enough power to destroy an entire planet.\n\n" +
            //        "Pursued by the Empire's sinister agents, Princess Leia races home aboard her starship, custodian of the stolen plans that can " +
            //        "save her people and restore freedom to the galaxy.....";
            foreach (TalkFile tf in Tlks)
            {
                tf.replaceString(153106, crawl);
            }

        }

        private void RandomizeBioPawnSize(IExportEntry export, Random random, double amount)
        {
            Log.Information("Randomizing pawn size for " + export.UIndex + ": " + export.GetIndexedFullPath);
            var props = export.GetProperties();
            StructProperty sp = props.GetProp<StructProperty>("DrawScale3D");
            if (sp == null)
            {
                var structprops = ME1UnrealObjectInfo.getDefaultStructValue("Vector", true);
                sp = new StructProperty("Vector", structprops, "DrawScale3D", ME1UnrealObjectInfo.isImmutableStruct("Vector"));
                props.Add(sp);
            }

            if (sp != null)
            {
                //Debug.WriteLine("Randomizing morph face " + Path.GetFileName(export.FileRef.FileName) + " " + export.UIndex + " " + export.GetFullPath + " vPos");
                FloatProperty x = sp.GetProp<FloatProperty>("X");
                FloatProperty y = sp.GetProp<FloatProperty>("Y");
                FloatProperty z = sp.GetProp<FloatProperty>("Z");
                if (x.Value == 0) x.Value = 1;
                if (y.Value == 0) y.Value = 1;
                if (z.Value == 0) z.Value = 1;
                x.Value = x.Value * random.NextFloat(1 - amount, 1 + amount);
                y.Value = y.Value * random.NextFloat(1 - amount, 1 + amount);
                z.Value = z.Value * random.NextFloat(1 - amount, 1 + amount);
            }

            export.WriteProperties(props);
            //export.GetProperties(true);
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

            Bio2DA movementSpeed2DA = new Bio2DA(export);
            int[] colsToRandomize = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 15, 16, 17, 18, 19 };
            for (int row = 0; row < movementSpeed2DA.RowNames.Count(); row++)
            {
                for (int i = 0; i < colsToRandomize.Count(); i++)
                {
                    //Console.WriteLine("[" + row + "][" + colsToRandomize[i] + "] value is " + BitConverter.ToSingle(cluster2da[row, colsToRandomize[i]].Data, 0));
                    int randvalue = random.Next(10, 1200);
                    Console.WriteLine("Movement Speed Randomizer [" + row + "][" + colsToRandomize[i] + "] value is now " + randvalue);
                    movementSpeed2DA[row, colsToRandomize[i]].Data = BitConverter.GetBytes(randvalue);
                    movementSpeed2DA[row, colsToRandomize[i]].Type = Bio2DACell.Bio2DADataType.TYPE_INT;
                }
            }

            movementSpeed2DA.Write2DAToExport();
        }

        //private void RandomizeGalaxyMap(Random random)
        //{
        //    ME1Package engine = new ME1Package(Utilities.GetEngineFile());

        //    foreach (IExportEntry export in engine.Exports)
        //    {
        //        switch (export.ObjectName)
        //        {
        //            case "GalaxyMap_Cluster":
        //                //RandomizeClustersXY(export, random);
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
        private void RandomizeClustersXY(IExportEntry export, Random random, List<TalkFile> Tlks)
        {
            mainWindow.CurrentOperationText = "Randomizing Galaxy Map - Clusters";

            Bio2DA cluster2da = new Bio2DA(export);
            int xColIndex = cluster2da.GetColumnIndexByName("X");
            int yColIndex = cluster2da.GetColumnIndexByName("Y");

            for (int row = 0; row < cluster2da.RowNames.Count(); row++)
            {
                //Randomize X,Y
                float randvalue = random.NextFloat(0, 1);
                cluster2da[row, xColIndex].Data = BitConverter.GetBytes(randvalue);
                randvalue = random.NextFloat(0, 1);
                cluster2da[row, yColIndex].Data = BitConverter.GetBytes(randvalue);
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
            int[] colsToRandomize = { 2, 3 }; //X,Y
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
                system2da[row, 9].Type = Bio2DACell.Bio2DADataType.TYPE_FLOAT;
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
            int[] colsToRandomize = { 2, 3 }; //X,Y
            for (int row = 0; row < planet2da.RowNames.Count(); row++)
            {
                for (int i = 0; i < planet2da.ColumnNames.Count(); i++)
                {
                    if (planet2da[row, i] != null && planet2da[row, i].Type == Bio2DACell.Bio2DADataType.TYPE_FLOAT)
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

        private void RandomizeOpeningSequence(Random random)
        {
            Log.Information($"Randomizing opening cutscene");

            ME1Package p = new ME1Package(Utilities.GetGameFile(@"BioGame\CookedPC\Maps\PRO\CIN\BIOA_GLO00_A_Opening_Flyby_CIN.SFM"));
            foreach (var ex in p.Exports)
            {
                if (ex.ClassName == "BioSunFlareComponent")
                {
                    var tint = ex.GetProperty<StructProperty>("FlareTint");
                    if (tint != null)
                    {
                        RandomizeTint(random, tint, false);
                        ex.WriteProperty(tint);
                    }
                }
                else if (ex.ClassName == "BioSunActor")
                {
                    var tint = ex.GetProperty<StructProperty>("SunTint");
                    if (tint != null)
                    {
                        RandomizeTint(random, tint, false);
                        ex.WriteProperty(tint);
                    }
                }

            }

            p.save();
        }

        private void RandomizeTint(Random random, StructProperty tint, bool randomizeAlpha)
        {
            var a = tint.GetProp<FloatProperty>("A");
            var r = tint.GetProp<FloatProperty>("R");
            var g = tint.GetProp<FloatProperty>("G");
            var b = tint.GetProp<FloatProperty>("B");

            float totalTintValue = r + g + b;

            //Randomizing hte pick order will ensure we get a random more-dominant first color (but only sometimes).
            //e.g. if e went in R G B order red would always have a chance at a higher value than the last picked item
            List<FloatProperty> randomOrderChooser = new List<FloatProperty>();
            randomOrderChooser.Add(r);
            randomOrderChooser.Add(g);
            randomOrderChooser.Add(b);
            randomOrderChooser.Shuffle(random);

            randomOrderChooser[0].Value = random.NextFloat(0, totalTintValue);
            totalTintValue -= randomOrderChooser[0].Value;

            randomOrderChooser[1].Value = random.NextFloat(0, totalTintValue);
            totalTintValue -= randomOrderChooser[1].Value;

            randomOrderChooser[2].Value = totalTintValue;
            if (randomizeAlpha)
            {
                a.Value = random.NextFloat(0, 1);
            }
        }

        /// <summary>
        /// Randomizes the planet-level galaxy map view. 
        /// </summary>
        /// <param name="export">2DA Export</param>
        /// <param name="random">Random number generator</param>
        private void RandomizeWeaponStats(IExportEntry export, Random random)
        {
            mainWindow.CurrentOperationText = "Randomizing Item Levels (only partially implemented)";


            //Console.WriteLine("Randomizing Items - Item Effect Levels");
            Bio2DA itemeffectlevels2da = new Bio2DA(export);

            if (mainWindow.RANDSETTING_MOVEMENT_MAKO)
            {
                float makoCannonFiringRate = random.NextFloat(0.1, 3);
                float makoCannonForce = random.NextFloat(1000, 5000) + random.NextFloat(0, 2000);
                float makoCannonDamage = 120 / makoCannonFiringRate; //to same damage amount.
                float damageincrement = random.NextFloat(60, 90);
                for (int i = 0; i < 10; i++)
                {
                    itemeffectlevels2da[598, 4 + i].Data = BitConverter.GetBytes(makoCannonFiringRate); //RPS
                    itemeffectlevels2da[604, 4 + i].Data = BitConverter.GetBytes(makoCannonDamage + (i * damageincrement)); //Damage
                    itemeffectlevels2da[617, 4 + i].Data = BitConverter.GetBytes(makoCannonForce);
                }
            }
            //Randomize 
            //for (int row = 0; row < itemeffectlevels2da.RowNames.Count(); row++)
            //{
            //    Bio2DACell propertyCell = itemeffectlevels2da[row, 2];
            //    if (propertyCell != null)
            //    {
            //        int gameEffect = propertyCell.GetIntValue();
            //        switch (gameEffect)
            //        {
            //            case 15:
            //                //GE_Weap_Damage
            //                ItemEffectLevels.Randomize_GE_Weap_Damage(itemeffectlevels2da, row, random);
            //                break;
            //            case 17:
            //                //GE_Weap_RPS
            //                ItemEffectLevels.Randomize_GE_Weap_RPS(itemeffectlevels2da, row, random);
            //                break;
            //            case 447:
            //                //GE_Weap_Projectiles
            //                ItemEffectLevels.Randomize_GE_Weap_PhysicsForce(itemeffectlevels2da, row, random);
            //                break;
            //            case 1199:
            //                //GE_Weap_HeatPerShot
            //                ItemEffectLevels.Randomize_GE_Weap_HeatPerShot(itemeffectlevels2da, row, random);
            //                break;
            //            case 1201:
            //                //GE_Weap_HeatLossRate
            //                ItemEffectLevels.Randomize_GE_Weap_HeatLossRate(itemeffectlevels2da, row, random);
            //                break;
            //            case 1259:
            //                //GE_Weap_HeatLossRateOH
            //                ItemEffectLevels.Randomize_GE_Weap_HeatLossRateOH(itemeffectlevels2da, row, random);
            //                break;
            //        }
            //    }
            //}

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
                if (powersToNotReassign.Contains(talentId))
                {
                    continue;
                }

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

            var playerPowersShuffled = TalentsShuffler.TalentShuffle(powersToReassignPlayerMaster, 6, 9, random);
            var squadPowersShuffled = TalentsShuffler.TalentShuffle(powersToReassignSquadMaster, 6, 9, random);

            //ASSIGN POWERS TO TABLE

            // >> Player
            for (int classId = 0; classId < 6; classId++)
            {
                int assignmentStartRow = (classId * 16) + 5; //16 powers per player, the first 5 of each are setup, the last 2 are charm/intimidate
                var talentList = playerPowersShuffled[classId];
                for (int i = 0; i < talentList.Count; i++)
                {
                    Log.Information("Talent randomizer [PLAYER - CLASSID " + classId + "]: Setting row " + (assignmentStartRow + i) + " to " + talentList[i]);
                    classtalents[assignmentStartRow + i, 1].Data = BitConverter.GetBytes(talentList[i]);
                }
            }

            // >> Squad
            int currentClassId = -1;
            List<int> currentList = null;
            for (int i = 0; i < classtalents.RowNames.Count; i++)
            {
                int rowClassId = classtalents[i, 0].GetIntValue();
                if (rowClassId == 10 || rowClassId < 6) continue; //skip supersoldier, player classes
                int currentTalentId = classtalents[i, 1].GetIntValue();
                if (rowClassId != currentClassId)
                {
                    currentList = squadPowersShuffled[0];
                    squadPowersShuffled.RemoveAt(0);
                    currentClassId = rowClassId;
                    //Krogan only has 2 non-assignable powers
                    if (currentClassId == 7)
                    {
                        i += 2;
                    }
                    else
                    {
                        i += 3;
                    }
                }

                int newPowerToAssign = currentList[0];
                currentList.RemoveAt(0);
                Log.Information("Talent randomizer [SQUAD - CLASSID " + currentClassId + "]: Setting row " + i + " to " + newPowerToAssign);
                classtalents[i, 1].Data = BitConverter.GetBytes(newPowerToAssign);
            }

            //UPDATE UNLOCKS (in reverse)
            int prereqTalentCol = classtalents.GetColumnIndexByName("PrereqTalent0");
            for (int row = classtalents.RowNames.Count() - 1; row > 0; row--)
            {
                var hasPrereq = classtalents[row, prereqTalentCol] != null;
                if (hasPrereq)
                {
                    classtalents[row, prereqTalentCol].Data = BitConverter.GetBytes(classtalents[row - 1, 1].GetIntValue()); //Talent ID of above row
                }
            }

            /*
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

                            //squadmate class
                            int talentId = squadReassignmentList[0];
                            squadReassignmentList.RemoveAt(0);
                            classtalents[row, 1].SetData(talentId);
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
            }*/

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
            Log.Information("Reassigned talents");
            classtalents.Write2DAToExport();

            return true;










            /*








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
            return true;*/
        }

        private static string[] TalentEffectsToRandomize_THROW = { "GE_TKThrow_CastingTime", "GE_TKThrow_Kickback", "GE_TKThrow_CooldownTime", "GE_TKThrow_ImpactRadius", "GE_TKThrow_Force" };
        private static string[] TalentEffectsToRandomize_LIFT = { "GE_TKLift_Force", "GE_TKLift_EffectDuration", "GE_TKLift_ImpactRadius", "GE_TKLift_CooldownTime" };

        public bool RunMapRandomizerPass
        {
            get => mainWindow.RANDSETTING_MISC_MAPFACES
                   || mainWindow.RANDSETTING_MISC_MAPPAWNSIZES
                   || mainWindow.RANDSETTING_MISC_HAZARDS
                   || mainWindow.RANDSETTING_MISC_INTERPS
                   || mainWindow.RANDSETTING_MISC_ENEMYAIDISTANCES
                   || mainWindow.RANDSETTING_GALAXYMAP_PLANETNAMEDESCRIPTION
                   || mainWindow.RANDSETTING_WACK_FACEFX
                   || mainWindow.RANDSETTING_WACK_SCOTTISH
                   || mainWindow.RANDSETTING_PAWN_MATERIALCOLORS
            ;
        }

        public bool RunMapRandomizerPassAllExports
        {
            get => mainWindow.RANDSETTING_MISC_MAPFACES
                   || mainWindow.RANDSETTING_MISC_MAPPAWNSIZES
                   || mainWindow.RANDSETTING_MISC_HAZARDS
                   || mainWindow.RANDSETTING_WACK_FACEFX
                   || mainWindow.RANDSETTING_MISC_INTERPS
                   || mainWindow.RANDSETTING_WACK_SCOTTISH
                   || mainWindow.RANDSETTING_PAWN_MATERIALCOLORS
            ;
        }

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
                                    if (level > 6)
                                    {
                                        header = "Advanced Throw";
                                    }

                                    if (level >= 11)
                                    {
                                        header = "Master Throw";
                                    }

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
                                    if (level > 6)
                                    {
                                        header = "Advanced Lift";
                                    }

                                    if (level >= 11)
                                    {
                                        header = "Master Lift";
                                    }

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
                            if (cell.Type == Bio2DACell.Bio2DADataType.TYPE_FLOAT)
                            {
                                challenge2da[row, col].Data = BitConverter.GetBytes(challenge2da[row, col].GetFloatValue() * multiplier);
                            }
                            else
                            {
                                challenge2da[row, col].Data = BitConverter.GetBytes(challenge2da[row, col].GetIntValue() * multiplier);
                                challenge2da[row, col].Type = Bio2DACell.Bio2DADataType.TYPE_FLOAT;
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
        private void RandomizeCharacterCreator2DA(Random random, IExportEntry export)
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

                        if (cell.Type == Bio2DACell.Bio2DADataType.TYPE_FLOAT)
                        {
                            cell.Data = BitConverter.GetBytes(cell.GetFloatValue() * multiplier);
                            hasChanges = true;
                        }
                        else
                        {
                            cell.Data = BitConverter.GetBytes(cell.GetIntValue() * multiplier);
                            cell.Type = Bio2DACell.Bio2DADataType.TYPE_FLOAT;
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
                        cell.Type = Bio2DACell.Bio2DADataType.TYPE_FLOAT;
                        Console.WriteLine("[" + row + "][" + col + "]  (" + export2da.ColumnNames[col] + ") value now is " + cell.GetDisplayableValue());
                        hasChanges = true;
                        continue;
                    }

                    //Skin Tone
                    if (cell != null && cell.Type == Bio2DACell.Bio2DADataType.TYPE_NAME)
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
                    if (columnName.Contains("Scalar") && cell != null && cell.Type != Bio2DACell.Bio2DADataType.TYPE_NAME)
                    {
                        float currentValue = float.Parse(cell.GetDisplayableValue());
                        cell.Data = BitConverter.GetBytes(currentValue * random.NextFloat(0.5, 2));
                        cell.Type = Bio2DACell.Bio2DADataType.TYPE_FLOAT;
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

        private void RandomizeCharacterCreatorSingular(Random random, List<TalkFile> Tlks)
        {
            //non-2da character creator changes.

            //Randomize look at targets
            ME1Package biog_uiworld = new ME1Package(Utilities.GetGameFile(@"BioGame\CookedPC\Maps\BIOG_UIWorld.sfm"));
            var bioInerts = biog_uiworld.Exports.Where(x => x.ClassName == "BioInert").ToList();
            foreach (IExportEntry ex in bioInerts)
            {
                RandomizeLocation(ex, random);
            }

            //Randomize face-zoom in
            var zoomInOnFaceInterp = biog_uiworld.getUExport(385);
            var eulerTrack = zoomInOnFaceInterp.GetProperty<StructProperty>("EulerTrack");
            var points = eulerTrack?.GetProp<ArrayProperty<StructProperty>>("Points");
            if (points != null)
            {
                var s = points[2]; //end point
                var outVal = s.GetProp<StructProperty>("OutVal");
                if (outVal != null)
                {
                    FloatProperty x = outVal.GetProp<FloatProperty>("X");
                    //FloatProperty y = outVal.GetProp<FloatProperty>("Y");
                    //FloatProperty z = outVal.GetProp<FloatProperty>("Z");
                    x.Value = random.NextFloat(0, 360);
                    //y.Value = y.Value * random.NextFloat(1 - amount * 3, 1 + amount * 3);
                    //z.Value = z.Value * random.NextFloat(1 - amount * 3, 1 + amount * 3);
                }
            }

            zoomInOnFaceInterp.WriteProperty(eulerTrack);
            biog_uiworld.save();
            ModifiedFiles[biog_uiworld.FileName] = biog_uiworld.FileName;

            //Psych Profiles
            string fileContents = Utilities.GetEmbeddedStaticFilesTextFile("psychprofiles.xml");

            XElement rootElement = XElement.Parse(fileContents);
            var childhoods = rootElement.Descendants("childhood").Where(x => x.Value != "").Select(x => (x.Attribute("name").Value, string.Join("\n", x.Value.Split('\n').Select(s => s.Trim())))).ToList();
            var reputations = rootElement.Descendants("reputation").Where(x => x.Value != "").Select(x => (x.Attribute("name").Value, string.Join("\n", x.Value.Split('\n').Select(s => s.Trim())))).ToList();

            childhoods.Shuffle(random);
            reputations.Shuffle(random);

            var backgroundTlkPairs = new List<(int nameId, int descriptionId)>();
            backgroundTlkPairs.Add((45477, 34931)); //Spacer
            backgroundTlkPairs.Add((45508, 34940)); //Earthborn
            backgroundTlkPairs.Add((45478, 34971)); //Colonist
            for (int i = 0; i < 3; i++)
            {
                foreach (var tlk in Tlks)
                {
                    tlk.replaceString(backgroundTlkPairs[i].nameId, childhoods[i].Item1);
                    tlk.replaceString(backgroundTlkPairs[i].descriptionId, childhoods[i].Item2);
                }
            }

            backgroundTlkPairs.Clear();
            backgroundTlkPairs.Add((45482, 34934)); //Sole Survivor
            backgroundTlkPairs.Add((45483, 34936)); //War Hero
            backgroundTlkPairs.Add((45484, 34938)); //Ruthless
            for (int i = 0; i < 3; i++)
            {
                foreach (var tlk in Tlks)
                {
                    tlk.replaceString(backgroundTlkPairs[i].nameId, reputations[i].Item1);
                    tlk.replaceString(backgroundTlkPairs[i].descriptionId, reputations[i].Item2);
                }
            }

        }

        private void RandomizeLocation(IExportEntry e, Random random)
        {
            SetLocation(e, random.NextFloat(-100000, 100000), random.NextFloat(-100000, 100000), random.NextFloat(-100000, 100000));
        }

        public static void SetLocation(IExportEntry export, float x, float y, float z)
        {
            StructProperty prop = export.GetProperty<StructProperty>("location");
            SetLocation(prop, x, y, z);
            export.WriteProperty(prop);
        }

        public static Point3D GetLocation(IExportEntry export)
        {
            float x = 0, y = 0, z = int.MinValue;
            var prop = export.GetProperty<StructProperty>("location");
            if (prop != null)
            {
                foreach (var locprop in prop.Properties)
                {
                    switch (locprop)
                    {
                        case FloatProperty fltProp when fltProp.Name == "X":
                            x = fltProp;
                            break;
                        case FloatProperty fltProp when fltProp.Name == "Y":
                            y = fltProp;
                            break;
                        case FloatProperty fltProp when fltProp.Name == "Z":
                            z = fltProp;
                            break;
                    }
                }

                return new Point3D(x, y, z);
            }

            return null;
        }

        public class Point3D
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Z { get; set; }

            public Point3D()
            {

            }

            public Point3D(double X, double Y, double Z)
            {
                this.X = X;
                this.Y = Y;
                this.Z = Z;
            }

            public double getDistanceToOtherPoint(Point3D other)
            {
                double deltaX = X - other.X;
                double deltaY = Y - other.Y;
                double deltaZ = Z - other.Z;

                return Math.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);
            }

            public override string ToString()
            {
                return $"{X},{Y},{Z}";
            }
        }

        public static void SetLocation(StructProperty prop, float x, float y, float z)
        {
            prop.GetProp<FloatProperty>("X").Value = x;
            prop.GetProp<FloatProperty>("Y").Value = y;
            prop.GetProp<FloatProperty>("Z").Value = z;
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
                    export2da[row, col].Type = Bio2DACell.Bio2DADataType.TYPE_INT;
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
                    if (music2da[row, col] != null && music2da[row, col].Type == Bio2DACell.Bio2DADataType.TYPE_NAME)
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
                    if (music2da[row, col] != null && music2da[row, col].Type == Bio2DACell.Bio2DADataType.TYPE_NAME)
                    {
                        if (!music2da[row, col].GetDisplayableValue().StartsWith("music"))
                        {
                            continue;
                        }

                        Log.Information("[" + row + "][" + col + "]  (" + music2da.ColumnNames[col] + ") value originally is " + music2da[row, col].GetDisplayableValue());
                        int r = random.Next(names.Count);
                        byte[] pnr = names[r];
                        names.RemoveAt(r);
                        music2da[row, col].Data = pnr;
                        Log.Information("Music Randomizer [" + row + "][" + col + "] (" + music2da.ColumnNames[col] + ") value is now " + music2da[row, col].GetDisplayableValue());

                    }
                }
            }

            music2da.Write2DAToExport();
        }

        private string[] aiTypes =
        {
            "BioAI_Krogan", "BioAI_Assault", "BioAI_AssaultDrone", "BioAI_Charge", "BioAI_Commander", "BioAI_Destroyer", "BioAI_Drone",
            "BioAI_GunShip", "BioAI_HumanoidMinion", "BioAI_Juggernaut", "BioAI_Melee", "BioAI_Mercenary", "BioAI_Rachnii", "BioAI_Sniper"
        };

        private Dictionary<string, string> systemNameMapping;
        private Dictionary<string, SuffixedCluster> clusterNameMapping;
        private Dictionary<string, string> planetNameMapping;
        private List<char> scottishVowelOrdering;
        private List<string> VanillaSuffixedClusterNames;

        private void RandomizeAINames(ME1Package pacakge, Random random)
        {
            bool forcedCharge = random.Next(10) == 0;
            for (int i = 0; i < pacakge.NameCount; i++)
            {
                NameReference n = pacakge.getNameEntry(i);

                //Todo: Test Saren Hopper AI. Might be interesting to force him to change types.
                if (aiTypes.Contains(n.Name))
                {
                    string newAiType = forcedCharge ? "BioAI_Charge" : aiTypes[random.Next(aiTypes.Length)];
                    Log.Information("Reassigning AI type in " + Path.GetFileName(pacakge.FileName) + ", " + n + " -> " + newAiType);
                    pacakge.replaceName(i, newAiType);
                    pacakge.ShouldSave = true;
                }
            }

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
            int[] colsToRandomize = { 0 }; //sound name
            List<byte[]> names = new List<byte[]>();

            if (requiredprefix != "music")
            {

                for (int row = 0; row < guisounds2da.RowNames.Count(); row++)
                {
                    if (guisounds2da[row, 0] != null && guisounds2da[row, 0].Type == Bio2DACell.Bio2DADataType.TYPE_NAME)
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
                if (guisounds2da[row, 0] != null && guisounds2da[row, 0].Type == Bio2DACell.Bio2DADataType.TYPE_NAME)
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

        public class SuffixedCluster
        {
            public string ClusterName;
            /// <summary>
            /// string ends with "cluster"
            /// </summary>
            public bool SuffixedWithCluster;
            /// <summary>
            /// string is suffixed with cluster-style word. Doesn't need cluster appended.
            /// </summary>
            public bool Suffixed;

            public SuffixedCluster(string clusterName, bool suffixed)
            {
                this.ClusterName = clusterName;
                this.Suffixed = suffixed;
                this.SuffixedWithCluster = clusterName.EndsWith("cluster", StringComparison.InvariantCultureIgnoreCase);
            }

            public override string ToString()
            {
                return $"SuffixedCluster ({ClusterName})";
            }
        }
    }
}