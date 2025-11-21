using AvaloniaToolbox.Core.Compression;
using AvaloniaToolbox.Core.IO;
using NextLevelLibrary.LM3;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NextLevelLibrary.Files.LM3
{
    public class PatchFile
    {
        public List<TablePatch> Tables = new List<TablePatch>();

        public PatchFile(Stream stream, DictionaryFile dict, Stream dataStream)
        {
            using (var reader = new FileReader(stream))
            {
                reader.ReadUInt32(); //flags
                reader.ReadUInt32(); //0

                uint buffer_offset = reader.ReadUInt32();
                uint buffer_compressed_size = reader.ReadUInt32();
                uint buffer_decompressed_size = reader.ReadUInt32();
                reader.ReadUInt32(); //0

                const int num_region_tables = 13;
                const int num_blocks_per_table = 16;

                //raw data to file chunks
                var data_decomp = Decompress(reader, buffer_offset, buffer_compressed_size);

                data_decomp = new SubStream(data_decomp, 96);

                //13 region tables
                for (int i = 0; i < num_region_tables; i++)
                {
                    var table_info = dict.FileTableReferences[i * 2];

                    uint table_offset = reader.ReadUInt32();
                    uint table_compressed_size = reader.ReadUInt32();
                    uint num_chunks = reader.ReadUInt32(); //total chunk count in table
                    ushort num_file_chunks = reader.ReadUInt16(); //total amount of 8 byte sections for file data
                    ushort num_files = reader.ReadUInt16(); //total file count not including file identifier chunk

                    //sometimes matches decomp original sizes, but sometimes does not when patched
                    uint[] decomp_sizes = reader.ReadUInt32s(num_blocks_per_table);
                    //0 if not patched, else it can be a decomp size
                    uint[] decomp_patch_sizes = reader.ReadUInt32s(num_blocks_per_table);
                    //possible offsets, but unsure how these work yet
                    uint[] decomp_patch_offsets = reader.ReadUInt32s(num_blocks_per_table);

                    //setup patch data
                    Stream[] blocks = new Stream[num_blocks_per_table];
                    for (int j = 0; j < num_blocks_per_table; j++)
                    {
                        var blockIdx = table_info.BlockIndices[j];
                        //var data = dict.Blocks[blockIdx].Decompress(dataStream);
                        //patch in the data

                        var offset = decomp_patch_offsets[j];
                        var size1 = decomp_patch_sizes[j];
                        var size2 = decomp_sizes[j];

                        //blocks[j] = data;
                        blocks[j] = new MemoryStream(new byte[dict.Blocks[blockIdx].DecompressedSize]);

                        //no patch to block applied
                        if (decomp_patch_offsets[j] == uint.MaxValue)
                            continue;

                        //attach buffer
                        var mem = new MemoryStream();
                        using (var writer = new FileWriter(mem))
                        {
                            writer.Write(new byte[dict.Blocks[blockIdx].DecompressedSize]);
                           // writer.Write(data.ReadAllBytes());
                            writer.Write(data_decomp.ReadAllBytes());
                        }

                        blocks[j] = new MemoryStream(mem.ToArray()); 

                        Console.WriteLine($"{offset} {size1} {size2} blocks[j] {blocks[j].Length}");
                    }

                    var decomp = Decompress(reader, table_offset, table_compressed_size);

                    ChunkTable table = new ChunkTable(dict, decomp, num_file_chunks);
                    table.Buffers = blocks;
                    Tables.Add(new TablePatch()
                    {
                        Table = table,
                        decomp_patch_offsets = decomp_patch_offsets,
                        decomp_patch_sizes = decomp_patch_sizes,
                        decomp_sizes = decomp_sizes,
                        Blocks = blocks,
                    });

                    break;
                }

                void LoadChunk(ChunkEntry chunk)
                {
                    //load data if not hiearchy node
                    if (!chunk.HasChildren)
                    {
                        var block = Tables[0].Blocks[chunk.BlockIndex];
                        Console.WriteLine($"data  {chunk.BlockIndex}  {chunk.Offset} {chunk.Size}");

                        if (chunk.Offset + chunk.Size <= block.Length)
                            chunk.Data = new SubStream(block, chunk.Offset, chunk.Size);
                        else
                        {
                            chunk.Data = new MemoryStream();
                            //failed
                            Console.WriteLine($"Failed to get data chunk  {chunk.BlockIndex}  {chunk.Offset} {chunk.Size}");
                        }
                    }

                    //file header chunk
                    if (chunk is ChunkFileEntry) 
                        LoadChunk(((ChunkFileEntry)chunk).Header);

                    foreach (var c in chunk.Children)
                        LoadChunk(c);
                }

                bool HasPatchedData(int tableIdx, ChunkEntry chunk)
                {
                    if (chunk.HasChildren)
                    {
                        foreach (var child in chunk.Children)
                        {
                            if (HasPatchedData(tableIdx, child))
                                return true;
                        }
                    }
                    else
                    {
                        var b = Tables[tableIdx].decomp_sizes[chunk.BlockIndex];
                        if (chunk.Offset >= b)
                            return true;
                    }

                    return false;
                }
           
                List<ChunkEntry> ChunkEntries = new List<ChunkEntry>();
                foreach (var file in Tables[0].Table.Files)
                {
                    LoadChunk(file);

                    if (HasPatchedData(0, file))
                        ChunkEntries.Add(file);
                }

                Tables[0].Table.Files.Clear();
                Tables[0].Table.Files.AddRange(ChunkEntries);
            }
        }

        public class TablePatch
        {
            public ChunkTable Table;

            public uint[] decomp_sizes;
            public uint[] decomp_patch_sizes;
            public uint[] decomp_patch_offsets;

            public Stream[] Blocks;

        }

        static Stream Decompress(FileReader reader, uint offset, uint comp_size)
        {
            using (reader.TemporarySeek(offset, SeekOrigin.Begin))
            {
                return new MemoryStream(ZLIB.Decompress(
                    reader.ReadBytes((int)comp_size)));
            }
        }
    }
}
