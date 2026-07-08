using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.UnrealTypes;

namespace AutoMog
{
    internal class Program
    {

        struct ModDir
        {
            public string fileName;
            public string filePath;
            public string assetName;
            public string assetPath;
            public byte[] hash;
            public List<ModDir> referencedAssets;
        }

        // Copy Pasted DirSearch from stackoverflow and adapted to return all filepaths
        static List<String> DirSearch_ex3(string sDir, List<String> files)
        {
            try
            {
                //Console.WriteLine(sDir);

                foreach (string f in Directory.GetFiles(sDir))
                {

                    if (Path.GetExtension(f) == ".uasset" || Path.GetExtension(f) == ".umap")
                    {
                        files.Add(f);
                    }
                }

                foreach (string d in Directory.GetDirectories(sDir))
                {
                    DirSearch_ex3(d, files);
                }
            }
            catch (System.Exception excpt)
            {
                Console.WriteLine(excpt.Message);
            }
            return files;
        }
        public static void ClearCurrentConsoleLine()
        {
            int currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, currentLineCursor);
        }

        // Main menu, nothing too complex here
        int MogMenu()
        {
            int Choice = 0;
            Console.WriteLine("Menu:");
            Console.WriteLine("1- SQEXSEADMusic");
            Console.WriteLine("2- SQEXSEADSound");
            Console.Write("Choice: ");
            if (int.TryParse(Console.ReadLine(), out Choice))
                return Choice;
            else
                return 0;
        }

        // Uses UAssetAPI to generate a new .uasset/.uexp file with path and filename replaced
        UAsset CreateNewSQEXSEADUasset(string currentFile, string filepath, UAsset SQEXAudio)
        {

            // Set the Uasset based on the user choices 
            string filename = Regex.Replace(Path.GetFileNameWithoutExtension(currentFile), @"\s+", "");
            string npath = filename + ".uasset";
            WaveFileReader currentWav = new WaveFileReader(currentFile);

            // Automatically change the uasset file's duration property based on the current .wav file duration
            NormalExport durationExport = (NormalExport)SQEXAudio.Exports[0];
            FloatPropertyData durationFloat = (FloatPropertyData)durationExport["Duration"];
            //float.TryParse(currentWav.TotalTime.TotalSeconds.ToString(), result: out durationFloat.Value);


            // Change the parameters of the SQEX file depending on what type the user chose
            if (Path.GetFileNameWithoutExtension(SQEXAudio.FilePath) == "SQEXSEADMusic")
            {
                SQEXAudio.SetNameReference(0, FString.FromString(filepath + filename));
                SQEXAudio.SetNameReference(3, FString.FromString(filename));
            }
            else
            {
                SQEXAudio.SetNameReference(1, FString.FromString(filepath + filename));
                SQEXAudio.SetNameReference(8, FString.FromString(filename));
            }

            // Make new SQEX audio file and replace the reference to the new one
            SQEXAudio.Write(npath);
            SQEXAudio = new UAsset(npath, EngineVersion.VER_UE4_17);

            return SQEXAudio;
        }

        // Uses AudioMog to replace the audio file on the newly generated file       
        void AudioMogProc(string currentFile, string ProjectPath, UAsset SQEXAudio)
        {
            string filename = Regex.Replace(Path.GetFileNameWithoutExtension(currentFile), @"\s+", "");

            // AudioMog Process settings

            string audiomogpath = Directory.GetCurrentDirectory() + "/Templates/AudioMog.exe";
            ProcessStartInfo mogProc = new ProcessStartInfo(audiomogpath, SQEXAudio.FilePath.Replace("uasset", "uexp"));
            mogProc.UseShellExecute = false;
            mogProc.RedirectStandardOutput = true;

            // AudioMog the newly created uasset/uexp file
            Process proce = Process.Start(mogProc);
            Thread.Sleep(500);

            // Wait for Directory to be created
            while (!Directory.Exists(ProjectPath))
            {
                Thread.Sleep(500);
                Console.WriteLine(".");
            }

            proce.CloseMainWindow();
            proce.Close();

            // AudioMog rebuild uasset/uexp with given .wav

            File.Copy(currentFile, ProjectPath + filename + "_000.wav", true);

            Thread.Sleep(500);

            ProcessStartInfo rebuild = new ProcessStartInfo(audiomogpath, ProjectPath + "RebuildSettings.json");
            rebuild.UseShellExecute = false;
            rebuild.RedirectStandardOutput = true;

            Process rebuilde = Process.Start(rebuild);
            Thread.Sleep(500);

            // Wait for File to be created
            while (!File.Exists(ProjectPath + filename + ".uasset"))
            {
                Thread.Sleep(500);
                Console.Write(".");
            }

            rebuilde.CloseMainWindow();
            rebuilde.Close();
        }

