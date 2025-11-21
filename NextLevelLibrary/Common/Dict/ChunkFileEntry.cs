using AvaloniaToolbox.Core.IO;
using FileConverter;
using FileConverter.LM2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace NextLevelLibrary
{
    /// <summary>
    /// A chunk that represents a file that stores a unique hash and identifier in the header.
    /// These will store either raw data or sub chunks depending on the sub data flag.
    /// </summary>
    public class ChunkFileEntry : ChunkEntry
    {
        //Hashes from the header chunk
        public HashString HashType;
        public HashString FilePath;

        /// <summary>
        /// The chunk that stores the file header containing the hash type and hash file path.
        /// </summary>
        public ChunkEntry Header;

        /// <summary>
        /// The file format embedded to save back as if possible.
        /// </summary>
        public IFormat FileFormat;

        public ChunkFileEntry(IDictionaryData dict, IChunkTable table) : base(dict, table) {
        }

        public ChunkFileEntry(IDictionaryData dict, IChunkTable table, ChunkType type) : base(dict, table)
        {
            this.Type = type;
            this.SetupDefaultFlags();
            this.Header = new ChunkEntry(dict, table)
            {
                Type = ChunkType.FileHeader,
                Data = new MemoryStream(),
            };
            this.Header.SetupDefaultFlags();
        }

        public override void OnSave()
        {
            FileFormat?.Save(this);
        }

        public override void Export(string path)
        {
            OnSave();
            // Here we write a binary format similar to .rlg on the wii
            // Only we include a file header chunk with their file path hash and hash type
            using (var writer = new FileWriter(path)) {
                WriteChunk(writer, this);
            }
        }

        static void WriteChunk(FileWriter writer, ChunkEntry chunk)
        {
            if (chunk is ChunkFileEntry fileEntry)
            {
                writer.Write((ushort)fileEntry.Header.Type);
                writer.Write((ushort)fileEntry.Header.Flags);
                writer.WriteZeroTerminatedString(fileEntry.Header.Type.ToString());
                writer.Write(8);
                writer.Write((uint)fileEntry.FilePath.Value);
                writer.Write((uint)fileEntry.HashType.Value);
            }

            writer.Write((ushort)chunk.Type);
            writer.Write((ushort)chunk.Flags);
            writer.WriteZeroTerminatedString(chunk.Type.ToString());

            var start = writer.Position;
            writer.Write(0); // total size

            if (chunk.HasChildren)
            {
                foreach (var child in chunk.Children)
                    WriteChunk(writer, child);
 
            }
            else
            {
                if (chunk.Data != null)
                {
                    chunk.Data.Position = 0;
                    chunk.Data.CopyTo(writer.BaseStream);
                }
            }

            var end = writer.Position;
            writer.WriteSectionSizeU32(start, (uint)(end - start - 4));
        }
    }
}
