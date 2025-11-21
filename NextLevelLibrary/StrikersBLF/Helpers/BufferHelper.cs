using NextLevelLibrary.LM2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AvaloniaToolbox.Core.Mesh;
using System.Numerics;
using AvaloniaToolbox.Core.IO;

namespace NextLevelLibrary.StrikersBLF
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
                    Console.WriteLine($"model {model.Hash}");

                    for (int i = 0; i < model.Meshes.Count; i++)
                    {
                        var mesh = model.Meshes[i];
                        model.Meshes[i].Faces = ReadIndices(reader, model.Meshes[i]);

                        for (int v = 0; v < mesh.Header.VertexCount; v++)
                        {
                            reader.SeekBegin(mesh.VertexBufferPointer + 0x30 * v);
                            GenericVertex vertex = ReadVertexLayout(reader, mesh.Header.MaterialKindHash);

                            reader.SeekBegin(mesh.TexCoordBufferPointer + mesh.TexCoordStride * v);
                            vertex.TexCoords = new Vector2[1];
                            vertex.TexCoords[0] = new Vector2(reader.ReadSingle(), reader.ReadSingle());

                            mesh.Vertices.Add(vertex);
                        }
                        if (mesh.HasSkinning && modelFile.Skeleton != null)
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
/*
                                    //Get the hash indexing the model's bone hash list
                                    uint boneHash = model.SkinBinding.MeshSkins[0].SkinningHashes[boneIndices[j]];
                                    //Get the index used from the skeleton's hash list
                                    int boneIndex = modelFile.Skeleton.BoneHashToID[boneHash];

                                    mesh.Vertices[v].BoneIndices.Add(boneIndex);
                                    mesh.Vertices[v].BoneWeights.Add(weights[j]);*/
                                }
                            }
                        }
                    }
                    modelIdx++;
                }
            }
        }

        static GenericVertex ReadVertexLayout(FileReader reader, uint hash)
        {
            GenericVertex vertex = new GenericVertex();
            vertex.Position = reader.ReadVector3();
            reader.ReadSingle();
            vertex.Normal = reader.ReadVector3();
            reader.ReadSingle();
            vertex.Tangent = reader.ReadVector4();
            return vertex;
        }

        public static uint[] ReadIndices(FileReader reader, ModelFormat.MeshInfo mesh)
        {
            reader.SeekBegin(mesh.Header.IndexOffset);

            uint[] faces = new uint[mesh.IndexCount];
            if (mesh.IndexFormat == 0x80)
            {
                for (int f = 0; f < mesh.IndexCount; f++)
                   faces[f] = reader.ReadByte();
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
