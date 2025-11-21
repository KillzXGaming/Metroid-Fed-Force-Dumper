using AvaloniaToolbox.Core.Compression;
using AvaloniaToolbox.Core.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.AccessControl;
using Toolbox.Core;

namespace NextLevelLibrary
{
    /// <summary>
    /// A block which can decompress part of the .data file for later loading chunk data.
    /// </summary>
    public class Block 
    {
        /// <summary>
        /// The raw data in the block. Compressed if the dictionary uses block compression.
        /// </summary>
        public Stream Data { get; set; }

        /// <summary>
        /// The parent dictionary file that the block uses.
        /// </summary>
        public IDictionaryData Dictionary { get; set; }

        /// <summary>
        /// The offset of the data.
        /// </summary>
        public uint Offset;

        /// <summary>
        /// The decompressed size of the data.
        /// </summary>
        public uint DecompressedSize;

        /// <summary>
        /// The compressed size of the data.
        /// </summary>
        public uint CompressedSize;

        /// <summary>
        /// The index of the block in the dictionary.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// The flags of the block.
        /// </summary>
        public uint Flags;

        public Block(IDictionaryData dict, int index)
        {
            Dictionary = dict;
            Index = index;
        }

        public bool IsZSTDCompressed = false;

        /// <summary>
        /// The index of the data source file that gets the file extension.
        /// Most external sources may not exist for debug purposes and can be skipped
        /// </summary>
        public byte SourceIndex { get; set; }

        /// <summary>
        /// The detected resource type. Can either be a type of table or raw data.
        /// </summary>
        public ResourceType SourceType { get; set; }

        /// <summary>
        /// The file extension to determine what external file to load data on.
        /// Will always be .data. The .debug files are unused.
        /// </summary>
        public string FileExtension { get; set; }

        public Stream Compress(Stream decompressed)
        {
            if (Dictionary == null) return new MemoryStream();

            CompressedSize = (uint)decompressed.Length;
            DecompressedSize = (uint)decompressed.Length;

            if (Dictionary.BlocksCompressed)
            {
                var buffer = decompressed.ReadAllBytes();
                var compressed = ZLIB.Compress(buffer);
                CompressedSize = (uint)compressed.Length;
                return new MemoryStream(compressed);
            }
            else
                return decompressed;
        }

        public byte[] CompressZLIB(byte[] input)
        {
            return ZLIB.Compress(input);
        }

        public virtual Stream Decompress(Stream compressed)
        {
            using (var reader = new FileReader(compressed, true))
            {
                if (Offset > reader.BaseStream.Length || DecompressedSize == 0)
                    return new MemoryStream();

                reader.SeekBegin(Offset);

                if (IsZSTDCompressed)
                {
                    return new MemoryStream(ZstdFormat.Decompress(reader.ReadBytes((int)CompressedSize)));
                }

                //Check the dictionary if the files are compressed
                if (Dictionary.BlocksCompressed)
                {
                    //Check the magic to see if it's zlib compression
                    ushort Magic = reader.ReadUInt16();
                    bool IsZLIB = Magic == 0x9C78 || Magic == 0xDA78 || Magic == 0x789C;
                    reader.SeekBegin(Offset);

                    if (IsZLIB)
                    {
                        var decomp = new MemoryStream(ZLIB.Decompress(
                              reader.ReadBytes((int)CompressedSize)));

                        if (decomp.Length != this.DecompressedSize)
                            throw new Exception();

                        File.WriteAllBytes($"Block{Index}.bin", decomp.ReadAllBytes());

                        return decomp;
                    }
                    else
                        //Unknown compression so skip it.
                        return new MemoryStream();
                } //File is decompressed so check if it's in the range of the current data file.
                else if (Offset + DecompressedSize <= reader.BaseStream.Length)
                {
                    reader.SeekBegin(Offset);
                    var data = reader.ReadBytes((int)DecompressedSize);
                    return new MemoryStream(data);
                }
                //   return new SubStream(reader.BaseStream, Offset, DecompressedSize);
            }
            return new MemoryStream();
        }

        public byte[] ReadBytes()
        {
            using (var reader = new FileReader(Data, true))
            {
                return reader.ReadBytes((int)reader.BaseStream.Length);
            }
        }

        public bool IsZLIB()
        {
            using (var reader = new FileReader(Data, true))
            {
                ushort Magic = reader.ReadUInt16();
                bool IsZLIP = Magic == 0x9C78 || Magic == 0xDA78;
                return IsZLIP;
            }
        }
    }
}