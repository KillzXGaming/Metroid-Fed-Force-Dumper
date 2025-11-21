using AvaloniaToolbox.Core.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;

namespace NextLevelLibrary.LM3
{
    public class Material : IFormat
    {
        private MaterialTable Table;

        /// <summary>
        /// The name of the material.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// A list of material variations, including different depth, alpha and translucent states.
        /// </summary>
        public Dictionary<uint, MaterialVariation> Variations = new Dictionary<uint, MaterialVariation>();

        /// <summary>
        /// A list of parameter offsets and the index to what lookup slot to use in the model material data.
        /// </summary>
        public Dictionary<ushort, ushort> ParameterIndices = new Dictionary<ushort, ushort>();

        /// <summary>
        /// A list of offsets for the parameters and the index to the parameter.
        /// These seem to represent the order, possibly for loading into a buffer?
        /// </summary>
        public Dictionary<ushort, ushort> ParameterOrderOffsets = new Dictionary<ushort, ushort>();

        /// <summary>
        /// A list of indices with an unknown purpose. Usually 7, 8, 9, 10
        /// </summary>
        public Dictionary<ushort, ushort> AttributeIndices = new Dictionary<ushort, ushort>();

        /// <summary>
        /// A list of indices for mapping the variation draw order
        /// </summary>
        public List<ushort> VariationIDs = new List<ushort>();

        /// <summary>
        /// Fixed set of 33 slots for render order purposes. Transparency is always first
        /// </summary>
        public List<ushort> VariationOrderIDs = new List<ushort>();

        /// <summary>
        /// A list of rasterization states that materials can index to use.
        /// </summary>
        public List<RasterizerConfig> RasterizerConfigs = new List<RasterizerConfig>();

        /// <summary>
        /// A list of depth states that materials can index to use.
        /// </summary>
        public List<DepthConfig> DepthConfigs = new List<DepthConfig>();

        /// <summary>
        /// A list of blend states that materials can index to use.
        /// </summary>
        public List<BlendConfig> BlendConfigs = new List<BlendConfig>();

        public Header MaterialShaderHeader;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Header
        {
            public ushort MaterialIndex;
            public ushort ParameterCount;
            public ushort NumB32B;
            public uint VariationStartID;
            public ushort VariationsCount;
        }

        public string GetParameter(ushort id)
        {
            var offset = this.ParameterIndices.FirstOrDefault(x => x.Value == id).Key;

            if (Table.Parameters.ContainsKey(offset))
                return Table.Parameters[offset].Name.String;
            return "";
        }

