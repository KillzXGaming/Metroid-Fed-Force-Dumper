using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Toolbox.Core;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Xml.Serialization;
using System.Diagnostics.Metrics;
using ZstdSharp;
using AvaloniaToolbox.Core.IO;
using AvaloniaToolbox.Core.Compression;

namespace NextLevelLibrary.StrikersBLF
{
    public class DictionaryFile : IDictionaryData
    {
        public IEnumerable<Block> BlockList => Blocks;
        public bool BlocksCompressed => true;


        public List<Block> Blocks = new List<Block>();
        public List<string> StringList = new List<string>();

        public ushort HeaderFlags = 0x4040;

        public uint[] Hashes = new uint[8];

        public string FilePath { get; set; }
        public GameVersion Version { get; set; } = GameVersion.StrikersBLF;

        public ChunkTableBLF Table = new ChunkTableBLF();

        DictionaryFile() { }

        public DictionaryFile(Stream stream)
        {
            using (var reader = new FileReader(stream))
            {
                uint Identifier = reader.ReadUInt32();
                HeaderFlags = reader.ReadUInt16();
                byte numStrings = reader.ReadByte();
                reader.ReadByte(); //padding
                Hashes = reader.ReadUInt32s(8); //hashes

                foreach (var hash in Hashes)
                    Console.WriteLine($"Hash {Hashing.GetString(hash)}");

                uint compressionSize = reader.ReadUInt32();
                uint numFiles = reader.ReadUInt32();
                uint numDataEntires = reader.ReadUInt32();
                uint numTable4 = reader.ReadUInt32();
                reader.ReadUInt32();
                uint numBlocks = reader.ReadByte();
                reader.ReadBytes(3);

                //ZSTB buffer to decompress
                var buffer = DecompressBuffer(reader.ReadBytes((int)compressionSize));
                File.WriteAllBytes("data.bin", buffer);
                using (var r = new FileReader(buffer))
                {
                    ReadChunkTable(r, numStrings, numFiles, numDataEntires, numTable4, numBlocks);
                }
            }
        }

