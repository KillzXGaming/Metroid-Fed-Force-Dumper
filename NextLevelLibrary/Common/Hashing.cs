using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NextLevelLibrary
{
    public class Hashing
    {
        public static List<uint> TextureHashCache = new List<uint>();

        //From (Works as tested comparing hashbin strings/hashes
        //https://gist.github.com/RoadrunnerWMC/f4253ef38c8f51869674a46ee73eaa9f

        private static Dictionary<uint, string> hashStrings = new Dictionary<uint, string>();

        public static Dictionary<uint, string> HashStrings
        {
            get
            {
                if (hashStrings?.Count == 0)
                    LoadHashes();

                return hashStrings;
            }
        }

        public static void AddString(string str)
        {
            if (hashStrings?.Count == 0)
                LoadHashes();

            LoadHash(str);
        }

        /// <summary>
        /// Calculates a string to a hash value used by NLG files.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="caseSensative"></param>
        /// <returns></returns>
        public static uint StringToHash(string name, bool caseSensative = false)
        {
            byte[] data = Encoding.Default.GetBytes(name);

            int h = -1;
            for (int i = 0; i < data.Length; i++)
            {
                int c = (int)data[i];
                if (caseSensative && ((c - 65) & 0xFFFFFFFF) <= 0x19)
                    c |= 0x20;

                h = (int)((h * 33 + c) & 0xFFFFFFFF);
            }
            return (uint)h;
        }

        /// <summary>
        /// Calculates a hash from a given string.
        /// The input must be a hex string when not defined.
        /// </summary>
        public static uint GetHashFromString(string str, bool caseSensative = false)
        {
            //try to get a hash as uint32 hex number
            //if fails, consider it a proper string to convert to a hash
            bool success = uint.TryParse(str, System.Globalization.NumberStyles.HexNumber, null, out uint hash);
            if (!success) return Hashing.StringToHash(str, caseSensative);

            return hash;
        }

        /// <summary>
        /// Attempts to get the string from the hash using the current string database.
        /// </summary>
        public static string GetString(uint hash)
        {
            //check if hash is in string database
            if (HashStrings.ContainsKey(hash)) return HashStrings[hash];
            //else return hex of hash
            return hash.ToString("X");
        }

        private static void LoadHashes()
        {
            void AddHash(uint hash, string key)
            {
                if (hashStrings.ContainsKey(hash))
                    return;

                hashStrings.Add(hash, key); 
            }

            HashBin bin = new HashBin(new MemoryStream(Resources.Resource.BundleHashID));
            foreach (var file in bin.Hashes)
            {
                AddHash(file.Key, file.Value);
                if (file.Value.Contains("/"))
                {
                    var split = file.Value.Split('/');
                    if (split.Length == 2)
                    {
                        AddHash(StringToHash(split[0], true), split[0]);
                    }
                }
            }

            string[] languages = new string[]
{
                "french","german","japanese","korean",
                "nafrench","naspanish","portuguese","russian",
                "spanish","italian","dutch","naportuguese",
                "english","ukenglish","cnsimplified","cntraditional",
};
            foreach (var language in languages)
            {
                LoadHash(language);
                LoadHash(language + "hw");
                LoadHash("debug" + language);
                LoadHash("debug" + language + "hw");
            }

            LoadHash("debughw");
            LoadHash("shader");
            LoadHash("feloc");
            LoadHash("material");
            LoadHash("lightfield");
            LoadHash("audiobank");
            LoadHash("bank");


            if (!hashStrings.ContainsKey(2245049458)) hashStrings.Add(2245049458, "localization");
            if (!hashStrings.ContainsKey(3731787994)) hashStrings.Add(3731787994, "gui");
            if (!hashStrings.ContainsKey(3354933275)) hashStrings.Add(3354933275, "materialparams");
            if (!hashStrings.ContainsKey(973535787)) hashStrings.Add(973535787, "shaderconstants");

            foreach (var hashStr in Resources.Resource.MaterialNames.Split('\n'))
            {
                AddHash(StringToHash(hashStr), hashStr);
                AddHash(StringToHash(hashStr.ToLower()), hashStr.ToLower());
            }
        }

        static void LoadHash(string HashString)
        {
            uint hash = StringToHash(HashString);
            uint lowerhash = StringToHash(HashString.ToLower());

            if (!hashStrings.ContainsKey(hash))
                hashStrings.Add(hash, HashString);
            if (!hashStrings.ContainsKey(lowerhash))
                hashStrings.Add(lowerhash, HashString.ToLower());
        }

        //reverse hex bom
        static string Reverse(string text)
        {
            int number = Convert.ToInt32(text, 16);
            byte[] bytes = BitConverter.GetBytes(number);
            string retval = "";
            foreach (byte b in bytes)
                retval += b.ToString("X2");
            return retval;
        }
    }
}
