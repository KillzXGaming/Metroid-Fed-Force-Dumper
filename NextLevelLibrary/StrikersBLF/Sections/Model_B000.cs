using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Linq;
using System.IO;
using Toolbox.Core;
using System.Numerics;
using System.Xml.Schema;
using System.Data;
using System.Xml.Serialization;
using System.Drawing.Imaging;
using NextLevelLibrary.LM3;
using AvaloniaToolbox.Core.Mesh;
using AvaloniaToolbox.Core.IO;

namespace NextLevelLibrary.StrikersBLF
{
    public class ModelFormat : IFormat
    {
        const uint MODEL_HEADER_SIZE = 12;
        const uint MESH_SIZE = 0x40;

        public ChunkFileEntry File;

        public List<ModelInfo> Models = new List<ModelInfo>();

        /// <summary>
        /// 
        /// </summary>
        public SkeletonFormat Skeleton { get; set; }

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
            public SkinBinding SkinBinding = new SkinBinding();

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

            public Stream MaterialData { get; set; }

            public Stream BufferData;
        }


        public void ToXML(string filePath)
        {
            XmlSerializer ser = new XmlSerializer(typeof(ModelFormat));

            TextWriter writer = new StreamWriter(filePath);
            ser.Serialize(writer, this);
            writer.Close();
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

            internal uint TexCoordBufferPointer { get; set; }

            internal uint TexCoordStride { get; set; }

            /// <summary>
            /// The skinning buffer position in the buffer chunk.
            /// </summary>
            internal uint SkinningBufferPointer { get; set; }

            /// <summary>
            /// The morph vertex buffer position in the buffer chunk used by morph meshes.
            /// </summary>
            internal uint MorphVertexBufferPointer { get; set; }

            /// <summary>
            /// Determines if there is skinning data present in the mesh.
            /// </summary>
            public bool HasSkinning => Header.SkinningFlags != 0xFFFFFF;

            /// <summary>
            /// Gets the total number of face indices.
            /// </summary>
            public uint IndexCount => Header.IndexFlags & 0xFFFFFF;

            /// <summary>
            /// Gets the format of the face indices as either ubyte (0x8000) or ushort (0).
            /// </summary>
            public uint IndexFormat => Header.IndexFlags >> 24;

            /// <summary>
            /// A list of faces for displaying mesh vertices as triangles.
            /// </summary>
            [XmlIgnore]
            public uint[] Faces { get; set; }

            /// <summary>
            /// A list of vertices of mesh data.
            /// </summary>
            [XmlIgnore]
            public List<GenericVertex> Vertices = new List<GenericVertex>();

            public MeshInfo(Mesh mesh) { Header = mesh; }

            public MeshInfo() { }
        }

        public class MaterialData
        {
            /// <summary>
            /// The material preset name configured by hash.
            /// </summary>
            public string Name { get; set; }
            public uint DiffuseHash { get; set; }
            public uint NormalMapHash { get; set; }
            public uint RoughnessMapHash { get; set; }
            public uint AmbientMapHash { get; set; }

            public List<uint> Lookup = new List<uint>();
        }

        //Parsed structs

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
            public uint Unknown;
            public ushort MeshCount;
            public ushort Unknown2;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Mesh
        {
            public uint Hash;
            public uint IndexOffset;
            public uint IndexFlags;
            public uint VertexCount;

            public byte VertexFlags;
            public byte MaterialLookupPointerCount;
            public ushort Padding;
            public ushort Unknown2;

            public ushort Unknown3;
            public uint MaterialHash;
            public uint MaterialKindHash;
            public ushort MorphCount;
            public ushort MorphStartIndex;
            public uint SkinningFlags; //0xFFFF for static meshes
            public ushort Unknown5;
            public ushort Unknown6;

            public uint Unknown7;
            public uint Unknown8;
            public uint Unknown9;
            public uint Unknown10;
            public uint Unknown11;
        }

        public ModelFormat() { }

