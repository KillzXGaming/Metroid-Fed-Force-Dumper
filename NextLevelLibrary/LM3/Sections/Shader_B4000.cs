using AvaloniaToolbox.Core;
using AvaloniaToolbox.Core.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Toolbox.Core;

namespace NextLevelLibrary.LM3
{
    public class Shader : IFormat
    {
        public HashString Name { get; set; }

        public Stream ByteCodeShader { get; set; }
        public Stream ControlShader { get; set; }

        public Shader(ChunkFileEntry fileEntry)
        {
            Name = fileEntry.FilePath;

            var shaderA = fileEntry.GetChild(ChunkType.ShaderA);
            var shaderB  = fileEntry.GetChild(ChunkType.ShaderB);

            ByteCodeShader = shaderA.Data;
            ControlShader = shaderB.Data;
        }

        public byte[] GetShaderData()
        {
            using (var reader = new FileReader(ByteCodeShader, true))
            {
                reader.Position = 0;
                byte[] binaryData = reader.ReadBytes((int)reader.BaseStream.Length);
                return ByteUtil.SubArray(binaryData, 48, (uint)binaryData.Length - 48);
            }
        }

        public byte[] GetConstants()
        {
            //Bnsh has 2 shader code sections. The first section has block info for constants
            using (var reader = new FileReader(ControlShader))
            {
                reader.SeekBegin(1776);
                ulong ofsUnk = reader.ReadUInt64();
                uint lenByteCode = reader.ReadUInt32();
                uint lenConstData = reader.ReadUInt32();
                uint ofsConstBlockDataStart = reader.ReadUInt32();
                uint ofsConstBlockDataEnd = reader.ReadUInt32();
                return GetConstantsFromCode(ByteCodeShader, ofsConstBlockDataStart, lenConstData);
            }
        }

        static byte[] GetConstantsFromCode(Stream shaderCode, uint offset, uint length)
        {
            using (var reader = new FileReader(shaderCode, true))
            {
                reader.SeekBegin(offset);
                return reader.ReadBytes((int)length);
            }
        }

        public byte[] GetShaderB()
        {
            using (var reader = new FileReader(ControlShader, true))
            {
                byte[] binaryData = reader.ReadBytes((int)reader.BaseStream.Length);
                return ByteUtil.SubArray(binaryData, 0, (uint)binaryData.Length - 0);
            }
        }
    }
}