        public Material(ChunkFileEntry fileEntry, MaterialTable table = null)
        {
            Table = table;

            var rasterChunks = fileEntry.GetChildList((ChunkType)0xB321);
            var depthChunks = fileEntry.GetChildList((ChunkType)0xB322);
            var blendChunks = fileEntry.GetChildList((ChunkType)0xB323);

            foreach (var chunk in rasterChunks)
                RasterizerConfigs.Add(new RasterizerConfig(chunk));
            foreach (var chunk in depthChunks)
                DepthConfigs.Add(new DepthConfig(chunk));
            foreach (var chunk in blendChunks)
                BlendConfigs.Add(new BlendConfig(chunk));

            var nameChunk = fileEntry.GetChild((ChunkType)0xB326);
            var headerChunk = fileEntry.GetChild((ChunkType)0xB325);

            MaterialShaderHeader = headerChunk.ReadStruct<Header>();

            using (var reader = new FileReader(nameChunk.Data, true)) {
                Name = reader.ReadStringZeroTerminated();
            }

            //Parameter indices/offsets
            var paramIndicesChunk = fileEntry.GetChild((ChunkType)0xB327);
            var paramOffsetsChunk = fileEntry.GetChild((ChunkType)0xB328);

            //Attribute indices
            var attributeIndicesChunk = fileEntry.GetChild((ChunkType)0xB329);
            var attributeOffsetsChunk = fileEntry.GetChild((ChunkType)0xB32A);

            var variationOffsetsChunk = fileEntry.GetChild((ChunkType)0xB32D);
            var variationDrawOrderChunk = fileEntry.GetChild((ChunkType)0xB32E);

            //Unsure how this gets used. Seems to be removed in later versions and isn't important
            var indexBuffer2Chunk = fileEntry.GetChild((ChunkType)0xB32B);
            if (indexBuffer2Chunk != null)
            {
                using (var reader = new FileReader(indexBuffer2Chunk.Data, true))
                {
                    while (!reader.EndOfStream)
                    {
                        byte index = reader.ReadByte();
                        if (index != 255)
                            ParameterOrderOffsets.Add((ushort)reader.Position, index);
                    }
                }
            }

            if (paramOffsetsChunk != null)
            {
                var offsets = paramOffsetsChunk.ReadUShortList((uint)paramOffsetsChunk.Data.Length / 2).ToList();
                using (var reader = new FileReader(paramIndicesChunk.Data, true))
                {
                    foreach (var offset in offsets)
                    {
                        reader.SeekBegin(offset);
                        ParameterIndices.Add(offset, reader.ReadByte());
                    }
                }
            }

            if (attributeOffsetsChunk != null)
            {
                var paramOffsets = attributeOffsetsChunk.ReadBytes((uint)attributeOffsetsChunk.Data.Length);
                using (var reader = new FileReader(attributeIndicesChunk.Data, true))
                {
                    foreach (var offset in paramOffsets)
                    {
                        reader.SeekBegin(offset);
                        AttributeIndices.Add(reader.ReadByte(), offset);
                    }
                }
            }

            var shaderProgramChunks = fileEntry.GetChildList((ChunkType)0xB330);
            foreach (var prog in shaderProgramChunks)
            {
                var shaderProgram = new MaterialVariation(this, prog);
                Variations.Add(shaderProgram.Name.Value, shaderProgram);
            }

            if (variationOffsetsChunk != null)
            {
                VariationIDs = variationOffsetsChunk.ReadUShortList((uint)variationOffsetsChunk.Data.Length / 2).ToList();
            }
            if (variationDrawOrderChunk != null)
            {
                VariationOrderIDs = variationDrawOrderChunk.ReadUShortList((uint)variationDrawOrderChunk.Data.Length / 2).ToList();
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            using (var writer = new StringWriter(sb))
            {
                writer.WriteLine($"Header MaterialIndex {this.MaterialShaderHeader.MaterialIndex}");
                writer.WriteLine($"Header ParameterCount {this.MaterialShaderHeader.ParameterCount}");
                writer.WriteLine($"Header NumB32B {this.MaterialShaderHeader.NumB32B}");
                writer.WriteLine($"Header VariationStartID {this.MaterialShaderHeader.VariationStartID}");
                writer.WriteLine($"Header VariationsCount {this.MaterialShaderHeader.VariationsCount}");

                foreach (var att in AttributeIndices)
                    writer.WriteLine($"AttributeIndices {att.Key} {att.Value}");

                List<MaterialParam> paramList = new List<MaterialParam>();
                foreach (var var in this.ParameterIndices.Keys)
                {
                    if (Table.Parameters.ContainsKey(var))
                        paramList.Add(Table.Parameters[var]);
                }

                foreach (var param in paramList.OrderBy(x => x.ID))
                    writer.WriteLine($"Param {param.Name}");


                foreach (var var in this.Variations.Values)
                {
                    writer.Write($"Header {var.Name.String} ");

                    if (var.BlendConfigIndex != -1)
                    {
                        var blend = this.BlendConfigs[var.BlendConfigIndex];
                        writer.Write($" blend {blend.BlendUsage} {blend.ColorSrc} {blend.ColorDst}");
                    }
                    if (var.DepthConfigIndex != -1)
                    {
                        var depth = this.DepthConfigs[var.DepthConfigIndex];
                        writer.Write($" depth {depth.Setting}");
                    }
                    writer.WriteLine();
                }

                return sb.ToString();
            }
        }

        public class RasterizerConfig
        {
            public RasterizerConfig(ChunkEntry chunkEntry)
            {
                using (var reader = new FileReader(chunkEntry.Data, true))
                {
                }
            }
        }

        public class DepthConfig
        {
            public Flag Setting = Flag.DepthTestLEqual;

            public DepthConfig() { }

            public DepthConfig(ChunkEntry chunkEntry)
            {
                using (var reader = new FileReader(chunkEntry.Data, true))
                {
                    Setting = reader.ReadEnum<Flag>(false);
                }
            }

            public enum Flag : ushort
            {
                DepthTestWrite = 0x7, //Depth test, depth write 
                NoDepth = 0x8, //No depth test, no write, no func
                DepthTestNoWriteLess = 0x5, //Depth test, no write, less
                DepthTestNoWrite = 0x9, //Depth test, no write
                DepthTestEqual = 15, //Depth test, write, equal
                DepthTestLEqual = 0, //Depth test, write, lequal
            }
        }

        public class BlendConfig
        {
            public Blend BlendUsage = Blend.None;
            public BlendingFactor ColorSrc = BlendingFactor.SrcAlpha;
            public BlendingFactor ColorDst = BlendingFactor.One;

            public BlendingFactor AlphaSrc = BlendingFactor.SrcAlpha;
            public BlendingFactor AlphaDst = BlendingFactor.One;

            public BlendConfig() { }

            public BlendConfig(ChunkEntry chunkEntry)
            {
                using (var reader = new FileReader(chunkEntry.Data, true))
                {
                    BlendUsage = (Blend)reader.ReadByte();
                    byte factor = reader.ReadByte();
                    ColorDst = (BlendingFactor)(byte)(factor >> 4);
                    ColorSrc = (BlendingFactor)(byte)(factor & 0xf);
                    byte factor2 = reader.ReadByte();
                    AlphaDst = (BlendingFactor)(byte)(factor2 >> 4);
                    AlphaSrc = (BlendingFactor)(byte)(factor2 & 0xf);
                }
            }

            public enum Blend : ushort
            {
                None = 0,
                Blend = 1,
                Seperate = 3, //Uses second factor value
            }

            public enum BlendingFactor : byte
            {
                Zero = 0,
                One = 1,
                SrcAlpha = 2,
                DstAlpha = 3,
                OneMinusSrcAlpha = 4, //Todo this can go up to 9 values but usually just these 5
            }
        }

        public class MaterialVariation
        {
            /// <summary>
            /// The hash name of the material variant. 
            /// </summary>
            public HashString Name; //Program name match from the mesh header

            /// <summary>
            /// The raw variation ID/index used for ordering
            /// </summary>
            public ushort VariationID;

            /// <summary>
            /// The hash name of the vertex shader. 
            /// </summary>
            public HashString VertexShader = new HashString(uint.MaxValue);

            /// <summary>
            /// The hash name of the pixel shader. 
            /// </summary>
            public HashString PixelShader = new HashString(uint.MaxValue);

            /// <summary>
            /// The hash name of the tessellation shader. 
            /// </summary>
            public HashString TessShaderAHash = new HashString(uint.MaxValue);

            /// <summary>
            /// The hash name of the second tessellation shader. 
            /// </summary>
            public HashString TessShaderBHash = new HashString(uint.MaxValue);

            /// <summary>
            /// The hash name of the geometry shader. 
            /// </summary>
            public HashString GeomShaderHash = new HashString(uint.MaxValue);

            /// <summary>
            /// The hash name of the compute shader. 
            /// </summary>
            public HashString ShaderComputeHash = new HashString(uint.MaxValue);

            /// <summary>
            /// The hash name of an alternate vertex shader.
            /// </summary>
            public HashString AltVertexShader = new HashString(uint.MaxValue);

            /// <summary>
            /// Parameter offsets for getting parameter data for the material.
            /// </summary>
            public List<ushort> MaterialParameterOffsets = new List<ushort>();

            /// <summary>
            /// Indices with an unknown purpose.
            /// </summary>
            [JsonIgnore]
            public List<byte> Indices = new List<byte>();

            /// <summary>
            /// A flag used for indices, always 0x10
            /// </summary>
            [JsonIgnore]
            public List<byte> Flags = new List<byte>();

            //Render state settings
            public sbyte RasterizerConfigIndex; //B321
            public sbyte BlendConfigIndex; //B323
            public sbyte DepthConfigIndex; //B322

            public Material ParentMaterial;

            public BlendConfig GetBlendState()
            {
                if (BlendConfigIndex == -1) return new BlendConfig();
                return ParentMaterial.BlendConfigs[BlendConfigIndex];
            }

            public DepthConfig GetDepthState()
            {
                if (DepthConfigIndex == -1) return new DepthConfig();
                return ParentMaterial.DepthConfigs[DepthConfigIndex];
            }

            public ushort GetVertexUniformParameterOffset(ushort targetIndex)
            {
                int index = 0;
                foreach (var paramOffset in this.MaterialParameterOffsets)
                {
                    if (this.ParentMaterial.Table.Parameters.ContainsKey(paramOffset))
                    {
                        var p = this.ParentMaterial.Table.Parameters[paramOffset];
                        switch (p.Name.String)
                        {
                            case "lightmapparams/vslightprobeparams": //0
                            case "clipplanes/clipplanes": //1
                            case "objectcenter/center"://2
                            case "matrices/matrices": //3
                            case "lightmaterial/vsparameters": //2
                                if (index == targetIndex)
                                    return p.ID;

                                index++;
                                break;
                        }
                    }
                }
                return ushort.MaxValue;
            }

            public ushort GetPixelUniformParameterOffset(ushort targetIndex)
            {
                string[] notused = new string[]
                {
                    "lightmapparams/vslightprobeparams",
                    "scenedepth/subsurface",
                    "gametime/vertextime",
                    "lightmaterial/vsparameters",
                    "warblecosine/warbleparams",
                };

                int index = 0;
                foreach (var paramOffset in this.MaterialParameterOffsets)
                {
                    if (this.ParentMaterial.Table.Parameters.ContainsKey(paramOffset))
                    {
                        var p = this.ParentMaterial.Table.Parameters[paramOffset];
                        //Skip any unused parameters and if it contains texture data
                        if (notused.Contains(p.Name.String) || p.Name.String.Contains("texture"))
                            continue;
                        //If index is current param, return the parameter
                        if (index == targetIndex)
                            return p.ID;

                        index++;
                    }
                }
                return ushort.MaxValue;
            }

            public string GetVertexUniformParameter(ushort targetIndex)
            {
                ushort offset = GetVertexUniformParameterOffset(targetIndex);
                if (ParentMaterial.Table.Parameters.ContainsKey(offset))
                    return ParentMaterial.Table.Parameters[offset].Name.String;
                return "";
            }

            public string GetPixelUniformParameter(ushort targetIndex)
            {
                ushort offset = GetPixelUniformParameterOffset(targetIndex);
                if (ParentMaterial.Table.Parameters.ContainsKey(offset))
                    return ParentMaterial.Table.Parameters[offset].Name.String;
                return "";
            }

            public MaterialVariation(Material mat, ChunkEntry chunkEntry)
            {
                ParentMaterial = mat;
                var renderState = chunkEntry.GetChild((ChunkType)0xB331);
                var headerChunk = chunkEntry.GetChild((ChunkType)0xB332);
                var parameterOffsetsChunk = chunkEntry.GetChild((ChunkType)0xB333);
                var paramIndices = chunkEntry.GetChild((ChunkType)0xB334);
                var paramFlags = chunkEntry.GetChild((ChunkType)0xB335);
                var hashesChunk = chunkEntry.GetChild((ChunkType)0xB337);

                using (var reader = new FileReader(renderState.Data, true))
                {
                    RasterizerConfigIndex = reader.ReadSByte(); //Polygon/Alpha flags
                    DepthConfigIndex = reader.ReadSByte(); //Depth flags
                    BlendConfigIndex = reader.ReadSByte(); //Blend flags
                }

                byte numParams = 0;
                byte numIndices = 0;

                using (var reader = new FileReader(headerChunk.Data, true))
                {
                    Name = new HashString(reader.ReadUInt32());
                    VariationID = reader.ReadUInt16();
                    numParams = reader.ReadByte();
                    numIndices = reader.ReadByte();
                }

                MaterialParameterOffsets = parameterOffsetsChunk.ReadUShortList(numParams).ToList();
                if (numIndices > 0)
                    Indices = paramIndices.ReadBytes(numIndices).ToList();
                if (paramFlags != null) //Flags which are always 0x10. Used by param indices
                    Flags = paramFlags.ReadBytes(numIndices).ToList();

                using (var reader = new FileReader(hashesChunk.Data, true))
                {
                    uint[] hashes = reader.ReadUInt32s((int)reader.BaseStream.Length / 4);
                    VertexShader = new HashString(hashes[0]); //vertex
                    TessShaderAHash = new HashString(hashes[1]); //tesselation
                    TessShaderBHash = new HashString(hashes[2]); //tesselation
                    GeomShaderHash = new HashString(hashes[3]); //geometry
                    PixelShader = new HashString(hashes[4]); //pixel
                    ShaderComputeHash = new HashString(hashes[5]); //compute
                    if (hashes.Length > 7) //Used for alternate vertex shader with probe data?
                    {
                        AltVertexShader = new HashString(hashes[7]);
                    }
                }
            }
        }
    }
}