        public ModelFormat(ChunkFileEntry fileChunk, SkeletonFormat skeleton)
        {
            Skeleton = skeleton;
            File = fileChunk;

            var chunkList = fileChunk.Children;

            List<Header> modelHeaders = new List<Header>();
            List<MeshInfo> meshes = new List<MeshInfo>();
            List<Matrix4x4> modelMatrices = new List<Matrix4x4>();
            List<BoundingBox> boundingBoxes = new List<BoundingBox>();
            List<float> boundingRadius = new List<float>();
            List<SkinBinding> skinBindings = new List<SkinBinding>();

            var vertexPointerChunk = fileChunk.GetChild(ChunkType.VertexStartPointers);
            var bufferChunk = fileChunk.GetChild(ChunkType.MeshBuffers);
            //Get the amount of models to prepare
            var modelChunk = fileChunk.GetChild(ChunkType.ModelInfo);
            uint numModels = (uint)modelChunk.Data.Length / MODEL_HEADER_SIZE;

            //Empty model, skip
            if (bufferChunk?.Data.Length <= 0)
                return;

            for (int i = 0; i < chunkList.Count; i++)
            {
                var chunk = chunkList[i];
                switch (chunk.Type)
                {
                    case ChunkType.ModelInfo:
                        modelHeaders = chunk.ReadStructs<Header>(numModels);
                        break;
                    case ChunkType.BoundingBox:
                        //1 per model
                        uint numBoxes = (uint)chunk.Data.Length / 24;
                        //Parse bounding boxes
                        boundingBoxes = chunk.ReadStructs<BoundingBox>(numBoxes);
                        break;
                    case ChunkType.BoundingRadius:
                        //1 per model + 1 per mesh
                        uint numRadius = (uint)chunk.Data.Length / 4;
                        //Parse bounding radius
                        boundingRadius.AddRange(chunk.ReadPrimitive<float>(numRadius));
                        break;
                    case ChunkType.ModelTransform:
                        //Parse model matrices. These are indexed by the meshes
                        modelMatrices = chunk.ReadStructs<Matrix4x4>((uint)(chunk.Data.Length / 64));
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

                            Console.WriteLine($"mesh {mesh.Header.Hash} SkinningFlags {mesh.Header.SkinningFlags} MaterialHash {mesh.Header.MaterialHash} MorphCount {mesh.Header.MorphCount} VertexFlags {mesh.Header.VertexFlags}");
                        }

                        //Read vertex points. These must be parsed by mesh header data
                        using (var reader = new FileReader(vertexPointerChunk.Data, true))
                        {
                            for (int m = 0; m < numMeshes; m++)
                            {
                                var mesh = meshes[m];

                                mesh.VertexBufferPointer = reader.ReadUInt32();
                                mesh.MorphVertexBufferPointer = reader.ReadUInt32();
                                mesh.TexCoordBufferPointer = reader.ReadUInt32();
                                mesh.SkinningBufferPointer = reader.ReadUInt32();
                                uint unk2 = reader.ReadUInt32();
                                uint unk3 = reader.ReadUInt32();

                                mesh.TexCoordStride = 16;
                                Console.WriteLine($"{mesh.VertexBufferPointer} {mesh.MorphVertexBufferPointer} {mesh.TexCoordBufferPointer} {mesh.SkinningBufferPointer} {unk2} {unk2}");
                            }
                        }
                        break;
                    case ChunkType.SkinControllerStart:
                        skinBindings.Add(new SkinBinding(chunk));
                        break;
                    case ChunkType.MaterialData: break;
                    case ChunkType.MaterialLookupTable: break;
                    case ChunkType.MeshBuffers: break;
                    case ChunkType.VertexStartPointers: break;
                    case ChunkType.ModelUnknownSection: break;
                    case ChunkType.MeshMorphInfos: break;
                    case ChunkType.MeshMorphIndexBuffer: break;
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
                model.SkinBinding = skinBindings.FirstOrDefault(x => x.ModelHash == model.Hash);
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
                    if (modelMatrices.Count > 0) //todo
                        mesh.Transform = modelMatrices[0];
                }
                meshIndex += (int)modelHeaders[i].MeshCount;
            }

            var materialLookupChunk = fileChunk.GetChild(ChunkType.MaterialLookupTable);
            var materialChunk = fileChunk.GetChild(ChunkType.MaterialData);

            //Read materials
            MaterialLoaderHelper.ParseMaterials(materialChunk.Data, materialLookupChunk.Data, meshes);

            //Read vertex and face data
            BufferHelper.ReadBufferData(bufferChunk.Data, this);
        }

        public void Save(ChunkFileEntry fileEntry)
        {
        }
    }
}