        // Cleans any leftover temp files
        void CleanMog(string filepath, string ProjectPath, string filename, string currentDir)
        {
            //Move the new cooked assets into the output folder
            if (!Directory.Exists("Output"))
                Directory.CreateDirectory("Output");
            string outputDir = Regex.Replace(filepath, "^/game", "/Content", RegexOptions.IgnoreCase);

            Directory.CreateDirectory("Output" + outputDir);

            File.Copy(ProjectPath + filename + ".uasset", currentDir + "/Output" + outputDir + filename + ".uasset", true);
            File.Copy(ProjectPath + filename + ".uexp", currentDir + "/Output" + outputDir + filename + ".uexp", true);

            File.Delete(currentDir + "/" + filename + ".uasset");
            File.Delete(currentDir + "/" + filename + ".uexp");
            Directory.Delete(ProjectPath, true);
        }

        // Creates, ready to use in engine, dummy references of each file generated
        void DummyMog(bool isMusic, string currentDir, string filepath, string filename, UAsset DummyAudio)
        {
            // Set Dummies Templates
            if (isMusic)
            {
                DummyAudio = new UAsset(currentDir + "/Templates/MusicDummy.uasset", EngineVersion.VER_UE4_17);
                DummyAudio.SetNameReference(0, FString.FromString(filepath + filename));
                DummyAudio.SetNameReference(5, FString.FromString(filename));
            }
            else
            {
                DummyAudio = new UAsset(currentDir + "/Templates/SoundDummy.uasset", EngineVersion.VER_UE4_17);
                DummyAudio.SetNameReference(0, FString.FromString(filepath + filename));
                DummyAudio.SetNameReference(7, FString.FromString(filename));
            }


            if (!Directory.Exists("Dummy"))
                Directory.CreateDirectory("Dummy");


            //Dummy Audio
            string dummyDir = "Dummy" + Regex.Replace(filepath, "^/game", "/Content", RegexOptions.IgnoreCase);
            Directory.CreateDirectory(dummyDir);
            DummyAudio.Write(dummyDir + filename + ".uasset");
        }
        int FindPath(List<ModDir> modFiles, List<String> batchFiles, int curIndex)
        {

            UAsset Asset = new UAsset();
            // Set Dummies Templates
            int index = 0;
            int itemIndex = 0;
            int fileIndex = -1;
            string tempParsedPath = "";

            ClearCurrentConsoleLine();
            Console.Write("\rMain Asset: " + modFiles[curIndex].filePath + "");
            if (batchFiles.FindAll(x => ParsePath(x) == modFiles[curIndex].filePath).Count >= 1)
            {
                SHA256 mySHA256 = SHA256.Create();
                FileStream filestream = new FileStream(batchFiles.Find(x => ParsePath(x) == modFiles[curIndex].filePath), FileMode.Open);
                if (modFiles[curIndex].hash.SequenceEqual(mySHA256.ComputeHash(filestream)))
                {
                    ClearCurrentConsoleLine();
                    Console.Write("\rMain Asset: " + modFiles[curIndex].filePath + " FOUND!");
                    fileIndex = batchFiles.IndexOf(batchFiles.Find(x => ParsePath(x) == modFiles[curIndex].filePath));    
                }
                filestream.Close();

            }
            else
            {
                foreach (string filee in batchFiles)
                {
                    SHA256 mySHA256 = SHA256.Create();
                    FileStream filestream = new FileStream(filee, FileMode.Open);

                    if (modFiles[curIndex].hash.SequenceEqual(mySHA256.ComputeHash(filestream)))
                    {
                        ClearCurrentConsoleLine();
                        Console.Write("\rMain Asset: " + modFiles[curIndex].filePath + " FOUND!");
                        fileIndex = batchFiles.IndexOf(filee);
                        filestream.Close();
                        break;
                    }
                    filestream.Close();
                }
            }
                
            //Console.WriteLine("fileIndex: " + fileIndex);

            if (fileIndex == -1)
                return -1;
            
           
            //Asset = new UAsset(batchFiles.ElementAt(fileIndex), EngineVersion.VER_UE4_17);

            ReplacePath(batchFiles.ElementAt(fileIndex), modFiles, batchFiles, curIndex, fileIndex);
            return 0;
        }
        void ReplacePath(string filePath, List<ModDir> modFiles, List<String> batchFiles, int modFilesIndex, int curIndex)
        {

            UAsset Asset = new UAsset();

            // Set Dummies Templates
            int index = 0;
            int itemIndex = 0;
            int fileIndex = -1;
            int cont = 0;
            string tempParsedPath = "";
            string parsedPath = "";
            Asset = new UAsset(filePath, EngineVersion.VER_UE4_17);
            List<String> NameMaps = new List<string>();
            List<int> indexes = new List<int>();
            bool found = false;
            bool refFound = false;

            ClearCurrentConsoleLine();
            Console.Write("\rMain Asset: " + modFiles[modFilesIndex].filePath + "");

            if (batchFiles.FindAll(x => ParsePath(x) == modFiles[modFilesIndex].filePath).Count >= 1)
            {
                found = true;
            }
            foreach (ModDir modref in modFiles[modFilesIndex].referencedAssets)
            {
                if (batchFiles.FindAll(x => ParsePath(x) == modref.filePath).Count >= 1)
                {
                    cont++;
                }
            }

            // IF NOT(NOT found OR cont equals 0) then return (aka if all paths match json file then there's no need to continue)
            if (!(!found || cont == 0)) return;

            foreach (FString RefAssetPath in Asset.GetNameMapIndexList())
            {
                // Referenced Asset never had any equivalent so we skip
                if (batchFiles.FindAll(x => ParsePath(x) == RefAssetPath.ToString()).Count >= 1) continue;

                if (RefAssetPath.ToString().Contains("/") && !RefAssetPath.ToString().Contains("/Script/"))
                {
                    ClearCurrentConsoleLine();
                    Console.Write("\rMain Asset: " + modFiles[modFilesIndex].filePath + " Current RefAsset: " + RefAssetPath.ToString());
                    // Main Asset Check
                    if (String.Equals(modFiles[modFilesIndex].fileName, Path.GetFileNameWithoutExtension(RefAssetPath.ToString()), StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        itemIndex = index;
                        if (Math.Abs(String.Compare(modFiles[modFilesIndex].filePath, ParsePath(batchFiles.ElementAt(curIndex)), StringComparison.OrdinalIgnoreCase)) != 0)
                        {
                            Console.BackgroundColor = ConsoleColor.Magenta;
                            Console.WriteLine("\r" + modFiles[modFilesIndex].filePath + " != " + ParsePath(batchFiles.ElementAt(curIndex)));
                            Console.ResetColor();
                            parsedPath = "/Game" + Regex.Replace(batchFiles.ElementAt(curIndex).Split("\\Content")[1], "\\u005C", "/");
                            parsedPath = Regex.Replace(parsedPath, "\\.uasset|\\.umap", "");
                            Console.Write("\rMain Asset: " + modFiles[modFilesIndex].filePath + " Current RefAsset: " + RefAssetPath.ToString() + " are the same.");
                        }

                    }
                    // Referenced Assets Check, if no counterpart is found then completely skip to the next one
                    else
                    {
                        foreach (string filee in batchFiles)
                        {
                            // Referenced Asset never had any equivalent so we skip
                            if (modFiles.FindAll(x => x.filePath == RefAssetPath.ToString()).Count <= 0) break;

                            refFound = false;
                            ClearCurrentConsoleLine();
                            Console.Write("\rMain Asset: " + modFiles[modFilesIndex].filePath + " Current RefAsset: " + RefAssetPath.ToString() + " checking against: " + ParsePath(filee));

                            // Find the equivalent asset that's referenced in the main one, and compare its hashes with the cached one
                            foreach (ModDir mod in modFiles.FindAll(x => x.fileName == Path.GetFileNameWithoutExtension(RefAssetPath.ToString())))
                            {
                                if (!String.Equals(mod.filePath, RefAssetPath.ToString(), StringComparison.OrdinalIgnoreCase)) continue;
                                    

                                SHA256 newSHA256 = SHA256.Create();
                                FileStream newfilestream = new FileStream(filee, FileMode.Open);
                                if (mod.hash.SequenceEqual(newSHA256.ComputeHash(newfilestream)))
                                {
                                    //Console.WriteLine("asdfwsgewrfgds: " + mod.filePath + " AAND " + ParsePath(filee));
                                    fileIndex = batchFiles.IndexOf(filee);
                                    newfilestream.Close();
                                    refFound = true;
                                    ClearCurrentConsoleLine();
                                    Console.WriteLine("\rMain Asset: " + modFiles[modFilesIndex].filePath + " Current RefAsset: " + RefAssetPath.ToString() + " checking against: " + ParsePath(filee) + " FOUND!");
                                    break;
                                }
                                newfilestream.Close();

                            }

                            if (!refFound) continue;

                            // Parse filepath into a string that UE expects and then check if there are any changes to it
                            tempParsedPath = "/Game" + Regex.Replace(filee.Split("\\Content")[1], "\\u005C", "/");
                            tempParsedPath = Regex.Replace(tempParsedPath, "\\.uasset|\\.umap", "");
                            
                            if (Math.Abs(String.Compare(tempParsedPath, Asset.GetNameReference(index).ToString(), StringComparison.OrdinalIgnoreCase)) != 0)
                            {
                                Console.BackgroundColor = ConsoleColor.Red;
                                ClearCurrentConsoleLine();
                                Console.WriteLine("\rMain Asset: " + modFiles[modFilesIndex].filePath + " RefAsset: " + Asset.GetNameReference(index).ToString() + " != " + tempParsedPath);
                                Console.ResetColor();
                                NameMaps.Add(tempParsedPath);
                                indexes.Add(index);
                            }
                            continue;

                            
                        }
                    }
                }
                index++;
            }

            // Write into file all the changes found if any
            bool hasAnyChangesBeenMade = false;
            int i = 0;
            foreach (string filee in NameMaps)
            {
                ClearCurrentConsoleLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\r" + Asset.GetNameReference(indexes[i]).ToString() + "->" + filee);
                Console.ResetColor();
                Asset.SetNameReference(indexes[i], FString.FromString(filee));
                hasAnyChangesBeenMade = true;
                i++;
            }
            if (!String.IsNullOrEmpty(parsedPath))
            {
                ClearCurrentConsoleLine();
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("\r" + Asset.GetNameReference(itemIndex).ToString() + "->" + parsedPath);
                Console.ResetColor();
                Asset.SetNameReference(itemIndex, FString.FromString(parsedPath));
                hasAnyChangesBeenMade = true;
            }

            if (hasAnyChangesBeenMade) Asset.Write(filePath);

        }

        string ParsePath(string path, bool keepExtension = false)
        {
            string internalParsedPath = "";
            internalParsedPath = "/Game" + Regex.Replace(path.Split("\\Content")[1], "\\u005C", "/");
            if (!keepExtension) internalParsedPath = Regex.Replace(internalParsedPath, "\\.uasset|\\.umap", "");
            return internalParsedPath;
        }
        void JsonBuilder (List<String> batchFiles, string jsonName, Program program)
        {
            string currentFile = "";
            string filepath = ""; 
            FileStream filestream;
            SHA256 mySHA256 = SHA256.Create();
            ModDir curAss = new ModDir();
            List<ModDir> AllAssets = new List<ModDir>();
            UAsset Asset = new UAsset(currentFile, EngineVersion.VER_UE4_17);

            foreach (string file in batchFiles)
            {
                currentFile = file;

                // IF user's root folder for batch thing is Content then remove content from path and also remove the filename, that's a mouthful yikes
                filepath = "/Game" + Regex.Replace(currentFile.Split("\\Content")[1], "\\u005C", "/");//Regex.Replace(Regex.Replace(Path.GetDirectoryName(currentFile.TrimStart(Directory.GetCurrentDirectory().ToCharArray())), "^\\e/content", "", RegexOptions.IgnoreCase), @"\\", "/") + "/";
                filepath = Regex.Replace(filepath, "\\.uasset|\\.umap", "");

                // Construct current Asset and then add it to the AllAssets list
                filestream = new FileStream(currentFile, FileMode.Open);
                
                filestream.Position = 0;
                curAss.filePath = filepath;
                curAss.fileName = Path.GetFileNameWithoutExtension(currentFile);
                curAss.assetPath = GetAssetPathName(currentFile,1);
                curAss.assetName = GetAssetPathName(currentFile,0);
                curAss.hash = mySHA256.ComputeHash(filestream);
                filestream.Close();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(currentFile);
                Console.ResetColor();
                curAss.referencedAssets = program.RefAssetWalker(currentFile, filepath, batchFiles, batchFiles.IndexOf(file));

                AllAssets.Add(curAss);
            }


            JsonSerializer serializer = new JsonSerializer();
            serializer.Converters.Add(new JavaScriptDateTimeConverter());
            serializer.NullValueHandling = NullValueHandling.Ignore;

            serializer.Formatting = Formatting.Indented;

            using (StreamWriter sw = new StreamWriter(jsonName))
            {
                sw.WriteLine(JsonConvert.SerializeObject(AllAssets, Formatting.Indented));
            }
        }

        string GetAssetPathName(string currentFile, int choice)
        {
            UAsset Asset = new UAsset();
            int count = 0;
            string AssetPathName = null;
            switch (choice)
            {
                case 0:

                            break;
                
                case 1:
                    Asset = new UAsset(currentFile, EngineVersion.VER_UE4_17);

                    break;
            }
            return "";
        }
        
        List<ModDir> RefAssetWalker(string currentFile, string parsedPath, List<String> batchFiles, int curIndex)
        {

            UAsset Asset = new UAsset();

            // Set Dummies Templates
            int index = 0;
            int itemIndex = 0;
            int fileIndex = -1;
            string tempParsedPath = "";
            string internalParsedPath = "";
            Asset = new UAsset(currentFile, EngineVersion.VER_UE4_17);
            List<String> NameMaps = new List<string>();
            List<int> indexes = new List<int>();
            bool found = false;
            bool refFound = false;
            ModDir tempReferencedAsset = new ModDir();
            List <ModDir> referencedAssets = new List<ModDir>();

            foreach (FString RefAssetPath in Asset.GetNameMapIndexList())
            {
                if (RefAssetPath.ToString().Contains("/") && !RefAssetPath.ToString().Contains("/Script/"))
                {
                    // Main Asset Check
                    if (String.Equals(parsedPath, RefAssetPath.ToString(), StringComparison.OrdinalIgnoreCase))
                    {

                        found = true;
                        itemIndex = index;
                        internalParsedPath = ParsePath(batchFiles.ElementAt(curIndex));
                    }
                    // Referenced Assets Check, if no counterpart is found then completely skip to the next one
                    else
                    {
                        if (batchFiles.FindAll(x => ParsePath(x) == RefAssetPath.ToString()).Count <= 0) continue;

                        // Find the equivalent asset that's referenced in the main one, and store its hashes

                        string path = batchFiles.Find(x => ParsePath(x) == RefAssetPath.ToString());
                        FileStream filestream = new FileStream(path, FileMode.Open);
                        SHA256 mySHA256 = SHA256.Create();
                        
                        //Console.WriteLine(parsePath(path));

                        filestream.Position = 0;
                        tempReferencedAsset.filePath = ParsePath(path);
                        tempReferencedAsset.fileName = Path.GetFileNameWithoutExtension(ParsePath(path));
                        tempReferencedAsset.hash = mySHA256.ComputeHash(filestream);
                        filestream.Close();

                        referencedAssets.Add(tempReferencedAsset);

                    }
                }
                index++;
            }
            // Return
            return referencedAssets;

        }

        static void Main(string[] args)
        {
            // Get .wav if not .wav close (in batch case skip over non .wav files)
            // Menu Select (SQEXSEADMusic, SQEXSEADSound)

            // void CreateNewSQEXSEADUasset()
            // Edit Uasset Path/Filename
            // Create new Uasset

            // void AudioMogProc()
            // Audiomog its new .uexp and move project folder to Temp folder
            // Replace the .wav in the project folder with new one and then audiomog it

            // void CleanMog()
            // Move newly made Uasset/Uexp to Output folders
            // Delete Project folder


            // In case of Batch go to step 3

            // Check for any file provided and if so proceed with initializing variables
            if (args.Length == 0)
            {
                Console.WriteLine("No file or folder provided. Exiting.");
                Environment.Exit(1);
            }
            Program program = new Program();

            UAsset SQEXTemplateAudio = new UAsset();
            UAsset SQEXAudio = new UAsset();
            UAsset DummyAudio = new UAsset();

            string currentDir = Directory.GetCurrentDirectory();
            string currentFile = "";
            string filepath = "";

            int tf = 137;
            int tindex = 0;
            int findex = 0;
            List<int> iary = new List<int>();

            List<String> batchFiles = new List<String>();
            FileAttributes attr = File.GetAttributes(args[0]);
            bool batchFileMode = false;

            //Detect whether its a directory or file
            if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                batchFileMode = true;
            else
            {
                currentFile = args[0];
                if (Path.GetExtension(currentFile) != ".uasset")
                {
                    Environment.Exit(1);
                }
            }

            bool isOnMenu = true;
            bool isMusic = false;
            //Menu

            /*
            while (isOnMenu)
            {
                switch (program.MogMenu())
                {
                    case 1:
                        SQEXTemplateAudio = new UAsset(currentDir + "/Templates/SQEXSEADMusic.uasset", EngineVersion.VER_UE4_17);
                        isOnMenu = false;
                        isMusic = true;
                        break;
                    case 2:
                        SQEXTemplateAudio = new UAsset(currentDir + "/Templates/SQEXSEADSound.uasset", EngineVersion.VER_UE4_17);
                        isOnMenu = false;
                        isMusic = false;
                        break;
                    default: Console.WriteLine("Choice isn't in the menu"); Thread.Sleep(500); break;

                }
                Console.Clear();
            }
            */

            // IF args include a singular file
            if (!batchFileMode)
            {
                Environment.Exit(1);

            }
            else
            {
                // IF args includes a Folder
                batchFiles = DirSearch_ex3(args[0], batchFiles);
                int index = 0;
                Console.WriteLine(args[0].Split("\\")[args[0].Split("\\").Length - 1] + ".json");
                string jsonName = args[0].Split("\\")[args[0].Split("\\").Length - 1] + ".json";
                if (!File.Exists(jsonName))
                {
                    program.JsonBuilder(batchFiles, jsonName, program);
                }
                else
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Converters.Add(new JavaScriptDateTimeConverter());
                    serializer.NullValueHandling = NullValueHandling.Ignore;

                    serializer.Formatting = Formatting.Indented;

                    List<ModDir> deserializedProduct = new List<ModDir>();

                    using (StreamReader sw = new StreamReader(jsonName))
                    {
                        sw.BaseStream.Seek(0, SeekOrigin.Begin);
                        deserializedProduct = JsonConvert.DeserializeObject<List<ModDir>>(sw.ReadToEnd());
                    }
                    for (int i = 0; i < deserializedProduct.Count; i++)
                        program.FindPath(deserializedProduct, batchFiles, i);

                    program.JsonBuilder(batchFiles, jsonName, program);
                }




                if (findex == 0)
                {
                    Console.WriteLine("Parsing complete, no uasset need to be renamed.");
                }
                else
                {
                    Console.WriteLine("Parsing complete, found " + findex + " out of " + (tindex + findex) + " that need to be replaced.");
                    Console.WriteLine("Start the Automatic Renaming Process? Y/N");
                }

                Console.Read();

                //foreach (int i in iary)
                //{
                    //filepath = "/Game" + Regex.Replace(batchFiles[i].Split("\\Content")[1], "\\u005C", "/");//Regex.Replace(Regex.Replace(Path.GetDirectoryName(currentFile.TrimStart(Directory.GetCurrentDirectory().ToCharArray())), "^\\e/content", "", RegexOptions.IgnoreCase), @"\\", "/") + "/";
                    //program.ReplacePath(batchFiles[i], Regex.Replace(filepath, "\\.uasset|\\.umap", ""), Path.GetFileNameWithoutExtension(batchFiles[i]), batchFiles);
                //}
                Console.Read();

            }

            //Exit Program
            //Console.Clear();
            Console.Beep();
            Console.WriteLine("Process complete! \nYou may close the program now.");
            Console.Read();
            //Environment.Exit(0);
        }
    }
}
