using ImageLibrary;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Toolbox.Core;

namespace NextLevelLibrary
{
    public class TextureFormat_LM2HD : IFormat
    {
        public static Dictionary<uint, TextureFormat_LM2HD> TextureCache = new Dictionary<uint, TextureFormat_LM2HD>();

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Header
        {
            public uint ImageSize;
            public uint Hash;
            public uint Padding;
            public uint SamplerFlags = 87632;
            public ushort Width;
            public ushort Height;
            public ushort Depth;
            public byte ArrayCount = 1;
            public byte MipFlags; //4 bits, each is the mip count
            public uint Padding2;
        }

        public DDS TextureData;
        public Header header = new Header();

        private ChunkFileEntry File;

        public TextureFormat_LM2HD()
        {

        }

        public TextureFormat_LM2HD(ChunkFileEntry file)
        {
            this.File = file;

            var imageChunk = file.GetChild(ChunkType.TextureData);
            this.header = file.GetChild(ChunkType.TextureHeader).ReadStruct<Header>();

            //image data is DDS which is already supported
            TextureData = new DDS(imageChunk.Data);

            if (!TextureCache.ContainsKey(file.FilePath.Value))
                TextureCache.Add(file.FilePath.Value, this);
        }

        public void Dispose()
        {
            if (File != null && TextureCache.ContainsKey(File.FilePath.Value))
                TextureCache.Remove(File.FilePath.Value);
        }

        public void Save(ChunkEntry fileEntry)
        {
            header.Width = (ushort)TextureData.MainHeader.Width;
            header.Height = (ushort)TextureData.MainHeader.Height;
            header.Depth = 1;
            header.ArrayCount = 1;
            header.MipFlags = SetMipCount((byte)TextureData.MainHeader.MipCount);

            var imageChunk = fileEntry.GetChild(ChunkType.TextureData);
            var headerChunk = fileEntry.GetChild(ChunkType.TextureHeader);

            //write extra dds data
            TextureData.MainHeader.Reserved9 = 1414813262; //NVTT magic
            if (TextureData.MainHeader.Reserved10 == 0)
                TextureData.MainHeader.Reserved10 = 131080; //unsure what this is, sampler info?

            var mem = new MemoryStream();
            TextureData.Save(mem);
            imageChunk.Data = new MemoryStream(mem.ToArray());

            header.ImageSize = (uint)imageChunk.Data.Length;
            headerChunk.WriteStruct(header);
        }

        private byte SetMipCount(byte count)
        {
            byte flag = 0x00; 
            flag |= count;
            flag |= (byte)(count << 4);
            return flag;
        }
    }
}
