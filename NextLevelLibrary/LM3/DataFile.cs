using AvaloniaToolbox.Core.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NextLevelLibrary.LM3
{
    public class DataFile : IDataFile
    {
        public List<ChunkTable> Tables { get; set; } = new List<ChunkTable>();

        public DictionaryFile DictionaryFile;

        public DataFile(DictionaryFile dict)
        {
            DictionaryFile = dict;
            for (int i = 0; i < dict.FileTableReferences.Count; i++)
            {
                LoadBlock(dict, dict.FileTableReferences[i], i);
                break;
            }
        }

        public void Save(string filePath)
        {
            DictionaryFile.IsCompressed = false;

            filePath = filePath.Replace(".dict", ".data");
            //Save loaded data
            foreach (var file in Tables.SelectMany(x => x.Files))
                file.OnSave();
            //Save data file
            DataWriter writer = new DataWriter();
            writer.Save(filePath, this, this.DictionaryFile);
        }

        private void LoadBlock(DictionaryFile dict, DictionaryFile.FileTableReference tableInfo, int index)
        {
            var table_block = DictionaryFile.Blocks[index];
            string data_path = DictionaryFile.FilePath.Replace(".dict", table_block.FileExtension);
            if (!File.Exists(data_path))
                return;

            //ensure all blocks exist in the table info
            for (int i = 0; i < tableInfo.BlockIndices.Length; i++)
            {
                if (i == 0)
                    continue;

                var block = DictionaryFile.Blocks[tableInfo.BlockIndices[i]];
                string block_data_path = DictionaryFile.FilePath.Replace(".dict", block.FileExtension);
                if (!File.Exists(block_data_path))
                    return;
            }

            var stream = File.OpenRead(data_path);
            //Load the chunk data
            var table = new ChunkTable(DictionaryFile, table_block.Decompress(stream), tableInfo.FileSectionCount);
            table.Name = tableInfo.Name;
            Tables.Add(table);

            //Setup hashes which help tell apart region data
            foreach (var file in table.Files)
                file.SetupHash(tableInfo.BlockIndices);

            LoadRegionOnlyFiles(tableInfo, table);
            //Prepare the raw data
            Stream[] buffers = new Stream[tableInfo.BlockIndices.Length];
            for (int i = 0; i < tableInfo.BlockIndices.Length; i++)
            {
                if (tableInfo.BlockIndices[i] == 0) //none used, skip
                    continue;

                //set data if needed
                var block = DictionaryFile.Blocks[tableInfo.BlockIndices[i]];
                string block_data_path = DictionaryFile.FilePath.Replace(".dict", block.FileExtension);
                Stream block_data_stream = File.OpenRead(block_data_path);

                if (block.Data == null)
                {
                    // Load decompressed stream
                    block.Data = LoadDecompressedStream(dict, block, block_data_stream, tableInfo.BlockIndices[i]);
                }

                buffers[i] = block.Data;
            }

            table.Buffers = buffers;

            //Finally load the files with data
            foreach (var file in table.Files)
            {
                //if (file.IsGlobalShared)
                 //   continue;

                LoadData(file, buffers);
            }
        }

        private Stream LoadDecompressedStream(DictionaryFile dict, Block block, Stream stream, int index)
        {
            return block.Decompress(stream);

            // Here we cache the decompressed stream to disk. This greatly improves performance.
            // The layout matches the LM3 noesis script so it can be used for both tools.
            var fileName = Path.GetFileNameWithoutExtension(dict.FilePath);
            var folderPath = Path.GetDirectoryName(dict.FilePath);
            var dir = Path.Combine(folderPath, fileName, "File_Data");
            var cached = Path.Combine(dir, $"{fileName}_{index}.lm3");

            Directory.CreateDirectory(dir);

            if (!File.Exists(cached))
            {
                var decomp = block.Decompress(stream);
                decomp.SaveToFile(cached);
            }

            return new FileStream(cached, FileMode.Open, FileAccess.Read);
        }

        private List<ChunkEntry> LoadRegionOnlyFiles(DictionaryFile.FileTableReference tableInfo, ChunkTable table)
        {
            var global_table = Tables.FirstOrDefault();
            if (global_table == null) return new List<ChunkEntry>();

            var hashes = global_table.Files.Select(x => x.DataHash).ToList();

            //check for duplicate files in upcoming tables and skip loading them
            List<ChunkEntry> entires = new List<ChunkEntry>();
            foreach (var file in table.Files)
            {
                file.IsGlobalShared = hashes.Contains(file.DataHash);

                if (file.IsGlobalShared)
                    continue;

                file.IsRegionOnly = true;
                entires.Add(file);
            }
            return entires;
        }

        private void LoadData(ChunkEntry entry, Stream[] block_streams)
        {
            if (entry.HasChildren)
            {
               // foreach (var child in entry.Children)
                //    LoadData(child, block_streams);
            }
            else
            {
                //Load the data. 
                var stream = block_streams[entry.BlockIndex];
                entry.Data = new SubStream(stream, entry.Offset, entry.Size);
            }

            if (entry is ChunkFileEntry)
            {
                //load header data
                LoadData(((ChunkFileEntry)entry).Header, block_streams);
                LoadFileHeader((ChunkFileEntry)entry);
            }
        }

        private void LoadFileHeader(ChunkFileEntry entry)
        {
            using (var reader = new FileReader(entry.Header.Data, true)) {
                reader.Position = 0;
                entry.HashType = new HashString(reader.ReadUInt32());
                entry.FilePath = new HashString(reader.ReadUInt32());
            }
        }
    }
}
