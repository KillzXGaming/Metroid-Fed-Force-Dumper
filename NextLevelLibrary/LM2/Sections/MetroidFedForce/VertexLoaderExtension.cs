using AvaloniaToolbox.Core.IO;
using System.Numerics;

namespace NextLevelLibrary.MetroidFedForce
{
    public static class VertexLoaderExtension
    {
        //Attribute scale factors
        const float SHORT_POS_SCALE_1 = 1.0f / 4096.0f;
        const float SHORT_POS_SCALE_2 = 1.0f / 8192.0f;
        const float MORPH_SCALE = 1.0f / 256f;
        const float NORMAL_SCALE = 1.0f / 128f;
        const float COLOR_SCALE = 1.0f / 255f;
        const float WEIGHT_SCALE = 1.0f / 16384.0f;
        const float UV_SCALE = 1.0f / 1024.0f;

        //Gets short skinning position scale. Luigi has different scale value.
        static float GetShortPositionScale(string mat)
        {
            if (mat == "luigimaterial" || mat == "morphluigimaterial")
                return SHORT_POS_SCALE_2;
            return SHORT_POS_SCALE_1;
        }

        /// <summary>
        /// A list of layouts used to read/write vertex data determined by material presets
        /// </summary>
        public enum LayoutType
        {
            Skinning,
            SkinningColor,
            SkinningMorph,
            SkinningMorphColor,
            PositionUV2,
            PositionUV2Color,
            PositionColorOnly,
            PositionNormalsUV1,

            RigidPositionNormalsUV1,
            RigidPositionNormalsUV1Color,
            RigidPositionNormalsUV1ColorUV2,

            PositionNormalsUV1ColorExtra,
            PositionNormalsUV3Color,

            PositionNormalsUV2,
            PositionNormalsUV2Color,
            RigidPositionNormals,
            PositionOnly,
            PositionNormalsUV2ColorComp,
        }

        /// <summary>
        /// A list of vertex strides used per layout
        /// </summary>
        static Dictionary<LayoutType, int> StrideTable = new Dictionary<LayoutType, int>()
        {
            { LayoutType.Skinning, 22 },
            { LayoutType.SkinningColor, 26 },
            { LayoutType.SkinningMorph, 70 },
            { LayoutType.SkinningMorphColor, 74 },

            { LayoutType.PositionUV2, 20 },
            { LayoutType.PositionUV2Color, 24 },

            { LayoutType.PositionColorOnly, 16 },
            { LayoutType.RigidPositionNormals, 16 },
            { LayoutType.PositionOnly, 12 },

            { LayoutType.PositionNormalsUV1, 20 },
            { LayoutType.RigidPositionNormalsUV1Color, 24 }, // 24 pos (12 byte) + nrm (3 byte) 1 byte bone, uvs (4 byte), color (4 byte)
            { LayoutType.RigidPositionNormalsUV1ColorUV2, 28 }, // 24 pos (12 byte) + nrm (3 byte) 1 byte bone, uvs (4 byte)
            { LayoutType.RigidPositionNormalsUV1, 20 },


            { LayoutType.PositionNormalsUV1ColorExtra, 28 },
            { LayoutType.PositionNormalsUV3Color, 32 },

            { LayoutType.PositionNormalsUV2, 24 },
            { LayoutType.PositionNormalsUV2Color, 28 },
            { LayoutType.PositionNormalsUV2ColorComp, 36 },
        };

