using AvaloniaToolbox.Core.IO;
using NextLevelLibrary;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.PortableExecutable;

namespace NlgFileTool
{
    internal sealed class Program
    {
        static GameVersion Version;

        [STAThread]
        public static void Main(string[] args)
        {
            if (File.Exists("Strings.txt"))
            {
                using (var reader = new StreamReader("Strings.txt"))
                {
                    while (!reader.EndOfStream)
                    {
                        string str = reader.ReadLine();
                        Hashing.AddString(str);
                    }
                }
            }

            string exeFilePath = Assembly.GetExecutingAssembly().Location;
            string exeFolderPath = Path.GetDirectoryName(exeFilePath);

            string exportFolder = Path.Combine(exeFolderPath, "Exported");
            foreach (var arg in args)
            {
                if (arg.Contains(".dict"))
                {
                    var data = LoadDict(arg);
                    if (data != null)
                        DumpDict(data, Path.Combine(exportFolder, Path.GetFileNameWithoutExtension(arg)));
                }
            }
        }

        static void DumpDict(IDataFile dataFile, string exportFolder)
        {
            foreach (ChunkFileEntry f in dataFile.Tables[0].GetFiles)
            {
                if (f.Type == ChunkType.Texture)
                    Hashing.TextureHashCache.Add(f.FilePath.Value);
            }

            // Loose data
            foreach (ChunkEntry f in dataFile.Tables[0].GetLooseData)
            {
                string exportSubFolder = Path.Combine(exportFolder, "Loose");
                Directory.CreateDirectory(exportSubFolder);
                string exportPath = Path.Combine(exportSubFolder, $"{f.Type.ToString()}.nlg");
                f.Export(exportPath);
            }
            // File data
            foreach (ChunkFileEntry f in dataFile.Tables[0].GetFiles)
            {
                string exportSubFolder = Path.Combine(exportFolder, f.HashType.String);
                Directory.CreateDirectory(exportSubFolder);
                string exportPath = Path.Combine(exportSubFolder, Path.GetFileName(f.FilePath.String) + ".nlg");
                f.Export(exportPath);

                if (Version == GameVersion.MetroidFed)
                    MetroidFedForceExporter.Export(dataFile, f, exportSubFolder, exportFolder);
            }
        }



        static IDataFile LoadDict(string filePath)
        {
            var stream = File.OpenRead(filePath);
            Version = FindVersion(stream);
            stream.Position = 0;

            IDictionaryData dictFile;
            IDataFile dataFile;

            switch (Version)
            {
                case GameVersion.LM2:
                case GameVersion.LM2HD:
                    dictFile = new NextLevelLibrary.LM2.DictionaryFile(stream);
                    break;
                case GameVersion.LM3:
                case GameVersion.MetroidFed:
                    dictFile = new NextLevelLibrary.LM3.DictionaryFile(stream);
                    break;
                case GameVersion.StrikersBLF:
                    dictFile = new NextLevelLibrary.StrikersBLF.DictionaryFile(stream);
                    break;
                default:
                    throw new Exception($"Game not supported! {Version}");
            }

            dictFile.FilePath = filePath;

            //Parse seperate data file
            string dataPath = filePath.Replace(".dict", ".data");
            if (!File.Exists(dataPath))
                return null;

            switch (Version)
            {
                case GameVersion.LM2:
                case GameVersion.LM2HD:
                    dataFile = new NextLevelLibrary.LM2.DataFile((NextLevelLibrary.LM2.DictionaryFile)dictFile);
                    break;
                case GameVersion.LM3:
                case GameVersion.MetroidFed:
                    dataFile = new NextLevelLibrary.LM3.DataFile((NextLevelLibrary.LM3.DictionaryFile)dictFile);
                    break;
                case GameVersion.StrikersBLF:
                    dataFile = new NextLevelLibrary.StrikersBLF.DataFile(File.OpenRead(dataPath),
                                (NextLevelLibrary.StrikersBLF.DictionaryFile)dictFile);
                    break;
                default:
                    throw new Exception($"Game not supported! {Version}");
            }
            return dataFile;
        }

        static GameVersion FindVersion(Stream stream)
        {
            using (var reader = new FileReader(stream, true))
            {
                reader.SetByteOrder(true);
                reader.SeekBegin(12);
                // Starting hash
                if (reader.ReadUInt32() == 0x78340300)
                    return GameVersion.LM3;

                reader.SeekBegin(16);
                if (reader.ReadUInt32() == 0x297B947A)
                    return GameVersion.MetroidFed;

                if (reader.BaseStream.Length > 0x44)
                {
                    reader.SetByteOrder(false);
                    reader.SeekBegin(0x40);
                    if (reader.ReadUInt32() == 4247762216)
                        return GameVersion.StrikersBLF;
                }
                //LM2HD is nearly identical to LM2 but has 1 less block used
                reader.SeekBegin(8);
                if (reader.ReadByte() % 7 == 0)
                    return GameVersion.LM2HD;
            }
            return GameVersion.LM2;
        }
    }
}