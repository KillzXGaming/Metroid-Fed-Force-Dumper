using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using AvaloniaToolbox.Core;
using System.Numerics;

namespace NextLevelLibrary.LM3
{
    public class SkeletonFormat : IFormat
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Header
        {
            public uint Padding1;
            public uint Padding2;
            public uint Padding3;
            public uint Padding4;
            public uint Padding5;
            public uint BoneCount;
            public uint BoneIndexListCount;
            public uint Padding6;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class BoneInfo
        {
            public uint Hash;
            public short TotalChildCount;
            public byte ChildCount;
            public byte Unknown3;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class BoneTransform
        {
            public float QuaternionX;
            public float QuaternionY;
            public float QuaternionZ;
            public float QuaternionW;

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

        [XmlIgnore]
        public GenericSkeleton GenericSkeleton = new GenericSkeleton();

        public Dictionary<uint, int> BoneHashToID = new Dictionary<uint, int>();

        public SkeletonFormat(ChunkFileEntry chunkFile)
        {
            var chunkList = chunkFile.Children;

            Header header = null;
            List<BoneInfo> boneInfos = new List<BoneInfo>();
            List<BoneTransform> boneTransforms = new List<BoneTransform>();
            List<uint> boneIndices = new List<uint>();
            List<BoneHash> boneHashes = new List<BoneHash>();
            List<short> parentIndices = new List<short>();

            for (int i = 0; i < chunkList.Count; i++) {
                var chunk = chunkList[i];
                switch (chunk.Type)
                {
                    case ChunkType.SkeletonHeader:
                        header = chunk.ReadStruct<Header>();
                        break;
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
                    case ChunkType.SkeletonBoneParenting:
                        parentIndices = chunk.ReadPrimitive<short>(header.BoneCount);
                        break;
                }
            }

            GenericSkeleton.RootTransform = 
                 Matrix4x4.CreateRotationX(MathHelper.Deg2Rad * -90) *
                 Matrix4x4.CreateRotationY(MathHelper.Deg2Rad * -90);

            for (int i = 0; i < boneInfos.Count; i++) {
                var info = boneInfos[i];
                var transform = boneTransforms[i];
                string name = Hashing.GetString(info.Hash);

                GenericSkeleton.Bones.Add(new GenericBone(GenericSkeleton)
                {
                    Name = name,
                    ParentIndex = parentIndices[i],
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
            }

            for (int i = 0; i < boneHashes.Count; i++) {
                BoneHashToID.Add(boneHashes[i].Hash, (int)boneHashes[i].Index);

            }
            GenericSkeleton.UpdateMatrices();
        }
    }
}