        /// <summary>
        /// A list of game materials and the assigned vertex layout to use.
        /// </summary>
        static Dictionary<string, LayoutType> Layouts = new Dictionary<string, LayoutType>()
        {
            { "enemyrigidskin",                 LayoutType.RigidPositionNormalsUV1Color }, 
            { "chameleonrigidskin",             LayoutType.RigidPositionNormalsUV1Color }, // 24
            { "rigidskinuvsliding",             LayoutType.RigidPositionNormalsUV1ColorUV2 }, // 28
            { "rigidskindiffuseconstcolor",     LayoutType.RigidPositionNormalsUV1 }, // 20
            { "rigidskinconstcolor",            LayoutType.RigidPositionNormals }, // 16

            { "uvsliding",                      LayoutType.PositionNormalsUV2Color }, // 28
            { "diffuselightmap",                LayoutType.PositionNormalsUV1ColorExtra }, // 28 
            { "chameleondiffuseconstcolor",     LayoutType.PositionNormalsUV1 }, // 20
            { "chameleonconstcolor",            LayoutType.PositionOnly }, // 12


            { "characterrigidskin",             LayoutType.RigidPositionNormalsUV1Color }, // 24
            { "transparent",                    LayoutType.RigidPositionNormalsUV1Color }, // 24
            { "decalrigidskin",                 LayoutType.RigidPositionNormalsUV1Color }, // 24
            { "chameleonmapgeo",                LayoutType.PositionOnly }, // 12
            { "chameleondiffuseconstcolorrimlight", LayoutType.PositionNormalsUV1 }, // 20 
            { "weaponrigidskin",                LayoutType.RigidPositionNormalsUV1Color }, // 24 
            { "rigidskindiffusevertcolor",      LayoutType.RigidPositionNormalsUV1Color }, // 24 
            { "cockpitrigidskin",               LayoutType.RigidPositionNormalsUV1Color }, // 24 
            { "cockpitenvironmentmaprigidskin", LayoutType.PositionNormalsUV2Color }, // 28 
            { "chameleondiffuselit",            LayoutType.PositionNormalsUV1 }, // 20 
            { "diffuselightmaprimlight",        LayoutType.PositionNormalsUV2Color }, // 28 
            { "SkyboxMaterial",                 LayoutType.PositionNormalsUV1 }, // 20 


            { "luigieyematerial",              LayoutType.Skinning },
            { "diffuseskin",                   LayoutType.Skinning },
            { "luigimaterial",                 LayoutType.Skinning }, //SHORT_POS_SCALE_2
            { "pestmaterial",                  LayoutType.SkinningColor },
            { "morphghostmaterial",            LayoutType.SkinningMorph  }, 
            { "morphluigimaterial",            LayoutType.SkinningMorph }, //SHORT_POS_SCALE_2
            { "morphpestmaterial",             LayoutType.SkinningMorphColor },
            { "windowrigidskin",               LayoutType.PositionUV2 },
            { "skyboxmaterial",                LayoutType.PositionUV2 },
            { "ghostnonskinmaterial",          LayoutType.PositionUV2 },
            { "diffusevertcolor",              LayoutType.PositionUV2Color },
            { "vertcolor",                     LayoutType.PositionColorOnly },
            { "windowmaterial",                LayoutType.PositionNormalsUV1 },
            { "propsmaterial",                 LayoutType.PositionNormalsUV2 },
            { "environmentspheremap",          LayoutType.PositionNormalsUV2 },
            { "environmentspheremaprigidskin", LayoutType.PositionNormalsUV2 },
            { "moolahmaterial",                LayoutType.PositionNormalsUV2 },
            { "uvslidingrigidskin",            LayoutType.PositionNormalsUV2Color },
            { "environmentmaterial",           LayoutType.PositionNormalsUV2Color },
            { "environmentrigidskin",          LayoutType.PositionNormalsUV2Color },
            { "environmentspecularrigidskin" , LayoutType.PositionNormalsUV2Color },
            { "environmentspecularmaterial",   LayoutType.PositionNormalsUV2Color },
            { "uvslidingmaterial",             LayoutType.PositionNormalsUV2Color },
            { "diffuseconstcolor",             LayoutType.RigidPositionNormals  }, 
            { "clothmaterial",                 LayoutType.PositionNormalsUV2ColorComp }, 
            { "uvslidingmaterialgs",           LayoutType.PositionOnly }, 
        };

        public static int GetStride(uint hash)
        {
            //Layouts currently use material name for cleanness. Would be better if the type can be extracted by hash instead
            string material = new HashString(hash).String;
            if (!Layouts.ContainsKey(material))
                return 0;

            var layout = Layouts[material];
            return StrideTable[layout];
        }

