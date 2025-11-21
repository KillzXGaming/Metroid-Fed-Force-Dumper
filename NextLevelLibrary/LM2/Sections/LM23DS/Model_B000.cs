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
using AvaloniaToolbox.Core.IO;

namespace NextLevelLibrary.LM2
{
    /// <summary>
    /// Represents a model format that contains mesh data.
    /// </summary>
    public class ModelFormat : IFormat
    {
        const uint MODEL_HEADER_SIZE = 16;
        const uint MESH_SIZE = 0x28;

        public List<ModelInfo> Models = new List<ModelInfo>();

        public SkeletonFormat SkeletonFormat;

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
            public SkinController SkinController = new SkinController();

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

            [XmlIgnore]
            public uint[] Faces { get; set; }

            [XmlIgnore]
            public List<Vertex> Vertices = new List<Vertex>();

            public MeshInfo(Mesh mesh) { Header = mesh; }

            public MeshInfo() {
                Header = new Mesh();
                Material = new MaterialData();
                Transform = new Matrix4x4();
                Faces = new uint[0];
            }
        }

        public class MaterialData
        {
            public uint DiffuseTextureHash { get; set; }
            public uint ShadowTextureHash { get; set; }

            public bool IsAmbientMap { get; set; }
            public bool HasShadowMap { get; set; }
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
            public uint MeshCount;
            public uint Padding1; //Always 0
            public uint Padding2; //Always 0
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Mesh
        {
            public uint IndexOffset;
            public ushort IndexCount;
            public ushort IndexFormat;
            public ushort BufferPointerOffset; //Pointer to vertex buffer pointer offset
            public ushort Padding; //Always 0
            public uint MaterialHash; // Hash of the material name as lowercase from material table
            public uint MaterialVariantHash; // Hash of the material variant name as lowercase from material table
            public uint TransformMatrixIndex = 0; //Index to model matrix section
            public uint Padding2; //Always 0
            public uint MaterialLookupIndex; //Multiply by 4 to get offset in table
            public ushort VertexCount;
            public ushort Flag = 256;
            public uint Hash; // Mesh hash name
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class MeshLM2HD
        {
            public uint IndexOffset;
            public uint IndexCount;
            public uint BufferPointerOffset; //Pointer to vertex buffer pointer offset
            public uint VertexFormatHash;
            public uint MaterialHash;
            public uint TransformMatrixIndex = 0; //Index to model matrix section
            public uint Padding; //Always 0
            public uint MaterialLookupIndex; //Multiply by 4 to get offset in table
            public uint VertexCount;
            public uint Flag = 15663360;
            public uint Hash;

            //Always 0, align 32
            public uint Padding2; 
            public uint Padding3;
            public uint Padding4;
            public uint Padding5;
            public uint Padding6;
        }

        public ModelFormat() { }

        public ModelFormat(ChunkFileEntry fileChunk, SkeletonFormat skeleton)
        {
            SkeletonFormat = skeleton;
            var chunkList = fileChunk.Children;

            List<Header> modelHeaders = new List<Header>();
            List<MeshInfo> meshes = new List<MeshInfo>();
            List<Matrix4x4> modelMatrices = new List<Matrix4x4>();
            List<BoundingBox> boundingBoxes = new List<BoundingBox>();
            List<float> boundingRadius = new List<float>();
            List<SkinController> skinBindings = new List<SkinController>();

            var vertexPointerChunk = fileChunk.GetChild(ChunkType.VertexStartPointers);
            var bufferChunk = fileChunk.GetChild(ChunkType.MeshBuffers);
            //Get the amount of models to prepare
            var modelChunk = fileChunk.GetChild(ChunkType.ModelInfo);
            uint numModels = (uint)modelChunk.Data.Length / MODEL_HEADER_SIZE;

            //Empty model, skip
            if (bufferChunk.Data.Length == 0)
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

                         /*   if (mesh.Header.Padding != 0 || mesh.Header.Padding2!= 0)
                                throw new Exception();*/
                        }

                        //Read vertex points. These must be parsed by mesh header data
                        using (var reader = new FileReader(vertexPointerChunk.Data, true))
                        {
                            for (int m = 0; m < numMeshes; m++)
                            {
                                reader.SeekBegin(meshes[m].Header.BufferPointerOffset);
                                meshes[m].VertexBufferPointer = reader.ReadUInt32();
                            }
                        }
                        break;
                    case ChunkType.SkinControllerStart:
                        skinBindings.Add(new SkinController(chunk));
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

                if (modelHeaders[i].Padding1 != 0 || modelHeaders[i].Padding2 != 0)
                    throw new Exception();

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
            MaterialLoaderHelper.ParseMaterials(materialChunk.Data, materialLookupChunk.Data, meshes);

            //Read vertex and face data
            BufferHelper.ReadBufferData(bufferChunk.Data, Models, skeleton);
        }

