using AvaloniaToolbox.Core.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace NextLevelLibrary.LM3
{
    /// <summary>
    /// Represents a table for storing material information for all the materials in the game.
    /// The contents are stored via init.dict
    /// </summary>
    public class MaterialTable
    {
        private static MaterialTable instance;

        [JsonIgnore]
        public static MaterialTable Instance
        {
            get
            {
                if (instance == null)
                {
                    var initPath = Path.Combine(GameSettings.GamePath, "init.dict");
                    var globalPath = Path.Combine(GameSettings.GamePath, "global.dict");

                    if (File.Exists(initPath) && File.Exists(globalPath))
                    {
                        var initDataFile = DictionaryFile.ReadDictionaryData(initPath);
                        var globalDataFile = DictionaryFile.ReadDictionaryData(globalPath);

                        instance = new MaterialTable(initDataFile.Tables[0]);
                        return instance;
                    }
                }
                return instance;
            }
            set { instance = value; }
        }

        public Dictionary<uint, Material> Materials = new Dictionary<uint, Material>();

        public Dictionary<ushort, MaterialParam> Parameters = new Dictionary<ushort, MaterialParam>();

        [JsonIgnore]
        public Dictionary<uint, Shader> Shaders = new Dictionary<uint, Shader>();

        [JsonIgnore]
        public Dictionary<uint, ChunkFileEntry> ShaderDefinitionList = new Dictionary<uint, ChunkFileEntry>();

        public Dictionary<uint, uint> ShaderConstantsRemapTable = new Dictionary<uint, uint>();
        public Dictionary<uint, uint> ShaderRemapTable = new Dictionary<uint, uint>();

        public MaterialParam GetParameter(string name)
        {
            foreach (var param in Parameters.Values)
            {
                if (param.Name.String == name)
                    return param;
            }
            return null;
        }

        public MaterialTable(IChunkTable table)
        {
            //3 primary sections, shaders, parameters, and material information

            //Load material parameters
            foreach (ChunkFileEntry file in table.Files.Where(x => x is ChunkFileEntry))
            {
                if (file.Type == ChunkType.MaterialParams)
                {
                    MaterialParam param = new MaterialParam(file);
                    Parameters.Add(param.ID, param);
                }
                if (file.Type == ChunkType.ShaderConstants)
                {
                    ShaderDefinitionList.Add(file.FilePath.Value, file);
                }
                if (file.Type == ChunkType.Shaders)
                {
                    Shader shader = new Shader(file);
                    Shaders.Add(file.FilePath.Value, shader);
                }
            }

            ShaderRemapTable = LoadShaderTable(table.Files.FirstOrDefault(x => x.Type == (ChunkType)0xB403));
            ShaderConstantsRemapTable = LoadShaderTable(table.Files.FirstOrDefault(x => x.Type == (ChunkType)0xB405));

            //Load material info
            foreach (var file in table.Files)
            {
                if (file.Type == ChunkType.MaterialShaders && file is ChunkFileEntry f)
                    Materials.Add(f.FilePath.Value, new Material(f, this));
            }

            List<uint> hashes = new List<uint>();
            foreach (var mat in Materials.Values)
            {
                foreach (var var in mat.Variations.Values)
                {
                    hashes.Add(var.Name.Value);

                    hashes.Add(var.VertexShader.Value);
                    hashes.Add(var.PixelShader.Value);
                    hashes.Add(var.GeomShaderHash.Value);
                    hashes.Add(var.ShaderComputeHash.Value);
                    hashes.Add(var.TessShaderAHash.Value);
                    hashes.Add(var.TessShaderBHash.Value);
                    hashes.Add(var.AltVertexShader.Value);
                }
            }

            foreach (var shd in Shaders)
                hashes.Add(shd.Key);

            foreach (var shd in Parameters)
                hashes.Add(shd.Value.Name.Value);

            foreach (var shd in ShaderDefinitionList)
                hashes.Add(shd.Key);

            hashes = hashes.Distinct().ToList();
/*
            using (var writer = new StreamWriter("info.txt"))
            {
                foreach (var mat in Materials.Values)
                {
                    writer.WriteLine($"{mat.Name}-----------------------------------------");

                    List<int> totalIndices = new List<int>();

                    foreach (var var in mat.Variations.Values)
                    {
                        writer.WriteLine($"Var {var.Name}-----------------------------------------");

                        List<string> parameters = new List<string>();
                        foreach (var param in var.MaterialParameterOffsets)
                        {
                            if (this.Parameters.ContainsKey(param))
                                parameters.Add(this.Parameters[param].Name.String);
                        }

                        int id = 0;
                        foreach (var param in parameters)
                            writer.WriteLine($"{id++} {param}");

                        var vertexShaderHash = GetShaderHash(var.VertexShader);
                        var pixelShaderHash = GetShaderHash(var.PixelShader);
                        if (this.ShaderDefinitionList.ContainsKey(vertexShaderHash))
                        {
                            ShaderDefinitions def = new ShaderDefinitions(this.ShaderDefinitionList[vertexShaderHash]);
                            writer.WriteLine($"Vertex Indices {string.Join(",", def.GetParameterIndices())}");

                            totalIndices.AddRange(def.GetParameterIndices());
                        }
                        if (this.ShaderDefinitionList.ContainsKey(pixelShaderHash))
                        {
                            ShaderDefinitions def = new ShaderDefinitions(this.ShaderDefinitionList[pixelShaderHash]);
                            writer.WriteLine($"Pixel Indices {string.Join(",", def.GetParameterIndices())}");

                            totalIndices.AddRange(def.GetParameterIndices());
                        }
                    }

                    totalIndices = totalIndices.Distinct().ToList();
                    writer.WriteLine($"All Param Indices {string.Join(",", totalIndices)}");
                }
            }
            */
            Console.WriteLine();
        }

        public uint GetShaderHash(HashString hashString)
        {
            if (this.Shaders.ContainsKey(hashString.Value)) return hashString.Value;

            if (ShaderRemapTable.ContainsKey(hashString.Value))
            {
                return ShaderRemapTable[hashString.Value];
            }
            return hashString.Value;
        }

        public uint GetShaderConstantHash(HashString hashString)
        {
            if (ShaderConstantsRemapTable.ContainsKey(hashString.Value))
            {
                return ShaderConstantsRemapTable[hashString.Value];
            }
            return hashString.Value;
        }

        public static Dictionary<uint, uint> LoadShaderTable(ChunkEntry entry)
        {
            Dictionary<uint, uint> values = new Dictionary<uint, uint>();
            using (var reader = new FileReader(entry.Data, true))
            {
                while (!reader.EndOfStream)
                {
                    uint hash = reader.ReadUInt32();
                    uint shaderTarget = reader.ReadUInt32();

                    values.Add(hash, shaderTarget);
                }
            }
            return values;
        }

        public MaterialData CreateMaterial(ModelFormat model, ModelFormat.MeshInfo mesh) {
            return new MaterialData(this, model, mesh);
        }

        public MaterialData CreateMaterial(uint materialHash)
        {
            return new MaterialData(this, materialHash);
        }

        public class MaterialData
        {
            public const int DEFAULT_FORWARD_PASS = 0;
            public const int DIFFUSE_PASS = 5;
            public const int DIRECT_PASS = 6;
            public const int EMISSIVE_PASS = 22;
            public const int GBUFFER_PASS = 16;

            public MaterialTable Table;

            public Dictionary<int, MaterialParam> Parameters = new Dictionary<int, MaterialParam>();

            public List<MaterialVariationData> Variations = new List<MaterialVariationData>();
            public Material Material;

            public List<Sampler> Samplers = new List<Sampler>();
            public List<Sampler> BakemapSamplers = new List<Sampler>();

            //Loaded uniform values that are not global
            //These parameters are usually 4, 8, 12, or 16 bytes long
            public List<Uniform> Uniforms = new List<Uniform>();

            public string Name;

            public bool IsVisible = true;

            public MaterialData(MaterialTable table, uint materialHash)
            {
                Name = Hashing.GetString(materialHash);
                Table = table;
                Material = table.Materials[materialHash];

                Init(table);
                LoadSamplers();
                LoadUniforms();

                foreach (var var in Material.Variations.Values)
                    this.Variations.Add(new MaterialVariationData(table, var));
            }

            public MaterialData(MaterialTable table, ModelFormat model, ModelFormat.MeshInfo mesh)
            {
                Name = mesh.Material.Name;
                Table = table;
                Material = table.Materials[mesh.Header.MaterialHash];

                Console.WriteLine($"Loading {Material.Name}");

                Init(table);
                SetParametersByModel(model, mesh.Material);
                LoadSamplers();
                LoadUniforms();

                foreach (var var in Material.Variations.Values)
                    this.Variations.Add(new MaterialVariationData(table, var));
            }


            public MaterialParam TryGetParameter(ushort offset)
            {
                foreach (var param in Parameters.Values)
                {
                    if (param.IsSet && param.ID == offset)
                        return param;
                }
                return null;
            }

            private void LoadUniforms()
            {
                foreach (var param in Parameters.Values)
                {
                    //Skip texture parameters
                    if (!param.IsSet || param.Name.String.EndsWith($"{Name}/textures") || param.Name.String.EndsWith("lightmaptextures"))
                        continue;

                    //Parse data as floats
                    using (var reader = new FileReader(param.Data))
                    {
                        Uniforms.Add(new Uniform()
                        {
                            Parameter = param,
                            Value = reader.ReadSingles((int)reader.BaseStream.Length / 4),
                        });
                    }
                }
            }

            private void LoadSamplers()
            {
                foreach (var param in Parameters.Values)
                {
                    if (param.Name.String.EndsWith($"{Name}/textures"))
                    {
                        uint count = (uint)param.Data.Length / 12;
                        using (var reader = new FileReader(param.Data))
                        {
                            for (int i = 0; i < count; i++)
                            {
                                Sampler sampler = new Sampler();
                                sampler.TextureHash = reader.ReadUInt32();
                                sampler.WrapFlags = reader.ReadUInt32();
                                sampler.FilterFlags = reader.ReadUInt32();
                                Samplers.Add(sampler);

                                Console.WriteLine(Hashing.GetString(sampler.TextureHash));
                            }
                        }
                    }
                    if (param.Name.String.EndsWith("lightmaptextures"))
                    {
                        uint count = (uint)param.Data.Length / 12;
                        using (var reader = new FileReader(param.Data))
                        {
                            for (int i = 0; i < count; i++)
                            {
                                Sampler sampler = new Sampler();
                                sampler.TextureHash = reader.ReadUInt32();
                                sampler.WrapFlags = reader.ReadUInt32();
                                sampler.FilterFlags = reader.ReadUInt32();
                                BakemapSamplers.Add(sampler);
                            }
                        }
                    }
                }
            }

            public class Sampler
            {
                public uint TextureHash;
                public uint WrapFlags;
                public uint FilterFlags;
            }

            public class Uniform
            {
                public string Name => Parameter.Name.String;
                public dynamic Value;
                public MaterialParam Parameter;

                public void Update()
                {
                    var mem = new MemoryStream();
                    using (var writer = new FileWriter(mem))
                    {
                        foreach (float value in Value)
                            writer.Write(value);
                    }
                    Parameter.Data = mem.ToArray();
                }
            }

            private void Init(MaterialTable table)
            {
                //Offsets that get the parameter
                foreach (var paramOffset in Material.ParameterIndices)
                {
                    if (!table.Parameters.ContainsKey(paramOffset.Key))
                        continue;

                    var param = table.Parameters[paramOffset.Key];

                    //Get the lookup index
                    int lookupIndex = Material.ParameterIndices[paramOffset.Key];
                    //Set a new parameter instance so we can update the param data with the model parameters
                    Parameters.Add(lookupIndex, new MaterialParam()
                    {
                        IsSet = false,
                        ID = param.ID,
                        Name = param.Name,
                        Data = param.Data,
                    });
                }
            }

            public void SetParametersByModel(ModelFormat model, ModelFormat.MaterialData mat)
            {
                //Material stream
                var materialBuffer = model.File.GetChild(ChunkType.MaterialData).Data;
                using (var reader = new FileReader(materialBuffer, true))
                {
                    //Parameters link to each value via index
                    foreach (var param in this.Parameters)
                    {
                        //The raw data offset in the model's material buffer
                        var paramDataOffset = mat.Lookup[param.Key];
                        //If offset is -1, it isn't used and uses default value
                        if (paramDataOffset == 0 || paramDataOffset == uint.MaxValue)
                            continue;

                        //get expected length from original param data
                        var length = param.Value.Data.Length;

                        //Read and set the new param data value
                        reader.SeekBegin(paramDataOffset);
                        param.Value.Data = reader.ReadBytes(length);
                        param.Value.IsSet = true;
                    }
                }
            }
        }

        public class MaterialVariationData
        {
            public Material.MaterialVariation MaterialVariation;

            //Shader data
            public Shader VertexShaderFile;
            public Shader PixelShaderFile;

            public ShaderDefinitions VertexShaderDefinition { get; set; }
            public ShaderDefinitions PixelShaderDefinition { get; set; }

            public byte[] ShaderConstants { get; set; }

            public int VertexDataIndex = -1;
            public int PixelDataIndex = -1;

            public MaterialVariationData(MaterialTable table, Material.MaterialVariation variation)
            {
                MaterialVariation = variation;

                //Get the shader hash
                var vertexShaderHash = table.GetShaderHash(variation.VertexShader);
                var pixelShaderHash = table.GetShaderHash(variation.PixelShader);
                var constShaderHash = table.GetShaderConstantHash(variation.AltVertexShader);

                //Get the shader
                if (table.Shaders.ContainsKey(vertexShaderHash))
                    VertexShaderFile = table.Shaders[vertexShaderHash];

                if (table.Shaders.ContainsKey(pixelShaderHash))
                    PixelShaderFile = table.Shaders[pixelShaderHash];

                if (table.ShaderDefinitionList.ContainsKey(vertexShaderHash))
                {
                    var data = table.ShaderDefinitionList[vertexShaderHash];
                    VertexShaderDefinition = new ShaderDefinitions(data);
                }
                if (table.ShaderDefinitionList.ContainsKey(pixelShaderHash))
                {
                    var data = table.ShaderDefinitionList[pixelShaderHash];
                    PixelShaderDefinition = new ShaderDefinitions(data);
                }

                //Load constants
                if (table.ShaderDefinitionList.ContainsKey(constShaderHash))
                {
                    var shaderConstantsHash = table.ShaderDefinitionList[constShaderHash];
                    ShaderConstants = shaderConstantsHash.ReadBytes((uint)shaderConstantsHash.Data.Length);
                }
            }
        }
    }
}