        public static Vertex ReadVertexLayout(this FileReader reader, uint hash)
        {
            //Layouts currently use material name for cleanness. Would be better if the type can be extracted by hash instead
            string material = new HashString(hash).String;
            if (!Layouts.ContainsKey(material))
                return new Vertex();

            var layout = Layouts[material];

            switch (layout)
            {
                case LayoutType.Skinning:
                case LayoutType.SkinningColor:
                case LayoutType.SkinningMorph:
                case LayoutType.SkinningMorphColor:
                    return ReadSkinningLayouts(reader, material, layout);
                case LayoutType.PositionUV2Color:
                case LayoutType.PositionUV2:
                case LayoutType.PositionOnly:
                    return ReadUV2LayoutColor(reader, layout);
                case LayoutType.RigidPositionNormals:
                case LayoutType.PositionNormalsUV1:
                case LayoutType.PositionNormalsUV2:
                case LayoutType.PositionNormalsUV2Color:
                case LayoutType.PositionNormalsUV2ColorComp:
                case LayoutType.RigidPositionNormalsUV1Color:
                case LayoutType.RigidPositionNormalsUV1ColorUV2:
                case LayoutType.PositionNormalsUV1ColorExtra:
                case LayoutType.PositionNormalsUV3Color:
                case LayoutType.RigidPositionNormalsUV1:
                    return ReadUV2LayoutNormals(reader, layout);
                case LayoutType.PositionColorOnly:
                    return ReadPositionColorsOnly(reader, layout);
                default:
                    throw new Exception("Unsupported vertex layout!");
            }
        }

        public static void WriteVertexLayout(this FileWriter writer,  Vertex vertex, uint hash)
        {
            //Layouts currently use material name for cleanness. Would be better if the type can be extracted by hash instead
            string material = new HashString(hash).String;
            var layout = Layouts[material];

            switch (layout)
            {
                case LayoutType.Skinning:
                case LayoutType.SkinningColor:
                case LayoutType.SkinningMorph:
                case LayoutType.SkinningMorphColor:
                    WriteSkinningLayout(writer, vertex, material, layout);
                    break;
                case LayoutType.PositionUV2Color:
                case LayoutType.PositionUV2:
                case LayoutType.PositionOnly:
                     WriteUV2LayoutClr(writer, vertex, material, layout);
                    break;
                case LayoutType.RigidPositionNormals:
                case LayoutType.PositionNormalsUV1:
                case LayoutType.PositionNormalsUV2:
                case LayoutType.PositionNormalsUV2Color:
                case LayoutType.PositionNormalsUV2ColorComp:
                     WriteUV2LayoutNrm(writer, vertex, material, layout);
                    break;
                case LayoutType.PositionColorOnly:
                     WritePositionColorsOnly(writer, vertex, material, layout);
                    break;
                default:
                    throw new Exception("Unsupported vertex layout!");
            }
        }

        private static Vertex ReadSkinningLayouts(this FileReader reader, string mat, LayoutType type) {
            Vertex vertex = new Vertex();

            vertex.Position = new Vector3(reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16()) * GetShortPositionScale(mat);
            vertex.Normal = new Vector3(reader.ReadSByte(), reader.ReadSByte(), reader.ReadSByte()) * NORMAL_SCALE;
            byte boneIndex1 = reader.ReadByte();
            vertex.TexCoord0 = new Vector2(reader.ReadInt16(), reader.ReadInt16()) * UV_SCALE;

            byte boneIndex2 = reader.ReadByte();
            byte boneIndex3 = reader.ReadByte();
            ushort[] weights = reader.ReadUInt16s(3);

            vertex.BoneIndices.Add(boneIndex2 / 3);
            vertex.BoneIndices.Add(boneIndex3 / 3);
            vertex.BoneIndices.Add(boneIndex1 / 3);

            vertex.BoneWeights.Add(weights[0] * WEIGHT_SCALE);
            vertex.BoneWeights.Add(weights[1] * WEIGHT_SCALE);
            vertex.BoneWeights.Add(weights[2] * WEIGHT_SCALE);

            if (type == LayoutType.SkinningMorph || type == LayoutType.SkinningMorphColor)
            {
                //8 bytes each, 6 total
                for (int i = 0; i < 6; i++)
                    vertex.Morphs[i] = new Vector4(reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16()) * MORPH_SCALE;
            }

            if (type == LayoutType.SkinningColor || type == LayoutType.SkinningMorphColor)
            {
                vertex.Color = new Vector4(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte()) * COLOR_SCALE;
            }

            return vertex;
        }

