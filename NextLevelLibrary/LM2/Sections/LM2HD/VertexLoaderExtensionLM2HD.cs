using AvaloniaToolbox.Core.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace NextLevelLibrary.LM2
{
    public static class VertexLoaderExtensionLM2HD
    {
        //Attribute scale factors
        const float MORPH_SCALE = 1.0f / 256f;

        /// <summary>
        /// A list of layouts used to read/write vertex data determined by material presets
        /// </summary>
        public enum LayoutType
        {
            LayoutStride24,
            LayoutStride28,

            LayoutStride44,
            LayoutStride52,
            LayoutStride48,
            LayoutStride56,
            LayoutStride140,
            LayoutStride156,

            Skinning,
            SkinningColor,
            SkinningMorph,
            SkinningMorphColor,
            PositionUV2,
            PositionUV2Color,
            PositionUV2ColorRigid,
            PositionUVOnly,
            PositionColorOnly,
            PositionNormalsUV1,
            PositionNormalsUV2,
            PositionNormalsUV2Color,
            PositionNormalsOnly,
            PositionOnly,
            PositionNormalsUV2ColorComp,

            Unknown,
        }

        /// <summary>
        /// A list of vertex strides used per layout
        /// </summary>
        static Dictionary<LayoutType, int> StrideTable = new Dictionary<LayoutType, int>()
        {
            
            { LayoutType.LayoutStride24, 24 },

            { LayoutType.LayoutStride44, 44 },
            { LayoutType.LayoutStride48, 48 },
            { LayoutType.LayoutStride52, 52 },
            { LayoutType.LayoutStride56, 56 },
            { LayoutType.LayoutStride140, 140 },
            { LayoutType.LayoutStride156, 152 },

            { LayoutType.PositionColorOnly, 16 },
            { LayoutType.PositionUVOnly, 16 },
            { LayoutType.PositionNormalsOnly, 20 },
            { LayoutType.PositionOnly, 12 },
            { LayoutType.PositionUV2, 20 },
            { LayoutType.PositionNormalsUV2Color, 44 },

            { LayoutType.Unknown, 52 },

            { LayoutType.Skinning, 56 },
            { LayoutType.SkinningMorph, 144 },
            { LayoutType.SkinningMorphColor, 152 },
        };

        static Dictionary<string, int> Strides = new Dictionary<string, int>()
        {
            { "uvslidingmaterialgs", 12 },
            { "vertcolor", 16 },
            { "diffuseconstcolor", 20 },
            { "skyboxmaterial", 24 },
            { "diffusevertcolor", 24 },
            { "ghostnonskinmaterial", 28 },
            { "propsmaterial", 32 },
            { "windowmaterial", 32 },
            { "environmentspheremap", 36 },
            { "windowrigidskin", 36 },
            { "environmentspheremaprigidskin", 40 },
            { "snowmaterial", 44 },
            { "environmentmaterial", 44 },
            { "environmentspecularmaterial", 44 },
            { "ghostmaterial", 44 },
            { "luigieyematerial", 44 },
            { "diffuseskin", 44 },
            { "uvslidingmaterialspheremap", 44 },
            { "uvslidingmaterial", 44 },
            { "clothmaterial", 44 },
            { "watermaterial", 44 },
            { "icematerial", 44 },
            { "waterfallmaterial", 44 },
            { "pestmaterial", 48 },
            { "environmentspecularrigidskin", 48 },
            { "environmentrigidskin", 48 },
            { "uvslidingrigidskin", 48 },
            { "uvslidingmaterialerosive", 48 },
            { "waterrigidskin", 48 },
            { "snowrigidskin", 48 },
            { "icerigidskin", 48 },
            { "poltergustcone", 52 },
            { "luigimaterial", 56 },
            { "morphghostmaterial", 140 },
            { "morphluigieyematerial", 140 },
            { "morphpestmaterial", 144 },
            { "morphspecularmaterial", 144 },
            { "morphluigimaterial", 152 }
        };

        /// <summary>
        /// A list of game materials and the assigned vertex layout to use.
        /// </summary>
        static Dictionary<string, LayoutType> Layouts = new Dictionary<string, LayoutType>()
        {
            { "propsmaterial", LayoutType.PositionOnly },
            { "snowmaterial", LayoutType.PositionOnly },
            { "uvslidingmaterialspheremap", LayoutType.PositionOnly },
            { "environmentspheremap", LayoutType.PositionOnly },
            { "uvslidingmaterial", LayoutType.PositionOnly },
            { "windowmaterial", LayoutType.PositionOnly },
            { "watermaterial", LayoutType.PositionOnly },
            { "icematerial", LayoutType.PositionOnly },
            { "uvslidingmaterialgs", LayoutType.PositionOnly },

            { "uvslidingrigidskin", LayoutType.PositionUV2 },
            { "windowrigidskin", LayoutType.PositionUV2 },
            { "uvslidingmaterialerosive", LayoutType.PositionUV2 },
            { "waterrigidskin", LayoutType.PositionUV2 },
            { "snowrigidskin", LayoutType.PositionUV2 },
            { "icerigidskin", LayoutType.PositionUV2 },


            { "skyboxmaterial", LayoutType.LayoutStride24 },
            { "diffusevertcolor", LayoutType.LayoutStride24 },
            { "ghostnonskinmaterial", LayoutType.LayoutStride28 },
            { "diffuseskin", LayoutType.LayoutStride28 },

            { "ghostmaterial", LayoutType.LayoutStride44 },
            { "luigieyematerial", LayoutType.LayoutStride44 },
            { "pestmaterial", LayoutType.LayoutStride48 },
            { "poltergustcone", LayoutType.LayoutStride52 },
            { "luigimaterial", LayoutType.LayoutStride56 },
            { "morphghostmaterial",  LayoutType.LayoutStride140 },
            { "morphluigieyematerial", LayoutType.LayoutStride140 },
            { "morphpestmaterial", LayoutType.LayoutStride140 }, //144
            { "morphspecularmaterial", LayoutType.LayoutStride140 }, //144
            { "morphluigimaterial", LayoutType.LayoutStride156 },



            //todo check, 20 bytes
            { "diffuseconstcolor", LayoutType.PositionUVOnly },

            //confirmed
            { "environmentmaterial", LayoutType.PositionUV2Color },
            { "environmentspecularmaterial", LayoutType.PositionUV2Color },
            { "environmentspheremaprigidskin", LayoutType.PositionUV2ColorRigid },
            { "environmentspecularrigidskin", LayoutType.PositionUV2ColorRigid },
            { "environmentrigidskin", LayoutType.PositionUV2ColorRigid },
            { "clothmaterial", LayoutType.PositionUV2Color },
            { "waterfallmaterial", LayoutType.PositionUV2Color },
            { "vertcolor", LayoutType.PositionColorOnly },

        };

        public static int GetStride(string material)
        {
            if (Strides.ContainsKey(material))
                return Strides[material];

            return 0;
        }

        public static Vertex ReadVertexLayout(this FileReader reader, string material)
        {
            var layout = Layouts[material];
            switch (layout)
            {
                case LayoutType.LayoutStride24:
                case LayoutType.LayoutStride28:
                    return reader.ReadSkybox(layout);
                case LayoutType.LayoutStride52:
                case LayoutType.LayoutStride56:
                    return reader.ReadVertexLayout52(layout);
                case LayoutType.PositionUV2Color:
                case LayoutType.PositionUV2:
                case LayoutType.PositionOnly:
                case LayoutType.PositionColorOnly:
                case LayoutType.PositionUV2ColorRigid:
                case LayoutType.PositionUVOnly:
                    return ReadUV2LayoutColor(reader, layout);
                case LayoutType.LayoutStride44:
                case LayoutType.LayoutStride48:
                case LayoutType.LayoutStride156:
                    return reader.ReadVertexLayout156(layout);
            }
            return reader.ReadVertexLayout156(layout);
        }

        public static Vertex ReadVertexLayout52(this FileReader reader, LayoutType layout)
        {
            Vertex vertex = new Vertex();

            vertex.Position = new Vector3(reader.ReadSingle(),
                                  reader.ReadSingle(),
                                  reader.ReadSingle());

            vertex.Normal = Vector3.Normalize(new Vector3(
               (float)reader.ReadHalf(),
               (float)reader.ReadHalf(),
               (float)reader.ReadHalf()));

            if (layout == LayoutType.LayoutStride52)
            {
                vertex.Color = new Vector4(
                    reader.ReadByte() / 255f,
                    reader.ReadByte() / 255f,
                    reader.ReadByte() / 255f,
                    reader.ReadByte() / 255f);
                reader.ReadUInt16(); //0

                vertex.TexCoord0 = new Vector2(
                  reader.ReadSingle(),
                  reader.ReadSingle());

                byte[] boneIndices = reader.ReadBytes(4);
                float[] weights = reader.ReadSingles(3);

                for (int i = 0; i < 3; i++)
                {
                    if (weights[i] == 0)
                        continue;

                    vertex.BoneIndices.Add(boneIndices[i]);
                    vertex.BoneWeights.Add(weights[i]);
                }
                return vertex;
            }
            else
            {
                reader.ReadUInt32(); //todo
                reader.ReadUInt32(); //todo
                reader.ReadUInt16(); //0

                vertex.TexCoord0 = new Vector2(
                  reader.ReadSingle(),
                  reader.ReadSingle());

                vertex.Color = new Vector4(
                    reader.ReadByte() / 255f,
                    reader.ReadByte() / 255f,
                    reader.ReadByte() / 255f,
                    reader.ReadByte() / 255f);

                if (layout == LayoutType.LayoutStride52)
                {
                    return vertex;
                }

                byte[] boneIndices = reader.ReadBytes(4);
                float[] weights = reader.ReadSingles(3);

                for (int i = 0; i < 3; i++)
                {
                    if (weights[i] == 0)
                        continue;

                    vertex.BoneIndices.Add(boneIndices[i]);
                    vertex.BoneWeights.Add(weights[i]);
                }
            }

            return vertex;
        }

        public static Vertex ReadVertexLayout156(this FileReader reader, LayoutType layout)
        {
            Vertex vertex = new Vertex();

            var pos = reader.BaseStream.Position;

            vertex.Position = new Vector3(reader.ReadSingle(),
                                  reader.ReadSingle(),
                                  reader.ReadSingle());

            vertex.Normal = Vector3.Normalize(new Vector3(
               (float)reader.ReadHalf(),
               (float)reader.ReadHalf(),
               (float)reader.ReadHalf()));
            reader.ReadUInt16();

            if (layout == LayoutType.LayoutStride156)
            {
                reader.ReadSingle();
                reader.ReadSingle();

                vertex.TexCoord0 = new Vector2(
                   reader.ReadSingle(),
                   reader.ReadSingle());

                vertex.Color = new Vector4(
                    reader.ReadByte() / 255f,
                    reader.ReadByte() / 255f,
                    reader.ReadByte() / 255f,
                    reader.ReadByte() / 255f);

                byte[] boneIndices = reader.ReadBytes(4);
                float[] weights = reader.ReadSingles(3);

             //   if (weights.Sum(x => x) != 1.0f)
               //     throw new Exception();

                for (int i = 0; i < 3; i++)
                {
                    if (weights[i] == 0)
                        continue;

                    vertex.BoneIndices.Add(boneIndices[i]);
                    vertex.BoneWeights.Add(weights[i]);
                }

                //morph data
                //8 vector3
                float[] morph_data = reader.ReadSingles(24);
/*
                if (morph_data[15] != 128)
                {
                    vertex.Position = new Vector3(
                                 MathF.Floor(morph_data[15] * 0.00390625f),
                                 MathF.Floor(morph_data[16] * 0.00390625f),
                                 MathF.Floor(morph_data[17] * 0.00390625f));
                }

                Console.WriteLine(string.Join(",", morph_data.Select(x => x * 0.00390625f)));
                */
                return vertex;
            }
            else
            {
                vertex.TexCoord0 = new Vector2(
                  reader.ReadSingle(),
                  reader.ReadSingle());

                byte[] boneIndices = reader.ReadBytes(4);
                float[] weights = reader.ReadSingles(3);

                //if (weights.Sum(x => x) != 1.0f)
                //    throw new Exception();

                for (int i = 0; i < 3; i++)
                {
                    if (weights[i] == 0)
                        continue;

                    vertex.BoneIndices.Add(boneIndices[i]);
                    vertex.BoneWeights.Add(weights[i]);
                }

                //skinning no morph data
                if (layout == LayoutType.LayoutStride44)
                    return vertex;

                reader.ReadBytes(4);

                //skinning color no morph data
                if (layout == LayoutType.LayoutStride48)
                    return vertex;

                //here would be morph data which can be 104 bytes (26 floats)

                return vertex;
            }
        }

        public static Vertex ReadVertexLayout48(this FileReader reader, LayoutType layout)
        {
            var pos = new Vector3(reader.ReadSingle(),
                                  reader.ReadSingle(),
                                  reader.ReadSingle());

            var nrm = Vector3.Normalize(new Vector3(
               (float)reader.ReadHalf(),
               (float)reader.ReadHalf(),
               (float)reader.ReadHalf()));
            reader.ReadUInt16();

            var texcoord = new Vector2(
              reader.ReadSingle(),
              reader.ReadSingle());

            byte[] boneIndices = reader.ReadBytes(4);
            float[] weights = reader.ReadSingles(3);

            if (layout == LayoutType.LayoutStride44)
                return new Vertex()
                {
                    Position = new Vector3(pos.X, pos.Y, pos.Z),
                    Normal = new Vector3(nrm.X, nrm.Z, -nrm.Y),
                    TexCoord0 = texcoord,
                    Color = new Vector4(1),
                };

            byte[] color = reader.ReadBytes(4);

            return new Vertex()
            {
                Position = new Vector3(pos.X, pos.Y, pos.Z),
                Normal = new Vector3(nrm.X, nrm.Z, -nrm.Y),
                TexCoord0 = texcoord,
                Color = new Vector4(1),
            };
        }

        private static Vertex ReadSkybox(this FileReader reader, LayoutType layout)
        {
            Vertex vertex = new Vertex();
            vertex.Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            vertex.TexCoord0 = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            if (layout == LayoutType.LayoutStride28)
                vertex.TexCoord1 = new Vector4(reader.ReadSingle(), reader.ReadSingle(), 0, 0);

            byte[] color = reader.ReadBytes(4);

           // vertex.Color = new Vector4(color[0] / 255f, color[1] / 255f, color[2] / 255f, color[3] / 255f);

            return vertex;
        }

        private static Vertex ReadUV2LayoutColor(this FileReader reader, LayoutType layout)
        {
            Vertex vertex = new Vertex();

            vertex.Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            if (layout == LayoutType.PositionUV2ColorRigid)
            {
                reader.ReadUInt32();
            }

            if (layout == LayoutType.PositionUVOnly)
            {
                vertex.TexCoord0 = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                return vertex;
            }
            if (layout == LayoutType.PositionColorOnly)
            {
                vertex.Color = new Vector4(reader.ReadByte() / 255f, reader.ReadByte() / 255f, reader.ReadByte() / 255f, reader.ReadByte() / 255f);
                return vertex;
            }

            if (layout == LayoutType.PositionOnly)
                return vertex;

            if (layout == LayoutType.PositionUV2Color || layout == LayoutType.PositionUV2ColorRigid)
            {
                vertex.Normal = Vector3.Normalize(new Vector3(
                   (float)reader.ReadHalf(),
                   (float)reader.ReadHalf(),
                   (float)reader.ReadHalf()));
                vertex.Tangent = Vector4.Normalize(new Vector4(
                   (float)reader.ReadHalf(),
                   (float)reader.ReadHalf(),
                   (float)reader.ReadHalf(), 1));
            }

            vertex.TexCoord0 = new Vector2(reader.ReadSingle(), reader.ReadSingle());

            if (layout == LayoutType.PositionUV2Color || layout == LayoutType.PositionUV2ColorRigid)
                vertex.TexCoord1 = new Vector4(reader.ReadSingle(), reader.ReadSingle(), 0, 0);

            if (layout == LayoutType.PositionUV2Color)
            {
                vertex.Color = new Vector4(reader.ReadByte() / 255f, reader.ReadByte() / 255f,
                    reader.ReadByte() / 255f, reader.ReadByte() / 255f);
            }

            return vertex;
        }
    }
}
