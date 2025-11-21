using AvaloniaToolbox.Core.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace NextLevelLibrary.MetroidFedForce
{
    public class MaterialLoaderHelper
    {
        public static List<uint> Hashes = new List<uint>();
 
        public static void ParseMaterials(Stream materialStream, List<ModelFormat.MeshInfo> meshes)
        {
            List<uint[]> materialPointers = meshes.Select(x => x.MaterialPointers).ToList();

            //Get lookup pointers
            using (var materialReader = new FileReader(materialStream, true))
            {
                for (int i = 0; i < meshes.Count; i++)
                {
                    var mat = new ModelFormat.MaterialData();
                    string materialPreset = Hashing.GetString(meshes[i].Header.MaterialVariantHash);
                    uint[] pointers = materialPointers[i];

                    //Get pointer indices based on the preset
                    int texturePointerIndex = GetTextureSlotLookupIndex(materialPreset);
                    if (texturePointerIndex != -1)
                    {
                        //Set slot parameters
                        materialReader.SeekBegin(pointers[texturePointerIndex]);
                        mat.DiffuseTextureHash = materialReader.ReadUInt32();
                        if (mat.HasShadowMap)
                            mat.ShadowTextureHash = materialReader.ReadUInt32();
                    }
                    else
                    {
                        uint hash = SearchTextureLookups(materialReader, pointers);
                        if (hash != 0)
                        {
                            mat.DiffuseTextureHash = hash;
                        }
                    }
                    meshes[i].Material = mat;
                }
            }
        }

        //Searchs all points to find valid texture pointers
        static uint SearchTextureLookups(FileReader materialReader, uint[] pointers)
        {
            for (int i = 0; i < pointers.Length; i++) {
                if (pointers[i] != uint.MaxValue && pointers[i] != 0) {
                    materialReader.SeekBegin(pointers[i]);
                    uint hash = materialReader.ReadUInt32();
                    if (Hashing.TextureHashCache.Contains(hash))
                    {
                        return hash;
                    }
                }
            }
            return 0;
        }

        static int GetTextureSlotLookupIndex(string preset)
        {
            switch (preset)
            {
                default:    
                    return -1;
            }
        }
    }
}
