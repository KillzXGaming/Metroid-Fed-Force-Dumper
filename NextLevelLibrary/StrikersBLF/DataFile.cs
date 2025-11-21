using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Toolbox.Core;
using AvaloniaToolbox.Core.IO;

namespace NextLevelLibrary.StrikersBLF
{
    public class DataFile : IDataFile
    {
        public List<ChunkTable> Tables { get; set; } = new List<ChunkTable>();
        private DictionaryFile DictionaryFile;

        public DataFile(Stream stream, IDictionaryData dict)
        {
            DictionaryFile = dict as DictionaryFile;
            Tables.Add(DictionaryFile.Table);

            var blocks = dict.BlockList.ToList();

            foreach (var b in blocks)
            {
                if (b.SourceIndex == 0)
                    b.Data = b.Decompress(stream);
            }

            foreach (var chunk in DictionaryFile.Table.Files)
            {
                if (chunk.Children.Count > 0)
                    continue;

                var index = BitUtils.GetBits(chunk.Flags, 8, 6);
                var block = blocks[index];
                if (block.SourceIndex != 0)
                    continue;

                if (block.Data != null)
                    chunk.Data = new SubStream(block.Data, chunk.Offset, chunk.Size);
                else
                    chunk.Data = new MemoryStream();
            }

            foreach (var chunk in DictionaryFile.Table.DataEntries)
            {
                if (chunk.Children.Count > 0)
                    continue;

                var index = BitUtils.GetBits(chunk.Flags, 8, 6);

                var block = blocks[index];
                if (block.SourceIndex != 0)
                    continue;

                if (block.Data != null)
                    chunk.Data = new SubStream(block.Data, chunk.Offset, chunk.Size);
                else
                    chunk.Data = new MemoryStream();
            }
        }
    }
}
