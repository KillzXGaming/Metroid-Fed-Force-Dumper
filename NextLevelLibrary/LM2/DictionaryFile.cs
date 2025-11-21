using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Toolbox.Core;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using AvaloniaToolbox.Core.IO;

namespace NextLevelLibrary.LM2
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
            //The blocks to index to grab data from the file table
            public byte[] BlockIndices = new byte[8] { 2, 3, 4, 5, 0, 0, 0, 0 };
        }

        public class FileTableInfo
        {
            public ushort NumFiles; //inside table
            public ushort TableBlockIndex; //block that contains the file table
        }

        public IEnumerable<Block> BlockList => Blocks;
        public bool BlocksCompressed => IsCompressed;

        //List of file table references
        public List<FileTableReference> FileTableReferences = new List<FileTableReference>();
        //List of blocks to get blobs of raw data
        public List<Block> Blocks = new List<Block>();
        //List of strings used for external file extensions
        public List<string> StringList = new List<string>();

        public List<FileTableInfo> FileTableInfos = new List<FileTableInfo>();
        public GameVersion Version { get; set; } = GameVersion.LM2;

        public ushort HeaderFlags;
        public bool IsCompressed = false;

        public string FilePath { get; set; }

        byte[] Unknowns { get; set; }

        public DictionaryFile(Stream stream)
        {
            using (var reader = new FileReader(stream))
            {
                uint Identifier = reader.ReadUInt32();
                HeaderFlags = reader.ReadUInt16();
                IsCompressed = reader.ReadByte() == 1;
                reader.ReadByte(); //Padding
                uint numBlocks = reader.ReadUInt32();
                uint SizeLargestBlock = reader.ReadUInt32();
                byte numFileTables = reader.ReadByte();
                reader.ReadByte(); //Padding
                byte numFileTableReferences = reader.ReadByte();
                byte numStrings = reader.ReadByte();
                //Table references. For LM2 it is only 2 with "standard" and "debug" (unused)
                //These determine where data is located for each file
                for (int i = 0; i < numFileTableReferences; i++)
                    FileTableReferences.Add(new FileTableReference()
                    {
                        Name = new HashString(reader.ReadUInt32()),
                        BlockIndices = reader.ReadBytes(8),
                    });
                //File table info, 2 references each (standard, debug)
                for (int i = 0; i < numFileTables * numFileTableReferences; i++)
                    FileTableInfos.Add(new FileTableInfo()
                    {
                        NumFiles = reader.ReadUInt16(), //number of files in file table per 12 bytes (includes each individual identifier section)
                        TableBlockIndex = reader.ReadUInt16(), //the block the table is in
                    });
                for (int i = 0; i < numBlocks; i++)
                {
                    Blocks.Add(new Block(this, i)
                    {
                        Offset = reader.ReadUInt32(),
                        DecompressedSize = reader.ReadUInt32(),
                        CompressedSize = reader.ReadUInt32(),
                        Flags = reader.ReadUInt32(),
                    });

                    //Handle the flags
                    uint resourceIndex = Blocks[i].Flags >> 16 & 0xFF;

                    //The source index determines which external file to use
                    Blocks[i].SourceIndex = (byte)resourceIndex;
                }
                for (int i = 0; i < numStrings; i++)
                    StringList.Add(reader.ReadStringZeroTerminated());

                for (int i = 0; i < numBlocks; i++)
                    Blocks[i].FileExtension = StringList[Blocks[i].SourceIndex];
            }
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
                writer.Write(Blocks.Count);
                long maxValuePos = writer.Position;
                if (IsCompressed)
                    writer.Write(Blocks.Max(x => x.CompressedSize));
                else
                    writer.Write((uint)0);
                writer.Write((byte)1);
                writer.Write((byte)0);
                writer.Write((byte)FileTableReferences.Count);
                writer.Write((byte)StringList.Count);
                foreach (var info in FileTableReferences)
                {
                    writer.Write(info.Name.Value);
                    writer.Write(info.BlockIndices);
                }
                foreach (var info in this.FileTableInfos)
                {
                    writer.Write(info.NumFiles);
                    writer.Write(info.TableBlockIndex);
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