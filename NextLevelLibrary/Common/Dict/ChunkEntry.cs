using AvaloniaToolbox.Core;
using AvaloniaToolbox.Core.IO;
using NextLevelLibrary.LM2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace NextLevelLibrary
{
    public class ChunkEntry
    {
        public Stream Data;

        private List<ChunkEntry> _children = new List<ChunkEntry>();

        public List<ChunkEntry> Children
        {
            get
            {
                if (HasChildren && _children.Count == 0 && Size > 0)
                    _children.AddRange(((ChunkTable)Table).ReadChildren(this));

                return _children;
            }
            set
            {
                _children = value;
            }
        }

        public bool IsGlobalShared = false;
        public bool IsRegionOnly = false;
        public bool IsDebug = false;

        public ChunkType Type;
        public ushort Flags;
        public uint Size;
        public uint Offset;

        public IChunkSave FileFormat;

        public bool HasParent
        {
            get { return BitUtils.GetBit(Flags, 0); }
            set { Flags = (ushort)BitUtils.SetBit(Flags, value, 0); }
        }

        public int Alignment
        {
            get { return BitUtils.GetBits(Flags, 1, 10); }
            set { Flags = (ushort)BitUtils.SetBits(Flags, (int)value, 1, 10); }
        }

        public int BlockIndex
        {
            get { if (DictionaryFile.Version == GameVersion.LM3)
                    return BitUtils.GetBits(Flags, 11, 4);
                else
                    return BitUtils.GetBits(Flags, 12, 3);
            }
            set {
                if (DictionaryFile.Version == GameVersion.LM3)
                    Flags = (ushort)BitUtils.SetBits(Flags, (int)value, 11, 4);
                else
                    Flags = (ushort)BitUtils.SetBits(Flags, (int)value, 12, 3);
            }
        }

        /// <summary>
        /// If the chunk has children or not.
        /// </summary>
        public bool HasChildren
        {
            get { return BitUtils.GetBit(Flags, 15); }
            set { Flags = (ushort)BitUtils.SetBit(Flags, value, 15); }
        }

        /// <summary>
        /// Gets the child chunk given a type
        /// </summary>
        public ChunkEntry GetChild(ChunkType type) => this.Children.FirstOrDefault(x => x.Type == type);

        /// <summary>
        /// Gets the child chunks given a type
        /// </summary>
        public List<ChunkEntry> GetChildList(ChunkType type) => this.Children.Where(x => x.Type == type).ToList();

        /// <summary>
        /// 
        /// </summary>
        public IChunkTable Table;

        private IDictionaryData DictionaryFile;

        public ChunkEntry() { }

        public ChunkEntry(IDictionaryData dict, IChunkTable table) {
            this.DictionaryFile = dict;
            this.Table = table;
        }

        public virtual void OnSave() {
            if (FileFormat == null) return;

            FileFormat.Save(this);
        }

        public void AddChild(ChunkEntry chunk)
        {
            this.Children.Add(chunk);
            chunk.HasParent = true;
            this.HasChildren = true;
        }

        public ChunkEntry AddChild(ChunkType type, bool hasChildren = false)
        {
            this.HasChildren = true;

            var entry = new ChunkEntry(DictionaryFile, Table)
            {
                Type = type,
                Alignment = GetDefaultAlignment(),
                HasChildren = hasChildren,
                BlockIndex = 0,
                HasParent = true,
            };
            Children.Add(entry);
            entry.SetupDefaultFlags();
            return entry;
        }

        public ChunkEntry Clone()
        {
            List<ChunkEntry> children = new List<ChunkEntry>();
            foreach (var child in this.Children)
                children.Add(child.Clone());

            if (this is ChunkFileEntry)
                return new ChunkFileEntry(DictionaryFile, Table)
                {
                    Header = ((ChunkFileEntry)this).Header.Clone(),
                    Data = this.Data,
                    Offset = this.Offset,
                    Size = this.Size,
                    Flags = this.Flags,
                    Type = this.Type,
                    Children = children,
                    DataHash = this.DataHash,
                };
            else
                return new ChunkEntry(DictionaryFile, Table)
                {
                    Data = this.Data,
                    Offset = this.Offset,
                    Size = this.Size,
                    Flags = this.Flags,
                    Type = this.Type,
                    Children = children,
                    DataHash = this.DataHash,
                };
        }

        public void SetupDefaultFlags()
        {
            if (DictionaryFile.Version == GameVersion.LM2HD)
            {
                switch (this.Type)
                {
                    case ChunkType.Texture:
                        this.Flags = 33793;
                        break;
                    case ChunkType.TextureHeader:
                        this.Flags = 513;
                        break;
                    case ChunkType.TextureData:
                        this.Flags = 1025;
                        break;
                    case ChunkType.FileHeader:
                        this.Flags = 512;
                        break;
                }

            }
            else if (DictionaryFile.Version == GameVersion.LM2)
            {
                switch (this.Type)
                {
                    case ChunkType.Texture:
                        this.Flags = 34561;
                        break;
                    case ChunkType.TextureHeader:
                        this.Flags = 513;
                        break;
                    case ChunkType.TextureData:
                        this.Flags = 5889;
                        break;
                    case ChunkType.FileHeader:
                        this.Flags = 512;
                        break;
                }

            }
            else
            {
                switch (this.Type)
                {
                    case ChunkType.Texture:
                        this.Flags = 41537;
                        break;
                    case ChunkType.TextureHeader:
                        this.Flags = 8385;
                        break;
                    case ChunkType.TextureData:
                        this.Flags = 19009;
                        break;
                    case ChunkType.FileHeader:
                        this.Flags = 8320;
                        break;
                }
            }
        }

        public int GetDataAlignment()
        {
            if (this.DictionaryFile.Version == GameVersion.LM3)
            {
                //1 -> 10 bits, unsure but these seem related to alignment
                switch (this.Alignment)
                {
                    case 0x120: //used by texture data
                    case 0x81:
                    case 0x80:
                        return 16;
                    case 0x61:
                    case 0x60: //most files use this
                    case 0x6C:
                        return 8;
                    case 0x40: //often by file header type
                        return 4;
                    default:
                        return 4;
                }
            }
            else
            {
                switch (this.Alignment)
                {
                    case 512:
                        return 16;
                }

                if (BitUtils.GetBit(Flags, 8))
                    return 8;
                else
                    return 4;
            }

        }



        public int GetDefaultAlignment()
        {
            switch (this.Type)
            {
                case ChunkType.Texture:
                case ChunkType.TextureData:
                    return 0x120;
                case ChunkType.FileHeader:
                    return 0x40;
                default:
                    return 0x60;
            }
        }

        public uint DataHash;

        public void SetupHash(byte[] blockIndices)
        {
           // DataHash = CalculateHash(blockIndices);
        }

        public uint CalculateHash(byte[] blockIndices)
        {
            var index = blockIndices[this.BlockIndex];

            uint hash = this.HasChildren ? 0 : (uint)0x1000 * index + Offset;
            foreach (var child in this.Children)
                hash += child.CalculateHash(blockIndices);

            return hash;
        }

        #region Export

        public virtual void Export(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            if (this.HasChildren)
            {
                using (var writer = new FileWriter(path))
                {
                    foreach (var child in this.Children)
                        ExportChild(writer, child);
                }
            }
            else
            {
                File.WriteAllBytes(path, this.Data.ReadAllBytes());
            }
        }

        static void ExportChild(FileWriter writer, ChunkEntry entry)
        {
            if (entry.HasChildren)
            {
                foreach (var child in entry.Children)
                    ExportChild(writer, child);
            }
            else
            {
                if (entry.Data != null)
                    writer.Write(entry.Data.ReadAllBytes());
            }
        }

        #endregion

        #region IO READ WRITE


        /// <summary>
        /// Reads a given structure from the start of the data stream.
        /// </summary>
        public T ReadStruct<T>()
        {
            using (var reader = new FileReader(Data, true))
            {
                return reader.ReadStruct<T>();
            }
        }

        /// <summary>
        /// Reads multiple structures from the start of the data stream.
        /// </summary>
        public List<T> ReadStructs<T>(uint count)
        {
            using (var reader = new FileReader(Data, true))
            {
                return reader.ReadMultipleStructs<T>(count);
            }
        }

        public byte[] ReadBytes(uint count)
        {
            using (var reader = new FileReader(Data, true))
            {
                return reader.ReadBytes((int)count);
            }
        }

        public ushort[] ReadUShortList(uint count)
        {
            ushort[] values = new ushort[(int)count];
            using (var reader = new FileReader(Data, true))
            {
                for (int i = 0; i < values.Length; i++)
                    values[i] = reader.ReadUInt16();
            }
            return values;
        }

        public uint[] ReadUint32s(uint count)
        {
            uint[] values = new uint[(int)count];
            using (var reader = new FileReader(Data, true))
            {
                for (int i = 0; i < values.Length; i++)
                    values[i] = reader.ReadUInt32();
            }
            return values;
        }

        public Vector3[] ReadVector3List(uint count)
        {
            Vector3[] values = new Vector3[(int)count];
            using (var reader = new FileReader(Data, true))
            {
                for (int i = 0; i < values.Length; i++)
                    values[i] = new Vector3(
                        reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            }
            return values;
        }

        public Vector4[] ReadVector4List(uint count)
        {
            Vector4[] values = new Vector4[(int)count];
            using (var reader = new FileReader(Data, true))
            {
                for (int i = 0; i < values.Length; i++)
                    values[i] = new Vector4(
                        reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            }
            return values;
        }

        /// <summary>
        /// Writes a given structure from the start of the data stream.
        /// </summary>
        public void WriteStructs<T>(List<T> values)
        {
            var mem = new MemoryStream();
            using (var writer = new FileWriter(mem, true))
            {
                foreach (var item in values)
                    writer.WriteStruct<T>(item);
            }
            Data = mem;
        }

        /// <summary>
        /// Writes a given structure from the start of the data stream.
        /// </summary>
        public void WriteStruct<T>(T value)
        {
            var mem = new MemoryStream();
            using (var writer = new FileWriter(mem))
            {
                writer.WriteStruct<T>(value);
            }
            Data = new MemoryStream(mem.ToArray());
        }

        public void Write(uint[] values)
        {
            var mem = new MemoryStream();
            using (var writer = new FileWriter(mem))
            {
                writer.Write(values);
            }
            Data = new MemoryStream(mem.ToArray());
        }
            
        /// <summary>
        /// Reads a list of primitive types from the start of the data stream.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="count"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public List<T> ReadPrimitive<T>(uint count)
        {
            T[] instace = new T[count];
            using (var reader = new FileReader(Data, true))
            {
                for (int i = 0; i < count; i++)
                {
                    object value = null;
                    if (typeof(T) == typeof(uint)) value = reader.ReadUInt32();
                    else if (typeof(T) == typeof(int)) value = reader.ReadInt32();
                    else if (typeof(T) == typeof(short)) value = reader.ReadInt16();
                    else if (typeof(T) == typeof(ushort)) value = reader.ReadUInt16();
                    else if (typeof(T) == typeof(float)) value = reader.ReadSingle();
                    else if (typeof(T) == typeof(bool)) value = reader.ReadBoolean();
                    else if (typeof(T) == typeof(sbyte)) value = reader.ReadSByte();
                    else if (typeof(T) == typeof(byte)) value = reader.ReadByte();
                    else
                        throw new Exception("Unsupported primitive type! " + typeof(T));

                    instace[i] = (T)value;
                }
            }
            return instace.ToList();
        }

        /// <summary>
        /// Reads a list of primitive types from the start of the data stream.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="count"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public T ReadPrimitive<T>()
        {
            using (var reader = new FileReader(Data, true))
            {
                object value = null;
                if (typeof(T) == typeof(uint)) value = reader.ReadUInt32();
                else if (typeof(T) == typeof(int)) value = reader.ReadInt32();
                else if (typeof(T) == typeof(short)) value = reader.ReadInt16();
                else if (typeof(T) == typeof(ushort)) value = reader.ReadUInt16();
                else if (typeof(T) == typeof(float)) value = reader.ReadSingle();
                else if (typeof(T) == typeof(bool)) value = reader.ReadBoolean();
                else if (typeof(T) == typeof(sbyte)) value = reader.ReadSByte();
                else if (typeof(T) == typeof(byte)) value = reader.ReadByte();
                else
                    throw new Exception("Unsupported primitive type! " + typeof(T));

                return (T)value;
            }
        }

        #endregion
    }
}
