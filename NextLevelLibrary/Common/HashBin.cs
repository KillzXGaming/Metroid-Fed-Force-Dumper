using AvaloniaToolbox.Core.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NextLevelLibrary
{
    public class HashBin
    {
        public Dictionary<uint, string> Hashes = new Dictionary<uint, string>();

        public HashBin(string path, bool bigEndian = false) {
            Load(File.OpenRead(path), bigEndian);
        }

        public HashBin(Stream stream, bool bigEndian = false) {
            Load(stream, bigEndian);
        }

        private void Load(Stream stream, bool bigEndian = false)
        {
            using (var reader = new FileReader(stream))
            {
                reader.SetByteOrder(bigEndian);
                uint count = reader.ReadUInt32();
                for (int i = 0; i < count; i++)
                {
                    uint hash = reader.ReadUInt32();
                    uint offset = reader.ReadUInt32();
                    using (reader.TemporarySeek(4 + count * 8 + offset, SeekOrigin.Begin))
                    {
                        string str = reader.ReadStringZeroTerminated();
                        Hashes.Add(hash, str);
                    }
                }
            }
        }
    }
}