        public void Save(ChunkEntry fileEntry)
        {
            /*  var m = ModelImporter.Import(SkeletonFormat, "untitled.dae");
              m.Hash = Models[0].Hash;

              Models.Clear();
              Models.Add(m);*/

            fileEntry.Children.Clear();
            fileEntry.AddChild(ChunkType.MaterialData);
            fileEntry.AddChild(ChunkType.MeshBuffers);
            fileEntry.AddChild(ChunkType.VertexStartPointers);
            fileEntry.AddChild(ChunkType.MaterialLookupTable);
            fileEntry.AddChild(ChunkType.MeshInfo);
            fileEntry.AddChild(ChunkType.ModelTransform);
            fileEntry.AddChild(ChunkType.ModelInfo);

            //Save material buffer
            SaveMaterialData(fileEntry);
            //Save vertex buffer
            BufferHelper.WriteBufferData(fileEntry, Models, null);
            //Save vertex buffer points
            SaveVertexBufferPointers(fileEntry);
            //Save mesh headers
            SaveMeshHeaders(fileEntry);
            //Save model matrices
            SaveMeshTransforms(fileEntry);
            //Save model headers
            SaveModelHeader(fileEntry);
            //Save skin bindings/controller if present
            foreach (var model in this.Models)
            {
                if (model.SkinController != null)
                {
                    var skinMatrices = fileEntry.AddChild(ChunkType.SkinControllerStart, true);
                    model.SkinController.Write(skinMatrices, model);
                }
            }
            //Save bounding radius
            //This only gets used for non static objects?
            if (Models.Any(x => x.SkinController != null))
            {
                var bndRadiusChunk = fileEntry.AddChild(ChunkType.BoundingRadius);
                using (var writer = new FileWriter(bndRadiusChunk.Data, true))
                {
                    foreach (var model in Models)
                        writer.Write(model.BoundingRadius);
                }
            }

            //Save bounding boxes
            var bndBoxChunk = fileEntry.AddChild(ChunkType.BoundingBox);
            using (var writer = new FileWriter(bndBoxChunk.Data, true))
            {
                foreach (var model in Models)
                    writer.WriteStruct(model.BoundingBox);
            }
        }

        private void SaveMaterialData(ChunkEntry fileEntry)
        {
       
        }

        private void SaveMeshTransforms(ChunkEntry fileEntry)
        {
            var transformChunk = fileEntry.GetChild(ChunkType.ModelTransform);
            using (var writer = new FileWriter(transformChunk.Data, true))
            {
                var matrices = Models.SelectMany(x => x.Meshes.Select(x => x.Transform)).ToList().Distinct();
                foreach (var matrix in matrices)
                    writer.WriteStruct(matrix);
            }
        }

        private void SaveMeshHeaders(ChunkEntry fileEntry)
        {
            var chunk = fileEntry.GetChild(ChunkType.MeshInfo);
            var meshHeaders = Models.SelectMany(x => x.Meshes.Select(x => x.Header)).ToList();
            chunk.WriteStructs(meshHeaders);
        }

        private void SaveModelHeader(ChunkEntry fileEntry)
        {
            var modelChunk = fileEntry.GetChild(ChunkType.ModelInfo);
            using (var writer = new FileWriter(modelChunk.Data, true))
            {
                foreach (var model in Models)
                {
                    writer.Write(model.Hash);
                    writer.Write(model.Meshes.Count);
                    writer.Write(0);
                    writer.Write(0);
                }
            }
        }

        private void SaveVertexBufferPointers(ChunkEntry fileEntry)
        {
            var bufferPointerChunk = fileEntry.GetChild(ChunkType.VertexStartPointers);
            using (var writer = new FileWriter(bufferPointerChunk.Data, true))
            {
                foreach (var mesh in Models.SelectMany(x => x.Meshes))
                    writer.Write(mesh.VertexBufferPointer);
            }
        }
    }
}
