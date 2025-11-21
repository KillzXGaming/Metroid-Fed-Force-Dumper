using AvaloniaToolbox.Core.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Toolbox.Core;

namespace NextLevelLibrary.LM3
{
    public class DictionaryFile : IDictionaryData
    {
        /// <summary>
        /// Data referred to the file table.
        /// This determines what blocks to use for the table.
        /// </summary>
        public class FileTableReference
        {
            //Standard or debug
            public HashString Name;
            //total amount of 8 byte sections for file data
            public ushort FileSectionCount;
            //total file count not including file identifier chunk
            public ushort FileCount;
            //The blocks to index to grab data from the file table
            public byte[] BlockIndices = new byte[16] {
                52, 53, 54, 55, 
                68, 69, 70, 01,
                65, 0, 0, 0,
                0, 0, 0, 0};
        }

        public IEnumerable<Block> BlockList => Blocks;
        public bool BlocksCompressed => IsCompressed;

        //List of file table references
        public List<FileTableReference> FileTableReferences = new List<FileTableReference>();
        //List of blocks to get blobs of raw data
        public List<Block> Blocks = new List<Block>();
        //List of strings used for external file extensions
        public List<string> StringList = new List<string>();

        public ushort HeaderFlags;
        public bool IsCompressed = false;

        public GameVersion Version { get; set; } = GameVersion.LM3;

        public string FilePath { get; set; }

        public DictionaryFile()
        {

        }

        public DictionaryFile(Stream stream)
        {
            Version = GetVersion(stream);

            using (var reader = new FileReader(stream))
            {
                reader.Position = 0;
                uint Identifier = reader.ReadUInt32();

                HeaderFlags = reader.ReadUInt16();
                IsCompressed = reader.ReadByte() == 1;
                reader.ReadByte(); //Padding
                uint SizeLargestFile = reader.ReadUInt32();
                byte numFiles = reader.ReadByte();
                byte numFileTableReferences = reader.ReadByte();
                byte numStrings = reader.ReadByte();
                reader.ReadByte(); //Padding
                for (int i = 0; i < numFileTableReferences; i++)
                {
                    FileTableReferences.Add(new FileTableReference()
                    {
                        //hash name of the table
                        //always region name + hw for LM3
                        Name = new HashString(reader.ReadUInt32()),
                        //total amount of 8 byte sections for file data
                        FileSectionCount = reader.ReadUInt16(),
                        //total file count not including file identifier chunk
                        FileCount = reader.ReadUInt16(),
                        BlockIndices = reader.ReadBytes(this.Version == GameVersion.MetroidFed ? 8 : 16),
                    });
                }

                for (int i = 0; i < numFiles; i++)
                {
                    Blocks.Add(new Block(this, i)
                    {
                        Offset = reader.ReadUInt32(),
                        DecompressedSize = reader.ReadUInt32(),
                        CompressedSize = reader.ReadUInt32(),
                        Flags = reader.ReadUInt32(),
                    });
                    uint resourceIndex = Blocks[i].Flags >> 16 & 0xFF;

                    //The source index determines which external file to use
                    Blocks[i].SourceIndex = (byte)resourceIndex;
                }
                for (int i = 0; i < numStrings; i++)
                    StringList.Add(reader.ReadStringZeroTerminated());

                for (int i = 0; i < numFiles; i++)
                    Blocks[i].FileExtension = StringList[Blocks[i].SourceIndex];
            }
        }

        private GameVersion GetVersion(Stream stream)
        {
            using (var reader = new FileReader(stream, true))
            {
                reader.SeekBegin(32);
                uint region_hash = reader.ReadUInt32();
                if (region_hash == 83197030)
                    return GameVersion.MetroidFed;
            }
            return GameVersion.LM3;
        }

        public void Save(Stream stream)
        {
            using (var writer = new FileWriter(stream))
            {
                writer.SetByteOrder(false);
                writer.Write(0xA9F32458);
                writer.Write(HeaderFlags);
                writer.Write(IsCompressed);
                writer.Write((byte)0); //padding
                long maxValuePos = writer.Position;
                if (IsCompressed)
                    writer.Write(Blocks.Max(x => x.CompressedSize));
                else
                    writer.Write((uint)0);
                writer.Write((byte)Blocks.Count);
                writer.Write((byte)FileTableReferences.Count);
                writer.Write((byte)StringList.Count);
                writer.Write((byte)0);
                foreach (var info in FileTableReferences)
                {
                    writer.Write(info.Name.Value);
                    writer.Write(info.FileSectionCount);
                    writer.Write(info.FileCount);
                    writer.Write(info.BlockIndices);
                }
                for (int i = 0; i < Blocks.Count; i++)
                {
                    writer.Write(Blocks[i].Offset);
                    writer.Write(Blocks[i].DecompressedSize);
                    writer.Write(IsCompressed ? Blocks[i].CompressedSize : 0);
                    writer.Write(Blocks[i].Flags);
                }
                foreach (var str in StringList)
                    writer.WriteZeroTerminatedString(str);
            }
        }

        public static DataFile ReadDictionaryData(string filePath)
        {
            var dictionary = new DictionaryFile(File.OpenRead(filePath));
            dictionary.FilePath = filePath;
            return new DataFile(dictionary);
        }
    }
}