using AvaloniaToolbox.Core.IO;
using NextLevelLibrary.Files.LM2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Toolbox.Core;

namespace NextLevelLibrary.LM2
{
    public class ModelFormat_LM2HD : IFormat
    {
        const uint MODEL_HEADER_SIZE = 12;
        const uint MESH_SIZE = 64;

        public List<ModelInfo> Models = new List<ModelInfo>();

        /// <summary>
        /// Represents a model instance that stores mesh, material, and matrices.
        /// </summary>
        public class ModelInfo
        {
            /// <summary>
            /// The hash used to identify the model.
            /// </summary>
            public uint Hash { get; set; }

            /// <summary>
            /// Binding information for finding what bones are assigned to mesh vertices.
            /// </summary>
            public SkinControllerLM2HD SkinController = new SkinControllerLM2HD();

            /// <summary>
            /// 
            /// </summary>
            public BoundingBox BoundingBox { get; set; }

            /// <summary>
            /// 
            /// </summary>
            public float BoundingRadius { get; set; }

            /// <summary>
            /// The mesh data of the model.
            /// </summary>
            public List<MeshInfo> Meshes = new List<MeshInfo>();
        }

        /// <summary>
        /// Represents a mesh instance used to store vertex and index information.
        /// </summary>
        public class MeshInfo
        {
            /// <summary>
            /// The raw mesh header.
            /// </summary>
            public Mesh Header { get; set; }

            /// <summary>
            /// The transform of the model in world space.
            /// </summary>
            public Matrix4x4 Transform { get; set; }

            /// <summary>
            /// The raw material data used via look up table
            /// </summary>
            public MaterialData Material { get; set; }

            /// <summary>
            /// The vertex buffer position in the buffer chunk.
            /// </summary>
            internal uint VertexBufferPointer { get; set; }

            internal List<uint> PointerList = new List<uint>();
            internal List<uint> PointerSizes = new List<uint>();

            internal uint VertexBufferSize;

            public uint[] Faces { get; set; }

            public List<Vertex> Vertices = new List<Vertex>();

            public MeshInfo(Mesh mesh) { Header = mesh; }

            public MeshInfo()
            {
                Header = new Mesh();
                Material = new MaterialData();
                Transform = new Matrix4x4();
                Faces = new uint[0];
            }
        }

        public class MaterialData
        {
            public string Name { get; set; }

            public uint DiffuseTexHash;
            public uint SpecmapTexHash;
            public uint LightmapTexHash;
            public uint ShadowTexHash;
            public uint NormalTexHash;

            public List<uint> TextureHashes = new List<uint>();

            public MaterialValue[] Layout { get; set; }

            public Dictionary<int, byte[]> Parameters = new Dictionary<int, byte[]>();

            public MaterialData()
            {
                Layout = new MaterialValue[0];
            }
        }

        public class MaterialValue
        {
            public int Index;

            public string Label = "";

            public string Type;

            public byte[] Value;

            public string Data;

            public int Size;

            public uint LookupPointer = uint.MaxValue;

            public MaterialValue() { }

            public MaterialValue(int slot, string type, byte[] value, uint pointer)
            {
                LookupPointer = pointer;
                Index = slot;
                Type = type;
                Value = value;
                Size = value.Length;
                Data = Convert.ToHexString(Value);
            }

            public MaterialValue(int slot, string type)
            {
                Index = slot;
                Type = type;
                Value = new byte[0];
            }
        }

