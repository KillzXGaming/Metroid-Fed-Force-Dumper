using NextLevelLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace FileConverter
{
    public class AssetList
    {
        public uint Flag;

        public List<HashEntry> Hashes = new List<HashEntry>();

        public AssetList() { }

        public AssetList(Stream stream) { Read(stream); }


        public void Read(Stream stream)
        {
            using (var reader = new BinaryReader(stream))
            {
                uint num_entries = reader.ReadUInt32();
                Flag = reader.ReadUInt32();
                for (int i = 0; i < num_entries; i++)
                {
                    uint Name = reader.ReadUInt32();
                    uint Path = reader.ReadUInt32();
                    Hashes.Add(new HashEntry() {
                        Name = Hashing.GetString(Name), 
                        Path = Hashing.GetString(Path) });
                }
            }
        }

        public byte[] Write()
        {
            var mem = new MemoryStream();
            using (var writer = new BinaryWriter(mem))
            {
                writer.Write(this.Hashes.Count);
                writer.Write(this.Flag);
                foreach (var kv in this.Hashes)
                {
                    writer.Write(Hashing.GetHashFromString(kv.Name));
                    writer.Write(Hashing.GetHashFromString(kv.Path));
                }
            }
            return mem.ToArray();
        }

        public void Export(string path)
        {
            XmlHelper<AssetList>.SaveXml(this, path);  
        }

        public static AssetList Import(string path)
        {
            return XmlHelper<AssetList>.LoadXml(path);
        }

        public static byte[] ImportAndSave(string path)
        {
            var anim = AssetList.Import(path);
            return anim.Write();
        }

        public class HashEntry
        {
            [XmlElement]
            public string Name;
            [XmlElement]
            public string Path;
        }
    }
}
