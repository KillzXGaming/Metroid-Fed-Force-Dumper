using AvaloniaToolbox.Core.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NextLevelLibrary.LM2
{
    public class DataWriter
    {
        public void Save(string filePath, DataFile data, DictionaryFile dictionary)
        {
            //create a list of streams to apply saving to
            Stream[] streams = new Stream[dictionary.Blocks.Count];
            for (int i = 0; i < dictionary.FileTableInfos.Count; i += dictionary.FileTableReferences.Count) //skip other file refs (ie debug data)
            {
                var table_info = dictionary.FileTableReferences[i];
                var tableCounterInfo = dictionary.FileTableInfos[i];

                //get the table from our data file
                var table = data.Tables.FirstOrDefault(x => x.Name.Value == table_info.Name.Value);

                //save all the table contents into the stream list
                SaveData(table, table_info.BlockIndices, streams);
                //save table into their block
                var files = table.Files.ToList();
                //save table
                streams[i] = new MemoryStream(ChunkTable.Save(files));
                //Number of total sections. Files have 2 (header + data)
                tableCounterInfo.NumFiles = (ushort)files.Sum(x => x is ChunkFileEntry ? 2 : 1);
            }
            //save each block that has been updated
            for (int i = 0; i < dictionary.Blocks.Count; i++)
            {
                if (streams[i]  != null) 
                    dictionary.Blocks[i].Data = dictionary.Blocks[i].Compress(streams[i]);
            }

            //external files
            var extensions = dictionary.Blocks.Select(x => x.FileExtension).Distinct().ToList();
            foreach (var ext in extensions)
            {
                var blocks = dictionary.Blocks.Where(x => x.FileExtension == ext && x.Data != null).ToList();
                //skip empty and unused blocks blocks
                if (blocks.Count == 0 || ext == ".debug" || ext == ".nxpc")
                    continue;

                //Save all the blocks to writer
                using (var writer = new FileWriter(filePath.Replace(".data", ext))) {
                    WriteData(writer, blocks, dictionary);
                }
            }
        }

        private void WriteData(FileWriter writer, List<Block> blocks, DictionaryFile dict)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                if (dict.Version == GameVersion.LM2HD)
                    writer.Align(512);
                else
                    writer.Align(8);
                blocks[i].Offset = (uint)writer.BaseStream.Position;
                if (blocks[i].Data != null)
                    writer.Write(blocks[i].Data.ReadAllBytes());
            }
        }

        private void SaveData(ChunkTable table, byte[] streamIndices, Stream[] streams)
        {
            foreach (var file in table.Files)
            {
                if (file is ChunkFileEntry)
                {
                    //2 chunks per file
                    SaveChunkData(((ChunkFileEntry)file).Header, streamIndices, streams);
                    SaveChunkData(file, streamIndices, streams);
                }
                else
                    SaveChunkData(file, streamIndices, streams);
            }
        }

        private void SaveChunkData(ChunkEntry chunk, byte[] streamIndices, Stream[] streams)
        {
            if (chunk.HasChildren) //no data, just list of chunks
            {
                foreach (var child in chunk.Children)
                    SaveChunkData(child, streamIndices, streams);
                return;
            }

            //target stream
            var index = streamIndices[chunk.BlockIndex];
            if (streams[index] == null)
                streams[index] = new MemoryStream();
            var stream = streams[index];
            //write to block stream
            using (var writer = new FileWriter(stream, true)) {

                var align = chunk.GetDataAlignment();

                //go to end of stream and write out the data
                writer.SeekBegin(writer.BaseStream.Length);
                writer.Align(align);
                chunk.Offset = (uint)writer.BaseStream.Position;
                chunk.Size = (uint)chunk.Data.Length;
                writer.Write(chunk.Data.ReadAllBytes());
            }
        }
    }
}
