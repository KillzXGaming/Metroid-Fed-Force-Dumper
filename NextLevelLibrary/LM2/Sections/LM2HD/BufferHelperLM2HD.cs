using AvaloniaToolbox.Core.IO;
using NextLevelLibrary.LM2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Toolbox.Core;

namespace NextLevelLibrary
{
    public class BufferHelperLM2HD
    {
        public static Dictionary<string, int> StrideTable = new Dictionary<string, int>();

        public static void ReadBufferData(Stream bufferChunk, 
            List<ModelFormat_LM2HD.ModelInfo> models, SkeletonFormat skeleton, List<uint> vertexPointers)
        {
            using (var reader = new FileReader(bufferChunk, true))
            {
                foreach (var model in models)
                {
                    for (int i = 0; i < model.Meshes.Count; i++)
                    {
                        var mesh = model.Meshes[i];
                        model.Meshes[i].Faces = ReadIndices(reader, model.Meshes[i].Header);

                        string mat = Hashing.GetString(mesh.Header.MaterialHash).ToLower();
                        string submat = Hashing.GetString(mesh.Header.SubMaterialHash).ToLower();

                        int vertexStride = VertexLoaderExtensionLM2HD.GetStride(mat);
                        if (vertexStride == 0 || true)
                        {
                            vertexStride = (int)(model.Meshes[i].VertexBufferSize / model.Meshes[i].Header.VertexCount);
                            Console.WriteLine($"{vertexStride.ToString()} {mat}");
                        }
                        else
                            Console.WriteLine($"{vertexStride.ToString()} {mat}");
                        

                        if (!StrideTable.ContainsKey(mat))
                            StrideTable.Add(mat, vertexStride);

                        if (StrideTable[mat] > vertexStride)
                            StrideTable[mat] = vertexStride;

                        for (int v = 0; v < mesh.Header.VertexCount; v++)
                        {
                            reader.SeekBegin(mesh.VertexBufferPointer + (v * vertexStride));
                            Vertex vertex = reader.ReadVertexLayout(mat);
                            vertex.Position = System.Numerics.Vector3.Transform(vertex.Position, mesh.Transform);

                            //flip to be Y up axis
                            vertex.Position = new System.Numerics.Vector3(
                                vertex.Position.X, vertex.Position.Z, -vertex.Position.Y);
                            vertex.Normal = new System.Numerics.Vector3(
                                   vertex.Normal.X, vertex.Normal.Z, -vertex.Normal.Y);
                            mesh.Vertices.Add(vertex);
                        }
                    }
                }
            }
        }

        public static uint[] ReadIndices(FileReader reader, ModelFormat_LM2HD.Mesh mesh)
        {
            reader.SeekBegin(mesh.IndexOffset);

            uint[] faces = new uint[mesh.IndexCount];
            for (int f = 0; f < mesh.IndexCount; f++)
                faces[f] = reader.ReadUInt16();
            return faces;
        }
    }
}