        private static Vertex ReadUV2LayoutColor(this FileReader reader, LayoutType layout)
        {
            Vertex vertex = new Vertex();

            vertex.Position = new Vector3( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

            if (layout == LayoutType.PositionOnly)
                return vertex;

            vertex.TexCoord0 = new Vector2(reader.ReadInt16(), reader.ReadInt16()) * UV_SCALE;
            vertex.TexCoord2 = new Vector2(reader.ReadInt16(), reader.ReadInt16()) * UV_SCALE;

            if (layout == LayoutType.PositionUV2Color)
            {
                vertex.Color = new Vector4(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte()) * COLOR_SCALE;
            }

            return vertex;
        }

        private static Vertex ReadUV2LayoutNormals(this FileReader reader, LayoutType layout)
        {
            Vertex vertex = new Vertex();

            vertex.Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            vertex.Normal = Vector3.Normalize(new Vector3(
                reader.ReadSByte(),
                reader.ReadSByte(),
                reader.ReadSByte()) * NORMAL_SCALE);
            byte p = reader.ReadByte(); //padding

            if (layout == LayoutType.RigidPositionNormalsUV1Color ||
                layout == LayoutType.RigidPositionNormals ||
                layout == LayoutType.RigidPositionNormalsUV1ColorUV2 ||
                layout == LayoutType.RigidPositionNormalsUV1) 
            {
                vertex.BoneIndices.Add(p / 3);
                vertex.BoneWeights.Add(1);
            }

            if (layout == LayoutType.RigidPositionNormals) //Position + normals
                return vertex;

            vertex.TexCoord0 = new Vector2(reader.ReadInt16(), reader.ReadInt16()) * UV_SCALE;

            if (layout == LayoutType.PositionNormalsUV1) //Position + normals + tex coord 0
                return vertex;

            if (layout == LayoutType.RigidPositionNormalsUV1Color) //Position + normals + tex coord 0 + color
            {
                vertex.Color = new Vector4(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte()) * COLOR_SCALE;
                return vertex;
            }
            if (layout == LayoutType.PositionNormalsUV1ColorExtra)
            {
                vertex.Color = new Vector4(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte()) * COLOR_SCALE;
                vertex.TexCoord2 = new Vector2(reader.ReadInt16(), reader.ReadInt16()) * UV_SCALE;
                return vertex;
            }
            if (layout == LayoutType.PositionNormalsUV3Color)
            {
                vertex.TexCoord2 = new Vector2(reader.ReadInt16(), reader.ReadInt16()) * UV_SCALE;
                vertex.TexCoord3 = new Vector2(reader.ReadInt16(), reader.ReadInt16()) * UV_SCALE;
                vertex.Color = new Vector4(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte()) * COLOR_SCALE;
                return vertex;
            }
            if (layout == LayoutType.RigidPositionNormalsUV1ColorUV2)
            {
                vertex.TexCoord2 = new Vector2(reader.ReadInt16(), reader.ReadInt16()) * UV_SCALE;
                vertex.Color = new Vector4(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte()) * COLOR_SCALE;
                return vertex;
            }

            vertex.TexCoord2 = new Vector2(reader.ReadInt16(), reader.ReadInt16()) * UV_SCALE;

            if (layout == LayoutType.PositionNormalsUV2Color) //Position + normals + tex coord 0 + tex coord 1 + color
                vertex.Color = new Vector4(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte()) * COLOR_SCALE;

            return vertex;
        }

        private static Vertex ReadPositionColorsOnly(this FileReader reader, LayoutType layout)
        {
            Vertex vertex = new Vertex();
            vertex.Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            vertex.Color = new Vector4(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte()) * COLOR_SCALE;
            return vertex;
        }

        private static void WriteSkinningLayout(FileWriter writer, Vertex vertex, string mat, LayoutType type)
        {
            writer.Write((short)(vertex.Position.X / GetShortPositionScale(mat)));
            writer.Write((short)(vertex.Position.Y / GetShortPositionScale(mat)));
            writer.Write((short)(vertex.Position.Z / GetShortPositionScale(mat)));

            writer.Write((sbyte)(vertex.Normal.X / NORMAL_SCALE));
            writer.Write((sbyte)(vertex.Normal.Y / NORMAL_SCALE));
            writer.Write((sbyte)(vertex.Normal.Z / NORMAL_SCALE));

            writer.Write((byte)(vertex.BoneIndices[2] * 3));

            writer.Write((short)(vertex.TexCoord0.X / UV_SCALE));
            writer.Write((short)(vertex.TexCoord0.Y / UV_SCALE));

            writer.Write((byte)(vertex.BoneIndices[0] * 3));
            writer.Write((byte)(vertex.BoneIndices[1] * 3));

            writer.Write((short)(vertex.BoneWeights[0] / WEIGHT_SCALE));
            writer.Write((short)(vertex.BoneWeights[1] / WEIGHT_SCALE));
            writer.Write((short)(vertex.BoneWeights[2] / WEIGHT_SCALE));

            if (type == LayoutType.SkinningMorph || type == LayoutType.SkinningMorphColor)
            {
                for (int i = 0; i < 6; i++)
                {
                    writer.Write((short)(vertex.Morphs[i].X / MORPH_SCALE));
                    writer.Write((short)(vertex.Morphs[i].Y / MORPH_SCALE));
                    writer.Write((short)(vertex.Morphs[i].Z / MORPH_SCALE));
                    writer.Write((short)(vertex.Morphs[i].W / MORPH_SCALE));
                }
            }

            if (type == LayoutType.SkinningColor || type == LayoutType.SkinningMorphColor)
            {
                writer.Write((byte)(vertex.Color.X / COLOR_SCALE));
                writer.Write((byte)(vertex.Color.Y / COLOR_SCALE));
                writer.Write((byte)(vertex.Color.Z / COLOR_SCALE));
                writer.Write((byte)(vertex.Color.W / COLOR_SCALE));
            }
        }

        private static void WriteUV2LayoutClr(FileWriter writer, Vertex vertex, string mat, LayoutType layout)
        {
            writer.Write(vertex.Position.X);
            writer.Write(vertex.Position.Y);
            writer.Write(vertex.Position.Z);

            if (layout == LayoutType.PositionOnly)
                return;

            writer.Write((short)(vertex.TexCoord0.X / UV_SCALE));
            writer.Write((short)(vertex.TexCoord0.Y / UV_SCALE));
            writer.Write((short)(vertex.TexCoord2.X / UV_SCALE));
            writer.Write((short)(vertex.TexCoord2.Y / UV_SCALE));

            if (layout == LayoutType.PositionUV2Color)
            {
                writer.Write((byte)(vertex.Color.X / COLOR_SCALE));
                writer.Write((byte)(vertex.Color.Y / COLOR_SCALE));
                writer.Write((byte)(vertex.Color.Z / COLOR_SCALE));
                writer.Write((byte)(vertex.Color.W / COLOR_SCALE));
            }
        }

        private static void WriteUV2LayoutNrm(FileWriter writer, Vertex vertex, string mat, LayoutType layout)
        {
            writer.Write(vertex.Position.X);
            writer.Write(vertex.Position.Y);
            writer.Write(vertex.Position.Z);

            writer.Write((sbyte)(vertex.Normal.X / NORMAL_SCALE));
            writer.Write((sbyte)(vertex.Normal.Y / NORMAL_SCALE));
            writer.Write((sbyte)(vertex.Normal.Z / NORMAL_SCALE));
            writer.Write((sbyte)(0)); //padding

            if (layout == LayoutType.RigidPositionNormals)
                return;

            writer.Write((short)(vertex.TexCoord0.X / UV_SCALE));
            writer.Write((short)(vertex.TexCoord0.Y / UV_SCALE));

            if (layout == LayoutType.PositionNormalsUV1) //Position + normals + tex coord 0
                return;

            writer.Write((short)(vertex.TexCoord2.X / UV_SCALE));
            writer.Write((short)(vertex.TexCoord2.Y / UV_SCALE));

            if (layout == LayoutType.PositionNormalsUV2Color)
            {
                writer.Write((byte)(vertex.Color.X / COLOR_SCALE));
                writer.Write((byte)(vertex.Color.Y / COLOR_SCALE));
                writer.Write((byte)(vertex.Color.Z / COLOR_SCALE));
                writer.Write((byte)(vertex.Color.W / COLOR_SCALE));
            }
        }

        private static void WritePositionColorsOnly(FileWriter writer, Vertex vertex, string mat, LayoutType type)
        {
            writer.Write(vertex.Position.X);
            writer.Write(vertex.Position.Y);
            writer.Write(vertex.Position.Z);
            writer.Write((byte)(vertex.Color.X / COLOR_SCALE));
            writer.Write((byte)(vertex.Color.Y / COLOR_SCALE));
            writer.Write((byte)(vertex.Color.Z / COLOR_SCALE));
            writer.Write((byte)(vertex.Color.W / COLOR_SCALE));
        }
    }
}