        private void ReadChunkTable(FileReader reader, uint numStrings, uint numFiles,
            uint numDataEntires, uint numTable4, uint numBlocks)
        {
            Console.WriteLine($"numBlocks {numBlocks}");
            Console.WriteLine($"numFiles {numFiles}");
            Console.WriteLine($"numChunks {numDataEntires}");
            Console.WriteLine($"numTable4 {numTable4}");

            for (int i = 0; i < numBlocks; i++)
            {
                uint Offset = reader.ReadUInt32();
                uint CompressedSize = reader.ReadUInt32();
                uint DecompressedSize = reader.ReadUInt32();
                //Table 4 start index and counter
                uint startIndex = reader.ReadUInt32();
                uint counter = reader.ReadUInt32();
                //0xFFFFFFFFFF flags. Maybe mask?
                reader.ReadUInt32();
                //Flags
                uint Flags = reader.ReadUInt32();

                Blocks.Add(new FileBlock(this, i)
                {
                    Offset = Offset,
                    DecompressedSize = DecompressedSize,
                    CompressedSize = CompressedSize,
                    Flags = Flags,
                    BeginIndex = startIndex,
                    Count = counter,
                    IsZSTDCompressed = true,
                    Dictionary = this,
                    SourceType = ResourceType.DATA,
                });

                //Handle the flags
                uint resourceIndex = Blocks[i].Flags >> 8 & 0xFF;
                //The source index determines which external file to use
                Blocks[i].SourceIndex = (byte)(resourceIndex - 1);

                Console.WriteLine($"startIndex {startIndex} count {counter} sourceIdx {Blocks[i].SourceIndex}");
            }

            Table = new ChunkTableBLF();

            Dictionary<int, ChunkEntry> globalChunkList = new Dictionary<int, ChunkEntry>();

            int globalIndex = 0;
            int max = 0;

            //Chunk data
            for (int i = 0; i < numFiles; i++)
            {
                ChunkFileEntry chunk = new ChunkFileEntry(this, Table);
                chunk.Type = (ChunkType)reader.ReadUInt16();
                chunk.Flags = reader.ReadUInt16();
                chunk.Size = reader.ReadUInt32();
                chunk.Offset = reader.ReadUInt32();
                Table.Files.Add(chunk);
                globalChunkList.Add(globalIndex++, chunk);

                max = Math.Max(max, BitUtils.GetBits(chunk.Flags, 8, 6));

                var blockIndex = BitUtils.GetBits(chunk.Flags, 8, 6);
                chunk.IsDebug = Blocks[blockIndex].SourceIndex != 0;

                uint flag = chunk.Flags;
               // if (!hasChildren(chunk))
               //     Console.WriteLine($"FILE {chunk.Type} {chunk.Offset} {chunk.Size} {chunk.Flags}");
            }

            uint numSubData = numDataEntires - numFiles;
            for (int i = 0; i < numSubData; i++)
            {
                ChunkEntry chunk = new ChunkEntry(this, Table);
                chunk.Type = (ChunkType)reader.ReadUInt16();
                chunk.Flags = reader.ReadUInt16();
                chunk.Size = reader.ReadUInt32();
                chunk.Offset = reader.ReadUInt32();
                Table.DataEntries.Add(chunk);
                globalChunkList.Add(globalIndex++, chunk);

                max = Math.Max(max, BitUtils.GetBits(chunk.Flags, 8, 6));

                var blockIndex = BitUtils.GetBits(chunk.Flags, 8, 6);
                chunk.IsDebug = Blocks[blockIndex].SourceIndex != 0;

                uint flag = chunk.Flags;
             //   if (!hasChildren(chunk))
              //      Console.WriteLine($"DATA {chunk.Type} {chunk.Offset} {chunk.Size} {chunk.Flags}");
            }

            Console.WriteLine($"max {max}");

            bool hasChildren(ChunkEntry chunk)
            {
                if (chunk.Type == ChunkType.MaterialVariation)
                    return true;

                return BitUtils.GetBit((int)chunk.Flags, 7);
            }

            //Connect children
            foreach (var file in Table.Files)
            {
                if (file.IsDebug)
                    continue;

                if (hasChildren(file))
                {
                    for (int f = 0; f < file.Size; f++)
                        file.AddChild(globalChunkList[(int)file.Offset + f]);
                }
            }
            foreach (var entry in Table.DataEntries)
            {
                if (entry.IsDebug)
                    continue;

                if (hasChildren(entry))
                {
                    for (int f = 0; f < entry.Size; f++)
                        entry.AddChild(globalChunkList[(int)entry.Offset + f]);
                }
            }

            //Chunk file headers
            for (int i = 0; i < numFiles; i++)
            {
                ((ChunkFileEntry)Table.Files[i]).HashType = new HashString(reader.ReadUInt32());
                ((ChunkFileEntry)Table.Files[i]).FilePath = new HashString(reader.ReadUInt32());
            }


            for (int i = 0; i < numTable4; i++)
            {
                var values = reader.ReadUInt16s(4);
                Console.WriteLine($"values {string.Join(',', values)}");
            }

            for (int i = 0; i < numStrings; i++)
                StringList.Add(reader.ReadStringZeroTerminated());

            for (int i = 0; i < numBlocks; i++)
            {
                Blocks[i].FileExtension = StringList[Blocks[i].SourceIndex];
            }
        }

        private byte[] DecompressBuffer(byte[] buffer) {
            return ZstdFormat.Decompress(buffer);
        }

        public void Save(Stream stream)
        {
            using (var writer = new FileWriter(stream))
            {
            }
        }

        public class FileBlock : Block
        {
            public uint BeginIndex;
            public uint Count;

            public FileBlock(IDictionaryData dict, int index) : base(dict, index)
            {
            }

            public override Stream Decompress(Stream compressed)
            {
                using (var reader = new FileReader(compressed, true))
                {
                    if (Offset > reader.BaseStream.Length || DecompressedSize == 0)
                        return new MemoryStream();

                    reader.SeekBegin(Offset);
                    if (CompressedSize == 0 || DecompressedSize == CompressedSize)
                        return new MemoryStream(reader.ReadBytes((int)DecompressedSize));

                    byte[] input = reader.ReadBytes((int)CompressedSize);
                    return new MemoryStream(ZSTDDecompress(input));
                }
            }
            private byte[] ZSTDDecompress(byte[] buffer)
            {
                var mem = new MemoryStream();
                using (var decompressor = new DecompressionStream(new MemoryStream(buffer))) {
                    decompressor.CopyTo(mem);
                    return mem.ToArray();
                }
            }
        }


        public class ChunkTableBLF : ChunkTable
        {
            public List<ChunkEntry> DataEntries { get; set; } = new List<ChunkEntry>();

            public void SaveData(List<Block> blocks)
            {
                throw new NotImplementedException();
            }
        }
    }
}