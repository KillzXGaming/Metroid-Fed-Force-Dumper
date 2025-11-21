using AvaloniaToolbox.Core.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NextLevelLibrary.LM2
{
    public class DataFile : IDataFile
    {
        public List<ChunkTable> Tables { get; set; } = new List<ChunkTable>();

        private DictionaryFile DictionaryFile;

        public DataFile(DictionaryFile dict)
        {
            DictionaryFile = dict;
            for (int i = 0; i < dict.FileTableInfos.Count; i += dict.FileTableReferences.Count)
            {
                //only load first table reference, second is unused (debug)
                LoadBlock(dict.FileTableInfos[i], dict.FileTableReferences[0]);
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

        private void LoadBlock(DictionaryFile.FileTableInfo tableCountInfo, DictionaryFile.FileTableReference tableInfo)
        {
            var table_block = DictionaryFile.Blocks[tableCountInfo.TableBlockIndex];
            string data_path = DictionaryFile.FilePath.Replace(".dict", table_block.FileExtension);
            if (!File.Exists(data_path))
                return;

            //Use a shift value, as the block indices shift per table
            var shift = tableCountInfo.TableBlockIndex;

            //ensure all blocks exist in the table info
            for (int i = 0; i < tableInfo.BlockIndices.Length; i++)
            {
                if (i == 0)
                    continue;

                var block = DictionaryFile.Blocks[shift + tableInfo.BlockIndices[i]];
                string block_data_path = DictionaryFile.FilePath.Replace(".dict", block.FileExtension);
                if (!File.Exists(block_data_path))
                    return;
            }

            var stream = File.OpenRead(data_path);
            //Load the chunk data
            var table = new ChunkTable(DictionaryFile, table_block.Decompress(stream), tableCountInfo.NumFiles);
            table.Name = tableInfo.Name;

            //Setup hashes which help tell apart region data
            foreach (var file in table.Files)
                file.SetupHash(tableInfo.BlockIndices);

            //Prepare the raw data
            Stream[] buffers = new Stream[tableInfo.BlockIndices.Length];
            for (int i = 0; i < tableInfo.BlockIndices.Length; i++)
            {
                if (tableInfo.BlockIndices[i] == 0) //none used, skip
                    continue;

                //set data if needed
                var block = DictionaryFile.Blocks[shift + tableInfo.BlockIndices[i]];
                string block_data_path = DictionaryFile.FilePath.Replace(".dict", block.FileExtension);
                Stream block_data_stream = File.OpenRead(block_data_path);

                if (block.Data == null)
                    block.Data = block.Decompress(block_data_stream);

                buffers[i] = block.Data;
            }
            table.Buffers = buffers;
            //Finally load the files with data
            foreach (var file in table.Files)
            {
                if (file.IsGlobalShared)
                    continue;

                LoadData(file, buffers);
            }
            Tables.Add(table);
        }

        private void LoadData(ChunkEntry entry, Stream[] block_streams)
        {
            if (entry.HasChildren)
            {
                foreach (var child in entry.Children)
                    LoadData(child, block_streams);
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
                entry.HashType = new HashString(reader.ReadUInt32());
                entry.FilePath = new HashString(reader.ReadUInt32());
            }
        }
    }
}
