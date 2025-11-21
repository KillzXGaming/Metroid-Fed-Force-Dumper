using NextLevelLibrary.LM2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Toolbox.Core;
using System.Numerics;
using AvaloniaToolbox.Core.IO;

namespace NextLevelLibrary.LM3
{
    internal class BufferHelper
    {
        public static void ReadBufferData(Stream bufferChunk, ModelFormat modelFile)
        {
            //Parse the vertex and index buffers
            using (var reader = new FileReader(bufferChunk, true))
            {
                int modelIdx = 0;
                foreach (var model in modelFile.Models)
                {
                    for (int i = 0; i < model.Meshes.Count; i++)
                    {
                        var mesh = model.Meshes[i];
                        model.Meshes[i].Faces = ReadIndices(reader, model.Meshes[i]);

                        ReadVertexBuffer(reader, mesh, mesh.Material);

                        //Unsupported vertex buffer
                        if (mesh.Vertices.Count == 0)
                            continue;

                        ReadTexCoordBuffer(reader, mesh, mesh.Material);

                        if (mesh.HasSkinning && modelFile.Skeleton != null &&
                            mesh.SkinningBufferPointer != uint.MaxValue)
                        {
                            reader.SeekBegin(mesh.SkinningBufferPointer);
                            for (int v = 0; v < mesh.Header.VertexCount; v++)
                            {
                                byte[] boneIndices = reader.ReadBytes(4);
                                float[] weights = reader.ReadSingles(4);

                                for (int j = 0; j < 4; j++)
                                {
                                    if (weights[j] == 0)
                                        break;

                                    //Get the hash indexing the model's bone hash list
                                    uint boneHash = model.SkinBinding.MeshSkins[0].SkinningHashes[boneIndices[j]];
                                    //Get the index used from the skeleton's hash list
                                    int boneIndex = modelFile.Skeleton.BoneHashToID[boneHash];

                                    mesh.Vertices[v].BoneIndices.Add(boneIndex);
                                    mesh.Vertices[v].BoneWeights.Add(weights[j]);
                                }
                            }
                        }
                    }
                    modelIdx++;
                }
            }
        }

        static void ReadTexCoordBuffer(FileReader reader, ModelFormat.MeshInfo mesh, ModelFormat.MaterialData mat)
        {
            var bufferType = 10;
            if (mat.Material != null)
            {
                var matInfo = mat.Material.Material;
                //3 = tex coord attribute
                if (!matInfo.AttributeIndices.ContainsKey(3))
                    return;

                //Get tex coord buffer type 
                 bufferType = matInfo.AttributeIndices[3];
            }

            switch (bufferType)
            {
                case 10: //8 stride
                    for (int v = 0; v < mesh.Header.VertexCount; v++)
                    {
                        reader.SeekBegin((mesh.UVLayerBufferPointer + v * 8));
                        mesh.Vertices[v].TexCoord1 = new Vector4(reader.ReadUInt16() / 65535f, reader.ReadUInt16() / 65535f, 0, 0);
                        mesh.Vertices[v].TexCoord2 = new Vector2(reader.ReadUInt16() / 65535f, reader.ReadUInt16() / 65535f);
                    }
                    break;
                case 11: //16 stride, 3 tex coords
                    for (int v = 0; v < mesh.Header.VertexCount; v++)
                    {
                        reader.SeekBegin((mesh.UVLayerBufferPointer + v * 16));
                        mesh.Vertices[v].TexCoord1 = new Vector4(reader.ReadSingle(), reader.ReadSingle(), 0, 0);
                        mesh.Vertices[v].TexCoord2 = new Vector2(reader.ReadUInt16() / 65535f, reader.ReadUInt16() / 65535f);
                        mesh.Vertices[v].TexCoord3 = new Vector2(reader.ReadUInt16() / 65535f, reader.ReadUInt16() / 65535f);
                    }
                    break;
                case 12: //32 stride, 5 tex coords
                    for (int v = 0; v < mesh.Header.VertexCount; v++)
                    {
                        reader.SeekBegin((mesh.UVLayerBufferPointer + v * 32));
                        //loads 1 attribute, 2 tex coords as float4
                        mesh.Vertices[v].TexCoord1 = new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        //loads 1 attribute as float2
                        mesh.Vertices[v].TexCoord2 = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                        //loads 2 tex coords as unorm2
                        mesh.Vertices[v].TexCoord3 = new Vector2(reader.ReadUInt16() / 65535f, reader.ReadUInt16() / 65535f);
                        mesh.Vertices[v].TexCoord4 = new Vector2(reader.ReadUInt16() / 65535f, reader.ReadUInt16() / 65535f);
                    }
                    break;
            }
        }

        static void ReadVertexBuffer(FileReader reader, ModelFormat.MeshInfo mesh, ModelFormat.MaterialData mat)
        {
            var bufferType = 7;

            if (mat.Material != null)
            {
                var matInfo = mat.Material.Material;

                //3 = tex coord attribute
                if (!matInfo.AttributeIndices.ContainsKey(0))
                    return;

                //Get tex coord buffer type 
                bufferType = matInfo.AttributeIndices[0];
            } // Else check if the vertex data can fit
            else if (reader.BaseStream.Length < mesh.VertexBufferPointer + mesh.Header.VertexCount * 48)
            {
                return;
            }

            switch (bufferType)
            {
                case 7: //48 stride

                    reader.SeekBegin(mesh.VertexBufferPointer);
                    for (int v = 0; v < mesh.Header.VertexCount; v++)
                    {
                        mesh.Vertices.Add(ReadVertexLayout(reader, mesh.Header.MaterialKindHash));
                    }
                    break;
            }
        }

        static Vertex ReadVertexLayout(FileReader reader, uint hash)
        {
            Vertex vertex = new Vertex();
            vertex.Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            float texCoordU = reader.ReadSingle();
            vertex.Normal = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            float texCoordV = reader.ReadSingle();
            vertex.Tangent = new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            vertex.TexCoord0 = new Vector2(texCoordU, texCoordV);
            return vertex;
        }

        public static uint[] ReadIndices(FileReader reader, ModelFormat.MeshInfo mesh)
        {
            reader.SeekBegin(mesh.Header.IndexOffset);
           // if (reader.BaseStream.Length <= mesh.Header.IndexOffset + mesh.IndexCount * 2)
           //     return new uint[mesh.IndexCount];

            uint[] faces = new uint[mesh.IndexCount];
            if (mesh.IndexFormat == 0x80)
            {
                for (int f = 0; f < mesh.IndexCount; f++)
                   faces[f] = reader.ReadByte();
            }
            else if (mesh.IndexFormat == 0x40)
            {
                for (int f = 0; f < mesh.IndexCount; f++)
                    faces[f] = reader.ReadUInt32();
            }
            else
            {
                for (int f = 0; f < mesh.IndexCount; f++)
                    faces[f] = reader.ReadUInt16();
            }
            return faces;
        }
    }
}
