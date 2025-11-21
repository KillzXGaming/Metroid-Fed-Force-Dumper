using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NextLevelLibrary
{
    public class HashString
    {
        public uint Value;

        public string String
        {
            get { return Hashing.GetString(Value); }
            set { Value = Hashing.StringToHash(value); }
        }

        public HashString() { }

        public HashString(string str)
        {
            String = str;
        }

        public HashString(uint hash)
        {
            Value = hash;
        }

        public override string ToString() => String;
    }
}
