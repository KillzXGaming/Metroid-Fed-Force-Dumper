using AvaloniaToolbox.Core.IO;
using NextLevelLibrary.LM2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace NextLevelLibrary.Files.LM2
{
    public class MaterialHelperLM2HD
    {
        public static void ReadMaterialParam(FileReader reader,
            string material, uint[] pointers, ModelFormat_LM2HD.MaterialData mat)
        {
            switch (material)
            {
                case "EnvironmentRigidSkin":
                    mat.DiffuseTexHash = FetchTextureHash(reader, pointers[10], 0);
                    mat.ShadowTexHash = FetchTextureHash(reader, pointers[10], 4);
                    mat.NormalTexHash = FetchTextureHash(reader, pointers[11], 16);
                    break;
                case "UVSlidingMaterialErosive": //2 noise, 14 0x0 has another noise
                    mat.DiffuseTexHash = FetchTextureHash(reader, pointers[10], 0);
                    break;
                case "EnvironmentSpecularMaterial":
                case "EnvironmentSpecularRigidSkin":
                    //rgb diffuse, alpha = specular
                    mat.DiffuseTexHash = FetchTextureHash(reader, pointers[10], 0);
                    mat.ShadowTexHash = FetchTextureHash(reader, pointers[10], 4); 
                    mat.NormalTexHash = FetchTextureHash(reader, pointers[11], 16);
                    break;
                case "PestMaterial":
                    mat.DiffuseTexHash = FetchTextureHash(reader, pointers[10], 32);
                    break;
                case "UVSlidingMaterialSphereMap":
                    mat.DiffuseTexHash = FetchTextureHash(reader, pointers[11], 0);
                    break;
                case "IceMaterial":
                    mat.DiffuseTexHash = FetchTextureHash(reader, pointers[11], 8);
                    break;
                case "SnowMaterial":
                    mat.DiffuseTexHash = FetchTextureHash(reader, pointers[14], 0);
                    break;
                case "MorphLuigiEyeMaterial":
                    //pupil
                    mat.DiffuseTexHash = FetchTextureHash(reader, pointers[12], 0);
                    FetchTextureHash(reader, pointers[12], 4); //mask
                    break;
                case "EnvironmentSphereMap":
                    mat.DiffuseTexHash = FetchTextureHash(reader, pointers[12], 0);
                    FetchTextureHash(reader, pointers[12], 4); //sphere
                    FetchTextureHash(reader, pointers[12], 8); //white
                    FetchTextureHash(reader, pointers[12], 12); //black
                    break;
                case "IceRigidSkin":
                    mat.DiffuseTexHash = FetchTextureHash(reader, pointers[12], 0);
                    break;
                case "SnowRigidSkin":
                    mat.DiffuseTexHash = FetchTextureHash(reader, pointers[15], 0);
                    break;
                case "MorphSpecularMaterial":
                    //rgb diffuse, alpha = specular
                    mat.DiffuseTexHash = FetchTextureHash(reader, pointers[12], 0);
                    break;
                case "WindowMaterial":
                    mat.DiffuseTexHash = FetchTextureHash(reader, pointers[13], 0);
                    FetchTextureHash(reader, pointers[13], 4); //white
                    break;
                case "LuigiMaterial":
                    mat.DiffuseTexHash = FetchTextureHash(reader, pointers[14], 0);
                    mat.SpecmapTexHash = FetchTextureHash(reader, pointers[14], 4);
                    FetchTextureHash(reader, pointers[14], 8); //black
                    FetchTextureHash(reader, pointers[14], 12); //black
                    break;
                case "MorphLuigiMaterial":
                    mat.DiffuseTexHash = FetchTextureHash(reader, pointers[18], 0);
                    mat.NormalTexHash = FetchTextureHash(reader, pointers[20], 16); //normal map
                    break;
                case "UVSlidingMaterialGS":
                case "DiffuseVertColor":
                    mat.DiffuseTexHash = FetchTextureHash(reader, pointers[3], 0);
                    break;
                case "SkyboxMaterial":
                case "DiffuseConstColor":
                    mat.DiffuseTexHash = FetchTextureHash(reader, pointers[4], 0);
                    break;
                case "DiffuseSkin":
                    mat.DiffuseTexHash = FetchTextureHash(reader, pointers[5], 0);
                    break;
                case "PoltergustCone":
                    mat.DiffuseTexHash = FetchTextureHash(reader, pointers[5], 0);
                    break;
                case "ClothMaterial":
                    mat.DiffuseTexHash = FetchTextureHash(reader, pointers[7], 32);
                    break;
                case "UVSlidingRigidSkin":
                case "PropsMaterial":
                case "GhostNonSkinMaterial":
                    mat.DiffuseTexHash = FetchTextureHash(reader, pointers[8], 0);
                    break;
                case "WaterfallMaterial":
                case "UVSlidingMaterial":
                    mat.DiffuseTexHash = FetchTextureHash(reader, pointers[9], 0);
                    break;
                case "MorphGhostMaterial":
                case "GhostMaterial":
                    mat.DiffuseTexHash = FetchTextureHash(reader, pointers[9], 0);
                    break;
                case "LuigiEyeMaterial":
                    mat.DiffuseTexHash = FetchTextureHash(reader, pointers[9], 0);
                    FetchTextureHash(reader, pointers[9], 4);
                    break;
                case "EnvironmentMaterial":
                    mat.DiffuseTexHash = FetchTextureHash(reader, pointers[9], 0);
                    FetchTextureHash(reader, pointers[9], 4); //white
                    FetchTextureHash(reader, pointers[10], 0); //black
                    break;
                case "WaterMaterial":
                    mat.DiffuseTexHash = FetchTextureHash(reader, pointers[10], 0); 
                    FetchTextureHash(reader, pointers[10], 4); //black
                    mat.NormalTexHash = FetchTextureHash(reader, pointers[9], 16);
                    break;
            }
        }

        static uint FetchTextureHash(FileReader reader, uint pointer, uint offset)
        {
            if (pointer == uint.MaxValue) return 0; //unused

            reader.SeekBegin(pointer + offset);
            return reader.ReadUInt32();
        }
    }
}
