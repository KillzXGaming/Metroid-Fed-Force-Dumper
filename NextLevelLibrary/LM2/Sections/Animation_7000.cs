using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Toolbox.Core;
using System.Linq;
using System.Text;
using AvaloniaToolbox.Core.IO;
using AvaloniaToolbox.Core.Animation;
using System.Numerics;
using AvaloniaToolbox.Core;

namespace NextLevelLibrary.LM2
{
    public class AnimationFormat : IFormat
    {
        private ChunkFileEntry File;

        public Header FileHeader;

        public List<Track> Tracks = new List<Track>();

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Header
        {
            public uint Padding;
            public ushort TrackCount;
            public ushort FrameCount;
            public float Duriation;
            public uint Padding2;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Track
        {
            public uint Hash;
            public byte Index;
            public byte Unknown;
            public TrackType Type;
            public byte OpCode;
            public uint DataOffset;
        }

        public List<BoneAnim> BoneAnims = new List<BoneAnim>();

        public class BoneAnim
        {
            public TrackVector3 Translation = new TrackVector3();
            public TrackQuat Rotation = new TrackQuat();
            public TrackVector3 Scale = new TrackVector3();

            public HashString Name;
        }

        public class TrackVector3
        {
            public List<Vector3> Values = new List<Vector3>();

            public Vector3 GetValue(float frame, float frameCount) =>
                AnimationUtil.GetValue(Values, frame, frameCount);  
        }
        public class TrackQuat
        {
            public List<Quaternion> Values = new List<Quaternion>();

            public Quaternion GetValue(float frame, float frameCount) =>
                AnimationUtil.GetValue(Values, frame, frameCount);
        }

        public enum TrackType : byte
        {
            Rotation = 0,
            Translate = 1,
            Scale = 2,
            JointVisibility = 3, //op code always 1 (byte)
            Morph = 4,
            Camera = 5, // "fov y", "camera rotation", "camera position"
            UVScroll = 6,
        }

        public enum TranslationOpCode
        {
            Float32 = 0x06,
            Float16 = 0x08,
            Float32Frames = 0x9,
            Float16Frames = 0xA,
            Float32Constant = 0xB,
            Float16Constant = 0xC,
            Float32AxisFrames = 0xD,
            Float16AxisFrames = 0xE,
        }

        public enum RotationOpCode
        {
            QUAT_FLOAT_BAKED = 15, //baked each frame, float 4
            QUAT_FLOAT_KEYED = 19, //keyed, float 4

            QUAT_SHORT_KEYED = 20, //keyed xyzw / 32767.0f
            QUAT_SHORT_FIXED = 22, //fixed xyzw / 32767.0f

            EULER_AXIS_X_SHORT_BAKED = 23, //baked each frame x / 32767.0f
            EULER_AXIS_Y_SHORT_BAKED = 24, //baked each frame y / 32767.0f
            EULER_AXIS_Z_SHORT_BAKED = 25, //baked each frame z / 32767.0f

            EULER_AXIS_X_SHORT_KEYED = 26, //x / 32767.0f
            EULER_AXIS_Y_SHORT_KEYED = 27, //y / 32767.0f
            EULER_AXIS_Z_SHORT_KEYED = 28, //z / 32767.0f
        }

        public AnimationFormat(ChunkFileEntry fileEntry) {
            File = fileEntry;
            Read(new FileReader(fileEntry.Data, true));
        }

        private void Read(FileReader reader) {
            FileHeader = reader.ReadStruct<Header>();
            Tracks = reader.ReadMultipleStructs<Track>(FileHeader.TrackCount);
        }

        public void LoadAnimation()
        {
            Dictionary<uint, BoneAnim> groupList = new Dictionary<uint, BoneAnim>();

            BoneAnims.Clear();
            using (var reader = new FileReader(File.Data, true))
            {
                for (int i = 0; i < FileHeader.TrackCount; i++)
                {
                    //Add and combine tracks by hash.
                    if (!groupList.ContainsKey(Tracks[i].Hash))
                    {
                        var newGroup = new BoneAnim()
                        { Name = new HashString(Tracks[i].Hash), };

                        this.BoneAnims.Add(newGroup);
                        groupList.Add(Tracks[i].Hash, newGroup);
                    }

                    var group = groupList[Tracks[i].Hash];

                    reader.SeekBegin(Tracks[i].DataOffset);
                    if (Tracks[i].Type == TrackType.Scale)
                        group.Scale = ParseScaleTrack(reader, FileHeader, Tracks[i]);
                    else if (Tracks[i].Type == TrackType.Rotation)
                        group.Rotation = ParseRotationTrack(reader, FileHeader, Tracks[i]);
                    else if (Tracks[i].Type == TrackType.Translate)
                        group.Translation = ParseTranslationTrack(reader, FileHeader, Tracks[i]);
                }
            }
        }

        static TrackVector3 ParseScaleTrack(FileReader reader, Header header, Track track)
        {
            TrackVector3 t = new TrackVector3();
            return t;
        }

        static TrackVector3 ParseTranslationTrack(FileReader reader, Header header, Track track)
        {
            Console.WriteLine($"{(TranslationOpCode)track.OpCode}");

            TrackVector3 t = new TrackVector3();
            switch ((TranslationOpCode)track.OpCode)
            {
                case TranslationOpCode.Float32:
                    {
                        for (int f = 0; f < header.FrameCount; f++)
                        {
                            float[] loc = reader.ReadSingles(3);
                            t.Values.Add(new Vector3(loc[0], loc[1], loc[2]));
                        }
                    }
                    break;
                case TranslationOpCode.Float16:
                    {
                        for (int f = 0; f < header.FrameCount; f++)
                        {
                            float[] loc = reader.ReadHalfSingles(3);
                            t.Values.Add(new Vector3(loc[0], loc[1], loc[2]));
                        }
                    }
                    break;
                case TranslationOpCode.Float32Frames:
                    {
                        uint count = reader.ReadUInt32();
                        for (int f = 0; f < count; f++)
                        {
                            float[] loc = reader.ReadSingles(3);
                            t.Values.Add(new Vector3(loc[0], loc[1], loc[2]));
                        }
                    }
                    break;
                case TranslationOpCode.Float16Frames:
                    {
                        uint count = reader.ReadUInt16();
                        for (int f = 0; f < count; f++)
                        {
                            float[] loc = reader.ReadHalfSingles(3);
                            t.Values.Add(new Vector3(loc[0], loc[1], loc[2]));
                        }
                    }
                    break;
                case TranslationOpCode.Float32Constant:
                    {
                        float[] loc = reader.ReadSingles(3);
                        t.Values.Add(new Vector3(loc[0], loc[1], loc[2]));
                    }
                    break;
                case TranslationOpCode.Float16Constant:
                    {
                        float[] loc = reader.ReadHalfSingles(3);
                        t.Values.Add(new Vector3(loc[0], loc[1], loc[2]));
                    }
                    break;
                case TranslationOpCode.Float32AxisFrames:
                    {
                        uint axis = reader.ReadUInt32();
                        var unk = reader.ReadBytes(8);
                        uint count = reader.ReadUInt32();
                        for (int f = 0; f < count; f++)
                        {
                            float frame = (f * count) / header.FrameCount;
                            float value = reader.ReadSingle();

                            var loc = new Vector3(0, 0, 0);
                            if (axis == 0)
                                loc = new Vector3(value, 0, 0);
                            else if (axis == 1)
                                loc = new Vector3(0, value, 0);
                            else if (axis == 2)
                                loc = new Vector3(0, 0, value);

                            t.Values.Add(loc);
                        }
                    }
                    break;
                case TranslationOpCode.Float16AxisFrames:
                    {
                        uint unk1 = reader.ReadUInt32();
                        ushort unk2 = reader.ReadUInt16();
                        ushort count = reader.ReadUInt16();
                        for (int f = 0; f < count; f++)
                        {
                            float frame = (f * count) / header.FrameCount;
                            float positionX = (float)reader.ReadHalf();
                            var loc = new Vector3(positionX, 0, 0);
                            t.Values.Add(loc);
                        }
                    }
                    break;
            }
            return t;
        }

        static TrackQuat ParseRotationTrack(FileReader reader, Header header, Track track)
        {
            Console.WriteLine($"{(RotationOpCode)track.OpCode}");

            TrackQuat t = new TrackQuat();
            switch ((RotationOpCode)track.OpCode)
            {
                case RotationOpCode.QUAT_FLOAT_BAKED:  //4 Singles Frame Count
                {
                        for (int f = 0; f < header.FrameCount; f++) {
                            float[] quat = reader.ReadSingles(4);
                            t.Values.Add(new Quaternion(
                                quat[0],
                                quat[1],
                                quat[2],
                                quat[3]));
                        }
                    }
                    break;
                case RotationOpCode.QUAT_FLOAT_KEYED: //4 Singles Custom Count
                    {
                        uint count = reader.ReadUInt32();
                        for (int f = 0; f < count; f++)
                        {
                            float[] quat = reader.ReadSingles(4);
                            t.Values.Add(new Quaternion(
                                quat[0], 
                                quat[1],
                                quat[2], 
                                quat[3]));
                        }
                    }
                    break;
                case RotationOpCode.QUAT_SHORT_FIXED: 
                    {
                        short[] quat = reader.ReadInt16s(4);
                        t.Values.Add(new Quaternion(
                            quat[0] / 32767.0f,
                            quat[1] / 32767.0f,
                            quat[2] / 32767.0f,
                            quat[3] / 32767.0f));
                    }
                    break;
                case RotationOpCode.QUAT_SHORT_KEYED:
                    {
                        uint count = reader.ReadUInt32();
                        for (int f = 0; f < count; f++)
                        {
                            short[] quat = reader.ReadInt16s(4);
                            t.Values.Add(new Quaternion(
                                quat[0] / 32767.0f,
                                quat[1] / 32767.0f,
                                quat[2] / 32767.0f,
                                quat[3] / 32767.0f));
                        }
                    }
                    break;
                case RotationOpCode.EULER_AXIS_X_SHORT_BAKED:
                case RotationOpCode.EULER_AXIS_Y_SHORT_BAKED:
                case RotationOpCode.EULER_AXIS_Z_SHORT_BAKED:
                    {
                        int axis_target = track.OpCode - (int)RotationOpCode.EULER_AXIS_X_SHORT_KEYED;
                        for (int f = 0; f < header.FrameCount; f++)
                        {
                            float axis = reader.ReadInt16() * 32767.0f;

                            Vector3 euler = new Vector3();
                            if (axis_target == 0) euler.X = axis;
                            if (axis_target == 1) euler.Y = axis;
                            if (axis_target == 2) euler.Z = axis;

                            var q = MathHelper.FromEulerAngles(euler);
                            t.Values.Add(q);
                        }
                    }
                    break;
                case RotationOpCode.EULER_AXIS_X_SHORT_KEYED: //0x1A
                case RotationOpCode.EULER_AXIS_Y_SHORT_KEYED: //0x1B
                case RotationOpCode.EULER_AXIS_Z_SHORT_KEYED: //0x1C
                    {
                        int axis_target = track.OpCode - (int)RotationOpCode.EULER_AXIS_X_SHORT_KEYED;

                        uint count = reader.ReadUInt32();
                        for (int f = 0; f < count; f++)
                        {
                            float axis = reader.ReadInt16() * 32767.0f;

                            Vector3 euler = new Vector3();
                            if (axis_target == 0) euler.X = axis;
                            if (axis_target == 1) euler.Y = axis;
                            if (axis_target == 2) euler.Z = axis;

                          //  var q = MathHelper.FromEulerAngles(euler);
                           // t.Values.Add(q);
                        }
                    }
                    break;
                default:
                    Console.WriteLine($"Unknown Op Code! Track {track.Index} Type {track.Type} OpCode {track.OpCode}");
                    break;
            }
            return t;
        }
    }
}
