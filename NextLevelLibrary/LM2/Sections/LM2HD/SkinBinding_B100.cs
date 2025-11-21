using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Numerics;
using AvaloniaToolbox.Core.IO;

namespace NextLevelLibrary.LM2
{
    /// <summary>
    /// Represents a skin controller for handling rigging from skinned indices and matrices.
    /// </summary>
    public class SkinControllerLM2HD
    {
        /// <summary>
        /// The model hash to assign to
        /// </summary>
        public uint ModelHash;

        /// <summary>
        /// Represents a list of mesh skins that assign bone indices to bone hashes for each skinned mesh.
        /// </summary>
        public List<MeshSkin> MeshSkins = new List<MeshSkin>();

        /// <summary>
        /// Matrices used for skinning in world space.
        /// </summary>
        [XmlIgnore]
        public Dictionary<uint, Matrix4x4> Matrices = new Dictionary<uint, Matrix4x4>();


        /// <summary>
        /// Represents a mesh skin that assign bone indices to bone hashes for the skinned mesh.
        /// </summary>
        public class MeshSkin
        {
            /// <summary>
            /// Hashes assigned to the index of the bone.
            /// </summary>
            public List<uint> SkinningHashes = new List<uint>();
        }

        public SkinControllerLM2HD() { }

        public SkinControllerLM2HD(ChunkEntry skinningChunk)
        {
            foreach (var chunk in skinningChunk.Children)
            {
                switch (chunk.Type)
                {
                    case ChunkType.SkinBindingModelAssign:
                        ModelHash = chunk.ReadPrimitive<uint>(1)[0];
                        break;
                    case ChunkType.SkinMatrices:
                        uint numBones = (uint)chunk.Data.Length / 68;
                        using (var reader = new FileReader(chunk.Data, true))
                        {
                            for (int m = 0; m < numBones; m++)
                            {
                                uint boneHash = reader.ReadUInt32();

                                Matrices.Add(boneHash, new Matrix4x4(
                                  reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                                  reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                                  reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                                  reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
                            }
                        }
                        break;
                    case ChunkType.SkinHashes:
                        uint count = (uint)chunk.Data.Length / 4; //4 bytes per entry
                        var hashes = chunk.ReadPrimitive<uint>(count);
                        //Each mesh has a skin hashes section
                        MeshSkins.Add(new MeshSkin() { SkinningHashes = hashes });
                        break;
                    default:
                        throw new Exception($"Unsupported skin binding section! {chunk.Type}");
                }
            }
        }

        public void Write(ChunkEntry skinningChunk, ModelFormat_LM2HD.ModelInfo model)
        {
            skinningChunk.Children.Clear();
            var modelAssign = skinningChunk.AddChild(ChunkType.SkinBindingModelAssign);

            using (var writer = new FileWriter(modelAssign.Data, true))
            {
                writer.Write(model.Hash);
            }

            foreach (var mesh in this.MeshSkins)
            {
                var skinHashes = skinningChunk.AddChild(ChunkType.SkinHashes);
                using (var writer = new FileWriter(skinHashes.Data, true)) {
                    writer.Write(mesh.SkinningHashes.ToArray());
                }
            }

            var matrices = skinningChunk.AddChild(ChunkType.SkinMatrices);

            using (var writer = new FileWriter(matrices.Data, true))
            {
                foreach (var matrix in this.Matrices)
                {
                    writer.Write(matrix.Key); //bone hash
                    writer.WriteStruct(matrix.Value); //matrix4x4
                }
            }
        }
    }
}
