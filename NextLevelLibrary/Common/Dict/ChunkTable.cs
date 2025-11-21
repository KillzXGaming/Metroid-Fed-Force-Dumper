using AvaloniaToolbox.Core.IO;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NextLevelLibrary
{
    public class ChunkTable : IChunkTable
    {
        public const int CHUNK_SIZE = 12;

        public List<ChunkEntry> Files { get; set; } = new List<ChunkEntry>();
        public List<ChunkEntry> GetFiles => this.Files.Where(x => x is ChunkFileEntry).ToList();
        public List<ChunkEntry> GetLooseData => this.Files.Where(x => x is not ChunkFileEntry).ToList();

        public HashString Name;

        private IDictionaryData DictionaryFile;

        public Stream[] Buffers = new Stream[8];

        private Stream _stream;

        public ChunkTable() { }

        public ChunkTable(IDictionaryData dict, Stream stream, uint num_files)
        {
            _stream = stream;

            stream.Position = 0;
            DictionaryFile = dict;
            using (var reader = new FileReader(stream, true)) {
                Read(reader, num_files);
            }
        }

        public static byte[] Save(List<ChunkEntry> files)
        {
            var mem = new MemoryStream();
            using (var writer = new FileWriter(mem)) {
                Write(writer, files);
            }
            return mem.ToArray();
        }

        public ChunkEntry GetTexture(uint hash)  => GetFile(hash, ChunkType.Texture);
        public ChunkEntry GetSkeleton(uint hash) => GetFile(hash, ChunkType.Skeleton);
        public ChunkEntry GetModel(uint hash)    => GetFile(hash, ChunkType.Model);
        public ChunkEntry GetAnimation(uint hash) => GetFile(hash, ChunkType.AnimationData);

        public ChunkEntry GetFile(uint hash, ChunkType type)
        {
            foreach (var file in this.Files)
            {
                if (file.Type != type)
                    continue;

                if (((ChunkFileEntry)file).FilePath.Value == hash)
                    return file;
            }
            return null;
        }

        public bool HasFile(uint hash)
        {
            foreach (ChunkFileEntry file in this.GetFiles)
            {
                if (file.FilePath.Value == hash)
                    return true;
            }
            return false;
        }

        public ChunkFileEntry GetFile(uint hash)
        {
            foreach (ChunkFileEntry file in this.GetFiles)
            {
                if (file.FilePath.Value == hash)
                    return file;
            }
            return null;
        }

        void Read(FileReader reader, uint num_files)
        {
            for (int i = 0; i < num_files; i++) {

                var file = ReadChunk(reader);
                Files.Add(file);
                //2 chunks per file header entry, so shift to next file
                if (file is ChunkFileEntry) i++;
            }
        }

        static void Write(FileWriter writer, List<ChunkEntry> files)
        {
            int childStartIndex = files.Sum(x => x is ChunkFileEntry ? 2 : 1);

            List<ChunkEntry> list = new List<ChunkEntry>();
            list.AddRange(files);

            void SetupChildren(ChunkEntry chunk)
            {
                int numChildren = chunk.Children.Sum(x => x is ChunkFileEntry ? 2 : 1);

                chunk.Offset = (uint)(childStartIndex);
                chunk.Size = (uint)numChildren;
                childStartIndex += numChildren;

                foreach (var child in chunk.Children)
                    list.Add(child);

                foreach (var child in chunk.Children)
                {
                    if (child.HasChildren)
                        SetupChildren(child);
                }
            }

            foreach (var file in files)
            {
                if (file.HasChildren)
                    SetupChildren(file);
            }

            //save each chunk in order
            foreach (var file in list)
            {
                if (file is ChunkFileEntry)
                    Write(writer, ((ChunkFileEntry)file).Header);

                Write(writer, file);
            }
        }

        static void Write(FileWriter writer, ChunkEntry chunk)
        {
            writer.Write((ushort)chunk.Type);
            writer.Write((ushort)chunk.Flags);
            writer.Write(chunk.Size);
            writer.Write(chunk.Offset);
        }

        internal ChunkEntry ReadChunk(FileReader reader)
        {
            var type = (ChunkType)reader.ReadUInt16();

            ChunkEntry chunk = type == ChunkType.FileHeader ?
                new ChunkFileEntry(DictionaryFile, this) : new ChunkEntry(DictionaryFile, this);

            if (type == ChunkType.FileHeader)
            {
                // 2 chunks, one for file header, then one for data
                // The game treats the header as its own chunk, but I combine to be easier to work with
                // The file header is always 8 bytes and stores a magic value and file path hash
                ((ChunkFileEntry)chunk).Header = new ChunkEntry(DictionaryFile, this)
                {
                    Type = type,
                    Flags = reader.ReadUInt16(),
                    Size = reader.ReadUInt32(),
                    Offset = reader.ReadUInt32(),
                };
                chunk.Type = (ChunkType)reader.ReadUInt16();
                chunk.Flags = reader.ReadUInt16();
                chunk.Size = reader.ReadUInt32();
                chunk.Offset = reader.ReadUInt32();
            }
            else
            {
                chunk.Type = type;
                chunk.Flags = reader.ReadUInt16();
                chunk.Size = reader.ReadUInt32();
                chunk.Offset = reader.ReadUInt32();
            }
/*
            if (chunk.HasChildren)
            {
                // Children are loaded dynamically when needed

                //index to chunk * size of chunk (12)
                using (reader.TemporarySeek(chunk.Offset * CHUNK_SIZE, SeekOrigin.Begin))
                {
                   for (int i = 0; i < chunk.Size; i++)
                    {
                        var child = ReadChunk(reader);
                        chunk.Children.Add(child);
                        //2 chunks per file header entry, so shift to next file
                        if (child is ChunkFileEntry) i++;
                    }
                }
            }*/
            return chunk; 
        }

        public List<ChunkEntry> ReadChildren(ChunkEntry chunk)
        {
            if (!chunk.HasChildren) return new List<ChunkEntry>();

            List<ChunkEntry> children = new List<ChunkEntry>();
            using (var reader = new FileReader(_stream, true))
            {
                //index to chunk * size of chunk (12)
                using (reader.TemporarySeek(chunk.Offset * CHUNK_SIZE, SeekOrigin.Begin))
                {
                     for (int i = 0; i < chunk.Size; i++)
                      {
                          var child = ReadChunk(reader);
                        children.Add(child);
                        //2 chunks per file header entry, so shift to next file
                        if (child is ChunkFileEntry) i++;

                        // Load data
                        if (!child.HasChildren)
                        {
                            var stream = this.Buffers[child.BlockIndex];
                            if (child.Offset + child.Size <= stream.Length)
                                child.Data = new SubStream(stream, child.Offset, child.Size);
                        }
                    }
                }
            }
            return children;
        }

        /// <summary>
        /// Exports chunk data in a more readable .txt format for debugging purposes
        /// </summary>
        public void Export(string path)
        {
            using (var writer = new StreamWriter(path))
            {
                void WriteChunk(ChunkEntry entry)
                {
                    if (entry is ChunkFileEntry)
                    {
                        var file = ((ChunkFileEntry)entry).Header;

                        writer.WriteLine($"{file.Flags} {file.Type} {file.Offset} {file.Size}");
                        writer.WriteLine($"{entry.Flags} {entry.Type}{entry.Offset} {entry.Size}");
                    }
                    else
                    {
                        writer.WriteLine($"{entry.Flags} {entry.Type} {entry.Offset} {entry.Size}");
                    }

                    foreach (var child in entry.Children)
                        WriteChunk(child);
                }

                foreach (var file in this.Files)
                    WriteChunk(file);
            }
        }
    }
}
