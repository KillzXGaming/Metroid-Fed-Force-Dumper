using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NextLevelLibrary
{
    public class NlgFont
    {
        public List<GlyphEntry> Glyphs = new List<GlyphEntry>();

        public List<NlgKerning> Kernings = new List<NlgKerning>();

        public bool isVersion111 = false; //1.11

        public string FontName = "Font";
        public string FontType = "48";
        public string FontColor = "255 255 255";

        public int FontHeight = 25;
        public int FontRenderHeight = 34;
        public int Ascent = 26;
        public int RenderAscent = 26;

        public int PageSize = 256;
        public int PageCount = 1;
        public string TextType = "color";
        public string Distribution = "english";

        public int CharSpacing = 0;
        public int LineHeight = 0;
        public int FallbackCharacter = 63;

        public int Inline = 10;

        public bool HasOffsetY = false;

        public NlgFont() { }

        public NlgFont(Stream stream)
        {
            int start_height = 0;
            int start_width = 0;
            int start_page_idx = 0;

            stream.Position = 0;

            using (var reader = new StreamReader(stream, true))
            {
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();

                    if (line.StartsWith("Font"))
                    {
                        var values = line.Split(' ');
                        FontName = values[1].Replace('"', ' ').Replace(" ", "");
                        FontType = values[2];
                    }
                    else if (line.StartsWith("Height"))
                    {
                        var values = line.Split(' ');
                        FontHeight = int.Parse(values[1]);
                        FontRenderHeight = int.Parse(values[3]);
                        Ascent = int.Parse(values[5]);
                        RenderAscent = int.Parse(values[7]);
                    }
                    else if(line.StartsWith("PageSize"))
                    {
                        var values = line.Split(' ');
                        PageSize = int.Parse(values[1]);
                        PageCount = int.Parse(values[3]);
                        TextType = values[5];
                        Distribution = values[7];
                    }
                    else if(line.StartsWith("Glyph"))
                    {
                        GlyphEntry entry = new GlyphEntry();
                        var values = line.Split(' ');

                        //0 = "Glyph"
                        //2 = "Width"
                        if (int.TryParse(values[1], out int char_int) && (char_int > 100 || char_int == 32))
                            entry.Glyph = (char)char_int;
                        else
                            entry.Glyph = char.Parse(values[1]);

                        entry.Width = int.Parse(values[3]);
                        entry.WidthPadded = int.Parse(values[4]);
                        entry.OffsetX = int.Parse(values[5]);
                        if (values.Length == 7) //LM3
                        {
                            entry.OffsetY = int.Parse(values[6]);
                            HasOffsetY = true;
                        }

                        if (values.Length == 11) //LM2HD
                        {
                            entry.StartPosX = int.Parse(values[6]);
                            entry.StartPosY = int.Parse(values[7]);
                            entry.EndPosX = int.Parse(values[8]);
                            entry.EndPosY = int.Parse(values[9]);
                            entry.PageIdx = int.Parse(values[10]);
                        }
                        else
                        {
                            //start a new row if the contents do not fit on X
                            if (start_width + entry.WidthPadded > PageSize)
                            {
                                start_width = 0;
                                start_height += FontRenderHeight;
                            }
                            //start a new page if the contents do not fit on Y
                            if (start_height + FontRenderHeight >= PageSize)
                            {
                                start_height = 0;
                                start_width = 0;
                                start_page_idx++;
                            }

                            Console.WriteLine($"{entry.Glyph} {start_width} {start_height}");

                            entry.StartPosX = start_width;
                            entry.StartPosY = start_height;
                            entry.EndPosX = start_width + entry.WidthPadded;
                            entry.EndPosY = start_height + FontRenderHeight;
                            entry.PageIdx = start_page_idx;

                            start_width += entry.WidthPadded;
                        }
                        this.Glyphs.Add(entry);
                    }
                    else if (line.StartsWith("Kern"))
                    {
                        char GetGlyph(string v)
                        {
                            if (int.TryParse(v, out int char_int) && (char_int > 100 || char_int == 32))
                               return (char)char_int;
                            else
                                return char.Parse(v);
                        }

                        var values = line.Split(' ');
                        //Kern, fist char
                        var firstChar = GetGlyph(values[1]);

                        NlgKerning kern = new NlgKerning();
                        kern.Glyph = firstChar;
                        this.Kernings.Add(kern);

                        for (int i = 2; i < values.Length;)
                        {
                            var secondChar = GetGlyph(values[i++]);
                            var adjustment = int.Parse(values[i++]);

                            kern.Pairs.Add(new NlgKerningPair()
                            {
                                Glyph = secondChar,
                                Value = adjustment,
                            });
                        }
                    }
                }
            }
        }

        public byte[] Save()
        {
            var mem = new MemoryStream();
            using (var writer = new StreamWriter(mem))
            {
                writer.WriteLine("NLG Font Description File");
                writer.WriteLine($"Version {(isVersion111 ? "1.11" : "1.1")}"); //1.11 for LM2HD, 1 for gcn/wii, 1.1 for the rest
                writer.WriteLine($"Font \"{FontName.Replace(" ", "")}\" {FontType} color {FontColor}");
                writer.WriteLine($"PageSize {PageSize} PageCount {PageCount} TextType {TextType} Distribution {Distribution}");
                writer.WriteLine($"Height {FontHeight} RenderHeight {FontRenderHeight} Ascent {Ascent} RenderAscent {RenderAscent} IL {Inline}");
                writer.WriteLine($"CharSpacing {CharSpacing} LineHeight {LineHeight} FallbackCharacter {FallbackCharacter}");
                foreach (var glyph in this.Glyphs)
                {
                    if (isVersion111)
                        writer.WriteLine($"Glyph {GlyphToString(glyph.Glyph)} Width {glyph.Width} {glyph.WidthPadded} {glyph.OffsetX} {glyph.StartPosX} {glyph.StartPosY} {glyph.EndPosX} {glyph.EndPosY} {glyph.PageIdx}");
                    else if (HasOffsetY)
                        writer.WriteLine($"Glyph {GlyphToString(glyph.Glyph)} Width {glyph.Width} {glyph.WidthPadded} {glyph.OffsetX} {glyph.OffsetY}");
                    else
                        writer.WriteLine($"Glyph {GlyphToString(glyph.Glyph)} Width {glyph.Width} {glyph.WidthPadded} {glyph.OffsetX}");
                }

                foreach (var kern in this.Kernings)
                {
                    string line = $"Kern {kern.Glyph}";
                    foreach (var pair in kern.Pairs)
                        line += $" {pair.Glyph} {pair.Value}";

                    writer.WriteLine(line);
                }

                writer.WriteLine("END");
            }
            return mem.ToArray();
        }

        private string GlyphToString(int value)
        {
            if (value > 32 && value < 161)
                return ((char)value).ToString();
            else
                return value.ToString();
        }
    }


    public class GlyphEntry
    {
        public char Glyph;

        public int Width; //original 3ds width for LM2HD
        public int WidthPadded; //original 3ds width for LM2HD
        public int OffsetX; //original 3ds offset for LM2HD
        public int OffsetY;

        public int StartPosX;
        public int StartPosY;
        public int EndPosX;
        public int EndPosY;
        public int PageIdx;
    }

    public class NlgKerning
    {
        public char Glyph;

        public List<NlgKerningPair> Pairs = new List<NlgKerningPair>();
    }

    public class NlgKerningPair
    {
        public char Glyph;
        public int Value;
    }
}
