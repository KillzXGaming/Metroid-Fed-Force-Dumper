using NextLevelLibrary.LM2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Toolbox.Core;
using System.Numerics;
using AvaloniaToolbox.Core.IO;

namespace NextLevelLibrary.MetroidFedForce
{
    internal class BufferHelper
    {
        const float UV_SCALE = 1.0f / 1024.0f;
        const float COLOR_SCALE = 1.0f / 255f;
        const float NORMAL_SCALE = 1.0f / 128f;

        public static void ReadBufferData(Stream bufferChunk, List<ModelFormat.ModelInfo> models, SkeletonFormat skeleton)
        {
            //Parse the vertex and index buffers
            using (var reader = new FileReader(bufferChunk, true))
            {
                int modelIdx = 0;
                foreach (var model in models)
                {
                    for (int i = 0; i < model.Meshes.Count; i++)
                    {
                        var mesh = model.Meshes[i];
                        model.Meshes[i].Faces = ReadIndices(reader, model.Meshes[i].Header);

                        int vertexStride = VertexLoaderExtension.GetStride(mesh.Header.MaterialHash);
                        if (vertexStride == 0)
                        {
                            // Estimate stride for unknown types
                            if (i < model.Meshes.Count - 1)
                            {
                                var next = model.Meshes[i + 1].VertexBufferPointer;
                                var size = next - mesh.VertexBufferPointer;
                                var stride = size / mesh.Header.VertexCount;
                                Console.WriteLine($"{stride} {new HashString(mesh.Header.MaterialHash).String}");
                            }
                            else if (modelIdx < models.Count - 1)
                            {
                                var next = models[modelIdx + 1].Meshes[0].VertexBufferPointer;
                                var size = next - mesh.VertexBufferPointer;
                                var stride = size / mesh.Header.VertexCount;
                                Console.WriteLine($"{stride} {new HashString(mesh.Header.MaterialHash).String}");
                            }
                            else
                            {
                                var next = mesh.Header.IndexOffset;
                                var size = next - mesh.VertexBufferPointer;
                                var stride = size / mesh.Header.VertexCount;
                                Console.WriteLine($"{stride} {new HashString(mesh.Header.MaterialHash).String}");
                            }
                        }

                        for (int v = 0; v < mesh.Header.VertexCount; v++)
                        {
                            // STVertex vertex = reader.ReadVertexLayout(mesh.Header.VertexFormatHash);
                            reader.SeekBegin(mesh.VertexBufferPointer + (v * vertexStride));
                            Vertex vertex = VertexLoaderExtension.ReadVertexLayout(reader, mesh.Header.MaterialHash);
                            //flip to be Y up axis
                          /*  vertex.Position = new System.Numerics.Vector3(
                                vertex.Position.X, vertex.Position.Z, -vertex.Position.Y);
                            vertex.Normal = new System.Numerics.Vector3(
                                   vertex.Normal.X, vertex.Normal.Z, -vertex.Normal.Y);*/
                            mesh.Vertices.Add(vertex);
                        }
                    }
                    modelIdx++;
                }
            }
        }



        static int GetStride(uint hash)
        {
            return 24;
        }

        static Vertex ReadVertexLayout(FileReader reader, uint hash)
        {
            Vertex vertex = new Vertex();
            vertex.Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            vertex.Normal = new Vector3(reader.ReadSByte(), reader.ReadSByte(), reader.ReadSByte()) * NORMAL_SCALE;
            reader.ReadSByte();
            vertex.TexCoord0 = new Vector2(reader.ReadInt16(), reader.ReadInt16()) * UV_SCALE;
            vertex.Color = new Vector4(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte()) * COLOR_SCALE;
            return vertex;
        }

        public static uint[] ReadIndices(FileReader reader, ModelFormat.Mesh mesh)
        {
            reader.SeekBegin(mesh.IndexOffset);

            uint[] faces = new uint[mesh.IndexCount];
            if (mesh.IndexFormat == 0x8000)
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

        public static void WriteBufferData(ChunkEntry file, List<ModelFormat.ModelInfo> models, SkeletonFormat skeleton)
        {
            var chunk = file.GetChild(ChunkType.MeshBuffers);
            using (var writer = new FileWriter(chunk.Data, true))
            {
                int ptrShfit = 0;

                var meshList = models.SelectMany(x => x.Meshes);
                foreach (var mesh in meshList)
                {
                    int vertexStride = VertexLoaderExtension.GetStride(mesh.Header.MaterialVariantHash);

                    mesh.Header.VertexCount = (ushort)mesh.Vertices.Count;
                    mesh.VertexBufferPointer = (uint)writer.Position;
                    mesh.Header.BufferPointerOffset = (ushort)ptrShfit;
                    ptrShfit += 4;

                    writer.Align(16);

                    for (int v = 0; v < mesh.Vertices.Count; v++)
                    {
                        writer.SeekBegin(mesh.VertexBufferPointer + (v * vertexStride));
                        VertexLoaderExtension.WriteVertexLayout(writer, mesh.Vertices[v], mesh.Header.MaterialVariantHash);
                    }
                }

                foreach (var mesh in meshList)
                {
                    writer.Align(4);

                    mesh.Header.IndexCount = (ushort)mesh.Faces.Length;
                    mesh.Header.IndexOffset = (uint)writer.Position;
                    mesh.Header.IndexFormat = (ushort)(mesh.Faces.Max(x => x > 255) ? 0 : 0x8000);

                    WriteIndices(writer, mesh);
                }
            }
        }

        public static void WriteIndices(FileWriter writer, ModelFormat.MeshInfo mesh)
        {
            if (mesh.Header.IndexFormat == 0x8000)
            {
                for (int f = 0; f < mesh.Faces.Length; f++)
                    writer.Write((byte)mesh.Faces[f]);
            }
            else
            {
                for (int f = 0; f < mesh.Faces.Length; f++)
                    writer.Write((ushort)mesh.Faces[f]);
            }
        }
    }
}
