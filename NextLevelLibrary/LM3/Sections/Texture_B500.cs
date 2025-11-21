using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using Toolbox.Core;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ImageLibrary;
using AvaloniaToolbox.Core.IO;

namespace NextLevelLibrary.LM3
{
    public class TextureFormatLM3 : IFormat
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Header
        {
            public uint Hash;
            public ushort Width = 128;
            public ushort Height = 128;
            public byte Unknown = 1;
            public byte Padding;
            public byte ArrayCount = 1;
            public byte MipFlags = 0xA0;
            public byte Format;
            public byte Unknown3 = 0x51;
            public ushort Unknown4 = 0x2C1A;

            public byte GetMipCount() => (byte)(this.MipFlags >> 4);
            public void SetMipCount(byte count)
            {
                byte flag = 0x00;
                flag |= count;
                flag |= (byte)(count << 4);
                MipFlags = flag;
            }
        }

        public Header ImageHeader;
        public Stream ImageData;

        public TextureFormatLM3() {
        }

        public TextureFormatLM3(ChunkFileEntry file)
        {
            var imageChunk = file.GetChild(ChunkType.TextureData);
            var headerChunk = file.GetChild(ChunkType.TextureHeader);
            ImageHeader = headerChunk.ReadStruct<Header>();
            ImageData = imageChunk.Data;
        }

        public void Save(ChunkFileEntry file)
        {
            var imageChunk = file.GetChild(ChunkType.TextureData);
            var headerChunk = file.GetChild(ChunkType.TextureHeader);

            headerChunk.WriteStruct(ImageHeader);
            imageChunk.Data = ImageData;
        }

        public GenericTextureBase ToGeneric()
        {
            return new GenericTextureBase()
            {
                Name = Hashing.GetString(this.ImageHeader.Hash),
                Width = this.ImageHeader.Width,
                Height = this.ImageHeader.Height,
                MipCount = this.ImageHeader.GetMipCount(),
                Data = this.ImageData.ToArray(),
                ImageFormat = new ImageFormat(Formats[this.ImageHeader.Format]),
                PlatformSwizzle = new ImageLibrary.PlatformSwizzle.PlatformSwizzleSwitch(),
            };
        }

        static Dictionary<uint, TextureFormat> Formats = new Dictionary<uint, TextureFormat>()
        {
            { 0x0, TextureFormat.RGBA8_UNORM },
            { 0x1, TextureFormat.RGBA8_SRGB },
            { 0x5, TextureFormat.RGB9E5_SHAREDEXP },
            { 0xD, TextureFormat.R8_UNORM },
            { 0xE, TextureFormat.R8_UNORM },
            { 0x11, TextureFormat.BC1_UNORM },
            { 0x12, TextureFormat.BC1_SRGB },
            { 0x13, TextureFormat.BC2_UNORM },
            { 0x14, TextureFormat.BC3_UNORM },
            { 0x15, TextureFormat.BC4_UNORM },
            { 0x16, TextureFormat.BC5_SNORM },
            { 0x17, TextureFormat.BC6H_UF16 },
            { 0x18, TextureFormat.BC7_UNORM },
            { 0x19, TextureFormat.ASTC_4x4_UNORM },
            { 0x1A, TextureFormat.ASTC_5x4_UNORM },
            { 0x1B, TextureFormat.ASTC_5x5_UNORM },
            { 0x1C, TextureFormat.ASTC_6x5_UNORM },
            { 0x1D, TextureFormat.ASTC_6x6_UNORM },
            { 0x1E, TextureFormat.ASTC_8x5_UNORM },
            { 0x1F, TextureFormat.ASTC_8x6_UNORM },
            { 0x20, TextureFormat.ASTC_8x8_UNORM},
        };
    }
}
