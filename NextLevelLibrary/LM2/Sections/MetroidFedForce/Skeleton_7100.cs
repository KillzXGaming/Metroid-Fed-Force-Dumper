using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using Toolbox.Core;
using AvaloniaToolbox.Core;
using System.Numerics;

namespace NextLevelLibrary.MetroidFedForce
{
    /// <summary>
    /// Represents a skeleton for animating a model file.
    /// The skeleton is linked to the model by using the same file hash name.
    /// </summary>
    public class SkeletonFormat : IFormat
    {
        /// <summary>
        /// Gets the bone index from a given bone hash.
        /// </summary>
        public Dictionary<uint, int> BoneHashToID = new Dictionary<uint, int>();

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Header
        {
            public uint BoneCount;
            public uint Padding1;
            public uint Padding2;
            public uint Padding3;
            public uint BoneIndexListCount;
            public uint Padding4;
            public uint Padding5;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class BoneInfo
        {
            public uint Hash;
            public short ParentIndex;
            public short TotalChildCount;
            public ushort BoneIndex;
            public byte ChildCount;
            public byte Unknown3;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class BoneTransform
        {
            public float QuaternionX;
            public float QuaternionY;
            public float QuaternionZ;
            public float QuaternionW = 1;

            public float TranslationX;
            public float TranslationY;
            public float TranslationZ;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class BoneHash
        {
            public uint Hash;
            public uint Index;
        }

        public GenericSkeleton GenericSkeleton;

        public SkeletonFormat() { }

        public SkeletonFormat(ChunkFileEntry file)
        {
            var chunkList = file.Children;

            GenericSkeleton = new GenericSkeleton();

            Header header = null;
            List<BoneInfo> boneInfos = new List<BoneInfo>();
            List<BoneTransform> boneTransforms = new List<BoneTransform>();
            List<uint> boneIndices = new List<uint>();
            List<BoneHash> boneHashes = new List<BoneHash>();

            for (int i = 0; i < chunkList.Count; i++)
            {
                if (chunkList[i].Type == ChunkType.SkeletonHeader)
                    header = chunkList[i].ReadStruct<Header>();
            }

            for (int i = 0; i < chunkList.Count; i++)
            {
                var chunk = chunkList[i];
                if (chunk.Data != null)
                    chunk.Data.Position = 0;

                switch (chunk.Type)
                {
                    case ChunkType.SkeletonBoneInfo:
                        boneInfos = chunk.ReadStructs<BoneInfo>(header.BoneCount);
                        break;
                    case ChunkType.SkeletonBoneTransform:
                        boneTransforms = chunk.ReadStructs<BoneTransform>(header.BoneCount);
                        break;
                    case ChunkType.SkeletonBoneIndexList:
                        boneIndices = chunk.ReadPrimitive<uint>(header.BoneIndexListCount);
                        break;
                    case ChunkType.SkeletonBoneHashList:
                        boneHashes = chunk.ReadStructs<BoneHash>(header.BoneCount);
                        break;
                }
            }

            for (int i = 0; i < boneInfos.Count; i++)
            {
                var info = boneInfos[i];
                var transform = boneTransforms[i];
                string name = Hashing.GetString(info.Hash);

                if (transform == null) transform = new BoneTransform();

                GenericSkeleton.Bones.Add(new GenericBone(GenericSkeleton)
                {
                    Name = name,
                    Parent = info.ParentIndex == -1 ? null : GenericSkeleton.Bones[info.ParentIndex],
                    Position = new Vector3(
                        transform.TranslationX,
                        transform.TranslationY,
                        transform.TranslationZ),
                    Rotation = new Quaternion(
                             transform.QuaternionX,
                             transform.QuaternionY,
                             transform.QuaternionZ,
                             transform.QuaternionW),
                });

                GenericSkeleton.Bones[0].Position = Vector3.Zero;
            }

            for (int i = 0; i < boneHashes.Count; i++) {
                if (!BoneHashToID.ContainsKey(boneHashes[i].Hash))
                    BoneHashToID.Add(boneHashes[i].Hash, (int)boneHashes[i].Index);
            }

         //   GenericSkeleton.RootTransform =
          //       Matrix4x4.CreateRotationX(MathHelper.Deg2Rad * -90);

            GenericSkeleton.Reset();
        }

        public void Save(ChunkFileEntry fileEntry)
        {

        }
    }
}
