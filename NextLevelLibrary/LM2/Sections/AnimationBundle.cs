using NextLevelLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace FileConverter
{
    public class AnimationBundle
    {
        public uint Flag;

        public List<HashEntry> Hashes = new List<HashEntry>();

        public AnimationBundle() { }

        public AnimationBundle(Stream stream) { Read(stream); }


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
                        HashValue = Path.ToString("X"),
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
                    writer.Write(uint.Parse(kv.Name, System.Globalization.NumberStyles.HexNumber, null));
                    writer.Write(uint.Parse(kv.Path, System.Globalization.NumberStyles.HexNumber, null));
                }
            }
            return mem.ToArray();
        }

        public void Export(string path)
        {
            XmlHelper<AnimationBundle>.SaveXml(this, path);  
        }

        public static AnimationBundle Import(string path)
        {
            return XmlHelper<AnimationBundle>.LoadXml(path);
        }

        public static byte[] ImportAndSave(string path)
        {
            var anim = AnimationBundle.Import(path);
            return anim.Write();
        }

        public class HashEntry
        {
            [XmlElement]
            public string HashValue;
            [XmlElement]
            public string Name;
            [XmlElement]
            public string Path;
        }
    }
}
