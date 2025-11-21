using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NextLevelLibrary.LM3
{
    public class MaterialParam : IFormat
    {
        public HashString Name { get; set; }

        public ushort ID { get; set; }

        public byte[] Data { get; set; }

        public bool IsSet = false;

        public MaterialParam() { }

        public MaterialParam(ChunkFileEntry fileEntry)
        {
            Name = fileEntry.FilePath;

            var idChunk = fileEntry.GetChild((ChunkType)0xB311);
            var dataChunk = fileEntry.GetChild((ChunkType)0xB312);

            ID = idChunk.ReadPrimitive<ushort>();
            Data = dataChunk.ReadBytes((uint)dataChunk.Data.Length);
        }
    }
}