        public SkeletonFormat SkeletonFormat;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class BoundingBox
        {
            public float MinX;
            public float MinY;
            public float MinZ;
            public float MaxX;
            public float MaxY;
            public float MaxZ;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Header
        {
            public uint Hash;
            public uint MeshCount;
            public uint Padding1; //Always 0
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Mesh
        {
            public uint IndexOffset;
            public uint IndexCount;
            public uint BufferPointerOffset; //Pointer to vertex buffer pointer offset
            public uint SubMaterialHash;
            public uint MaterialHash;
            public uint TransformMatrixIndex = 0; //Index to model matrix section
            public uint Padding; //Always 0
            public uint MaterialLookupIndex; //Multiply by 4 to get offset in table
            public uint VertexCount;
            public uint Flag = 15663360;
            public uint HashName;
            //Always 0, align 32
            public uint Padding2;
            public uint Padding3;
            public uint Padding4;
            public uint Padding5;
            public uint Padding6;
        }

        public ModelFormat_LM2HD(ChunkFileEntry fileChunk, SkeletonFormat skeleton)
        {
            SkeletonFormat = skeleton;
            var chunkList = fileChunk.Children;

            List<Header> modelHeaders = new List<Header>();
            List<MeshInfo> meshes = new List<MeshInfo>();
            List<Matrix4x4> modelMatrices = new List<Matrix4x4>();
            List<BoundingBox> boundingBoxes = new List<BoundingBox>();
            List<float> boundingRadius = new List<float>();
            List<SkinControllerLM2HD> skinBindings = new List<SkinControllerLM2HD>();

            var vertexPointerChunk = fileChunk.GetChild(ChunkType.VertexStartPointers);
            var bufferChunk = fileChunk.GetChild(ChunkType.MeshBuffers);
            //Get the amount of models to prepare
            var modelChunk = fileChunk.GetChild(ChunkType.ModelInfo);
            uint numModels = (uint)modelChunk.Data.Length / MODEL_HEADER_SIZE;

            //Empty model, skip
            if (bufferChunk.Data.Length == 0)
                return;

            List<uint> vertexPointers = new List<uint>();

            using (var reader = new FileReader(vertexPointerChunk.Data, true))
            {
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    uint ofs = reader.ReadUInt32();
                    vertexPointers.Add(ofs);
                }
            }

            for (int i = 0; i < chunkList.Count; i++)
            {
                var chunk = chunkList[i];
                switch (chunk.Type)
                {
                    case ChunkType.ModelInfo:
                        modelHeaders = chunk.ReadStructs<Header>(numModels);
                        break;
                    case ChunkType.BoundingBox:
                        //Check if valid size
                        if (chunk.Data.Length != numModels * 24) throw new Exception("Unexpected bounding box size!");
                        //Parse bounding boxes
                        boundingBoxes = chunk.ReadStructs<BoundingBox>(numModels);
                        break;
                    case ChunkType.BoundingRadius:
                        uint numRadius = (uint)chunk.Data.Length / 4;
                        //Check if valid size
                        if (chunk.Data.Length != numModels * 4) throw new Exception("Unexpected bounding radius size!");
                        //Parse bounding radius
                        boundingRadius.AddRange(chunk.ReadPrimitive<float>(numModels));
                        break;
                    case ChunkType.ModelTransform:
                        using (var reader = new FileReader(chunk.Data, true))
                        {
                            //Parse model matrices. These are indexed by the meshes
                            for (int j = 0; j < (int)chunk.Data.Length / 64; j++)
                            {
                                modelMatrices.Add(new Matrix4x4(
                                    reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                                    reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                                    reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                                    reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
                            }
                        }
                        break;
                    case ChunkType.MeshInfo:
                        uint numMeshes = (uint)chunk.Data.Length / MESH_SIZE;
                        var meshHeaders = chunk.ReadStructs<Mesh>(numMeshes);
                        //Load mesh structs into mesh info
                        //This will store vertex and face data
                        for (int m = 0; m < numMeshes; m++)
                        {
                            var mesh = new MeshInfo(meshHeaders[m]);
                            meshes.Add(mesh);
                        }

                        //Read vertex points. These must be parsed by mesh header data
                        using (var reader = new FileReader(vertexPointerChunk.Data, true))
                        {
                            //check number of pointers
                            for (int m = 0; m < numMeshes; m++)
                            {
                                int idx = (int)meshes[m].Header.BufferPointerOffset / 4;
                                int end_idx = vertexPointers.Count;

                                if (m < numMeshes - 1)
                                    end_idx = (int)meshes[m + 1].Header.BufferPointerOffset / 4;

                                for (int j = idx; j < end_idx; j++)
                                {
                                    meshes[m].PointerList.Add(vertexPointers[j]);

                                    if (end_idx == vertexPointers.Count)
                                        meshes[m].PointerSizes.Add(meshes[0].Header.IndexOffset - vertexPointers[j]);
                                    else 
                                        meshes[m].PointerSizes.Add(vertexPointers[j + 1] - vertexPointers[j]);
                                }
                            }

                            for (int m = 0; m < numMeshes; m++)
                            {
                                meshes[m].VertexBufferPointer = meshes[m].PointerList.LastOrDefault();
                                meshes[m].VertexBufferSize = meshes[m].PointerSizes.LastOrDefault();
                            }
                        }
                        break;
                    case ChunkType.SkinControllerStart:
                        skinBindings.Add(new SkinControllerLM2HD(chunk));
                        break;
                    case ChunkType.MaterialData: break;
                    case ChunkType.MaterialLookupTable: break;
                    case ChunkType.MeshBuffers: break;
                    case ChunkType.VertexStartPointers: break;
                    default:
                        throw new Exception($"Unsupported model chunk {chunk.Type}!");
                }
            }

            //Assign all the model and mesh data to the format
            int meshIndex = 0;
            for (int i = 0; i < modelHeaders.Count; i++)
            {
                var model = new ModelInfo() { Hash = modelHeaders[i].Hash };
                Models.Add(model);

                //Skin bindings assign via hash
                model.SkinController = skinBindings.FirstOrDefault(x => x.ModelHash == model.Hash);
                //Bounding and transform data of model.
                if (boundingBoxes.Count > 0)
                    model.BoundingBox = boundingBoxes[i];
                if (boundingRadius.Count > 0)
                    model.BoundingRadius = boundingRadius[i];
                //Mesh data
                for (int j = 0; j < modelHeaders[i].MeshCount; j++)
                {
                    var mesh = meshes[meshIndex + j];
                    model.Meshes.Add(mesh);
                    //Assign the model matrix
                    if (modelMatrices.Count > 0)
                        mesh.Transform = modelMatrices[(int)mesh.Header.TransformMatrixIndex];
                }
                meshIndex += (int)modelHeaders[i].MeshCount;
            }

            var materialLookupChunk = fileChunk.GetChild(ChunkType.MaterialLookupTable);
            var materialChunk = fileChunk.GetChild(ChunkType.MaterialData);

            //Read materials
            // MaterialLoaderHelper.ParseMaterials(materialChunk.Data, materialLookupChunk.Data, meshes);
            LoadMaterialData(fileChunk, Models);

            //Read vertex and face data
            BufferHelperLM2HD.ReadBufferData(bufferChunk.Data, Models, skeleton, vertexPointers);
        }

        public static Dictionary<string, string> MaterialInfo = new Dictionary<string, string>();

        private void LoadMaterialData(ChunkFileEntry fileChunk, List<ModelInfo> models)
        {
            var materialLookupChunk = fileChunk.GetChild(ChunkType.MaterialLookupTable);
            var materialChunk = fileChunk.GetChild(ChunkType.MaterialData);

            var meshes = models.SelectMany(x => x.Meshes).ToList();
            List<uint[]> materialPointers = GetLookupPointers(meshes, materialLookupChunk.Data);
            List<uint> usedPointers = materialPointers.SelectMany(x => x.Where(x => x != uint.MaxValue)).ToList();

            usedPointers.Add((uint)materialChunk.Data.Length);
            usedPointers = usedPointers.OrderBy(x => x).Distinct().ToList();

            //Get raw data
            using (var materialReader = new FileReader(materialChunk.Data, true))
            {
                for (int i = 0; i < meshes.Count; i++)
                {
                    var mat = new MaterialData();
                    mat.Name = Hashing.GetString(meshes[i].Header.MaterialHash);
                    meshes[i].Material = mat;

                    MaterialHelperLM2HD.ReadMaterialParam(materialReader, mat.Name,
                        materialPointers[i], mat);

                    string info = "";

                    int slot_idx = 0;
                    int tex_idx = 0;
                    foreach (var pointer in materialPointers[i])
                    {
                        slot_idx++;

                        //NULL
                        if (pointer == uint.MaxValue)
                            continue;

                        var index = usedPointers.IndexOf(pointer);
                        var endPointer = usedPointers[index + 1];
                        var datasize = endPointer - pointer;

                        materialReader.SeekBegin(pointer);
                        var data = materialReader.ReadBytes((int)datasize);

                        string type = "PROPERTY";

                        materialReader.SeekBegin(pointer);
                        //search for textures
                        while (materialReader.BaseStream.Position < pointer + datasize)
                        {
                            uint h = materialReader.ReadUInt32();
                            if (Hashing.HashStrings.ContainsKey(h))
                                mat.TextureHashes.Add(h);
                        }
                        mat.Parameters.Add(slot_idx - 1, data);
                    }

                    if (!MaterialInfo.ContainsKey(mat.Name))
                        MaterialInfo.Add(mat.Name, info);
                }
            }
        }

        /// <summary>
        /// Gets the total amount of pointers used for each mesh's material.
        /// </summary>
        /// <returns></returns>
        static List<uint[]> GetLookupPointers(List<MeshInfo> meshes, Stream lookupStream)
        {
            //Grab all the lookup indices
            List<uint> indices = meshes.Select(x => x.Header.MaterialLookupIndex).ToList();

            //Create an array of pointer counts
            uint[] lookupSizes = new uint[indices.Count];
            for (int i = 0; i < indices.Count; i++)
            {
                if (i + 1 < indices.Count)
                    lookupSizes[i] = indices[i + 1] - indices[i];
                else
                    lookupSizes[i] = (uint)(lookupStream.Length / 4) - indices[i];
            }

            List<uint[]> pointers = new List<uint[]>();
            using (var lookupReader = new FileReader(lookupStream, true))
            {
                for (int i = 0; i < meshes.Count; i++)
                {
                    lookupReader.SeekBegin(meshes[i].Header.MaterialLookupIndex * 4);
                    pointers.Add(lookupReader.ReadUInt32s((int)lookupSizes[i]));
                }
            }

            return pointers;
        }

        public void Save(ChunkFileEntry fileEntry)
        {
        }
    }
}
