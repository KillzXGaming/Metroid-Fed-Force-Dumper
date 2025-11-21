using AvaloniaToolbox.Core.IO;
using NextLevelLibrary.Files.Common;
using System.Runtime.InteropServices;

namespace NextLevelLibrary.LM3
{
    public class ShaderDefinitions : IFormat
    {
        public HashString Name { get; set; }

        public ulong ParametersFlag;
        public uint SamplerFlag;
        public uint InputsFlag;
        public uint Flag1;
        public uint OutputsFlag;

        public List<UniformBlock> Blocks = new List<UniformBlock>();

        public ShaderDefinitions(ChunkFileEntry fileEntry)
        {
            Name = fileEntry.FilePath;
            using (var reader = new FileReader(fileEntry.Data, true)) {
                reader.Position = 0;
                Read(reader);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class UniformParameter
        {
            public ushort ReadOffset; //offset to read data from a parameter buffer
            public ushort BlockOffset; //offset to write to the uniform buffer
            public ushort Size; //total data size to read/write to
        }

        public List<int> GetFlag1Indices() => Util.GetBitIndices(Flag1);
        public List<int> GetOutputIndices() => Util.GetBitIndices(OutputsFlag);
        public List<int> GetInputIndices() => Util.GetBitIndices(InputsFlag);
        public List<int> GetParameterIndices() => Util.GetBitIndices(ParametersFlag);
        public List<int> GetSamplerIndices() => Util.GetBitIndices(SamplerFlag);

        private void Read(FileReader reader)
        {
            ParametersFlag = reader.ReadUInt64(); //total parameter indices used by all blocks by bits
            SamplerFlag = reader.ReadUInt32(); //sampler indices by bits
            Flag1 = reader.ReadUInt32(); //unk
            OutputsFlag = reader.ReadUInt32(); //output indices by bits
            InputsFlag = reader.ReadUInt32(); //input indices by bits
            uint uniformBlockCount = reader.ReadUInt32(); //total amount of blocks used. Pixel shader always 4, but block will be empty if unused
            reader.ReadUInt32(); //0

            for (int i = 0; i < uniformBlockCount; i++)
            {
                //start of header
                long pos = reader.Position;

                UniformBlock block = new UniformBlock();
                block.ParametersFlag = reader.ReadUInt64(); //parameter indices by bits
                var numParamList = reader.ReadUInt32();
                var paramOffset = reader.ReadUInt32();
                Blocks.Add(block);

                using (reader.TemporarySeek(pos + paramOffset, SeekOrigin.Begin))
                {
                    for (int j = 0; j < numParamList; j++)
                    {
                        var offset = ReadOffset(reader); //offset to param
                        var count = reader.ReadUInt16(); //num params
                        //Read the parameter read/write offsets and size
                        block.Parameters.Add(ReadParameters(reader, offset, count));
                    }
                    block.BufferSize = reader.ReadUInt32(); //total buffer size
                }
            }
        }

        public class UniformBlock
        {
            public List<UniformParameter[]> Parameters = new List<UniformParameter[]>();

            public List<int> ParamIndices = new List<int>();

            public ulong ParametersFlag
            {
                get { return paramFlag; }
                set
                {
                    paramFlag = value;
                    ParamIndices = Util.GetBitIndices(paramFlag);
                }
            }

            private ulong paramFlag = 0;

            public uint BufferSize;
        }

        private UniformParameter[] ReadParameters(FileReader reader, uint offset, uint count)
        {
            UniformParameter[] values = new UniformParameter[count];

            using (reader.TemporarySeek(offset, SeekOrigin.Begin))
            {
                for (int i = 0; i < count; i++)
                    values[i] = reader.ReadStruct<UniformParameter>();
            }
            return values;
        }

        private uint ReadOffset(FileReader reader)
        {
            long pos = reader.Position;
            return (uint)pos + reader.ReadUInt16();
        }

        public enum Stage
        {
            Vertex,
            Pixel,
        }
    }
}
