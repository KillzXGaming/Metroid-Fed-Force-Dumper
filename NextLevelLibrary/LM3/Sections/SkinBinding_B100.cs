using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Numerics;
using AvaloniaToolbox.Core.IO;

namespace NextLevelLibrary.LM3
{
    public class SkinBinding
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
        public List<Matrix4x4> Matrices = new List<Matrix4x4>();


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

        public SkinBinding() { }

        public SkinBinding(ChunkEntry skinningChunk)
        {
            foreach (var chunk in skinningChunk.Children)
            {
                switch (chunk.Type)
                {
                    case ChunkType.SkinBindingModelAssign:
                        ModelHash = chunk.ReadPrimitive<uint>(1)[0];
                        break;
                    case ChunkType.SkinMatrices:
                        uint numBones = (uint)chunk.Data.Length / 64;
                        using (var reader = new FileReader(chunk.Data, true))
                        {
                            for (int m = 0; m < numBones; m++)
                                Matrices.Add(reader.ReadStruct<Matrix4x4>());
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

        public void Write(ChunkEntry skinningChunk)
        {
            skinningChunk.Children.Clear();
        }
    }
}
