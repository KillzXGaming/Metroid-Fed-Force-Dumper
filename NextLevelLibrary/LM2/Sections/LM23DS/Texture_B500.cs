using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using Toolbox.Core;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using AvaloniaToolbox.Core.IO;
using ImageLibrary.Helpers;
using ImageLibrary;

namespace NextLevelLibrary.LM2
{
    public class TextureFormatCTR : IFormat
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Header
        {
            public uint ImageSize;
            public uint Hash;
            public uint Padding;
            public uint Unknown2 = 559240; //Always 0x88888
            public ushort Width;
            public ushort Height;
            public ushort Padding2;
            public byte ArrayCount = 1;
            public byte MipFlags;
            public uint Unknown6;
            public uint Unknown7;
            public uint Unknown8;
            public uint Unknown9;
            public uint Unknown10;
            public byte Format;
            public byte Unknown11;
            public byte Unknown12;
            public byte Unknown13;

            public byte GetMipCount() => (byte) (this.MipFlags >> 4);
            public void SetMipCount(byte count)
            {
                byte flag = 0x00;
                flag |= count;
                flag |= (byte)(count << 4);
                MipFlags = flag;
            }
        }

        public Header ImageHeader { get; set; }
        public byte[] ImageData { get; set; }

        public TextureFormatCTR() {
            ImageHeader = new Header();
        }

        public TextureFormatCTR(ChunkFileEntry file)
        {
            var imageChunk = file.GetChild(ChunkType.TextureData);
            var headerChunk = file.GetChild(ChunkType.TextureHeader);

            imageChunk.Data.Position = 0;
            headerChunk.Data.Position = 0;

            ImageHeader = headerChunk.ReadStruct<Header>();
            using (var reader = new FileReader(imageChunk.Data, true)) {
                ImageData = reader.ReadBytes((int)ImageHeader.ImageSize);
            }
        }

        public void Save(ChunkFileEntry fileEntry)
        {
            var imageChunk = fileEntry.GetChild(ChunkType.TextureData);
            var headerChunk = fileEntry.GetChild(ChunkType.TextureHeader);

            uint hash = this.ImageHeader.Hash;

            imageChunk.Data = new MemoryStream(ImageData);
            headerChunk.WriteStruct(ImageHeader);
        }

        public GenericTextureBase ToGeneric()
        {
            return new GenericTextureBase()
            {
                Name = Hashing.GetString(this.ImageHeader.Hash),
                Width = this.ImageHeader.Width,
                Height = this.ImageHeader.Height,
                MipCount = this.ImageHeader.GetMipCount(),
                ImageFormat = new ImageFormat3DS((PICATextureFormat)this.ImageHeader.Format),
                Data = this.ImageData,
                PlatformSwizzle = new ImageLibrary.PlatformSwizzle.PlatformSwizzle3DS(),
            };
        }
    }
}
