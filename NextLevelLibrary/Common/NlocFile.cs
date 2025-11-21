using AvaloniaToolbox.Core.IO;
using CommunityToolkit.HighPerformance.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Serialization;

namespace NextLevelLibrary
{
    public class NlocFile
    {
        public List<Message> Messages = new List<Message>();

        public bool IsBigEndian;

        public uint EncodingType;
        public uint LanguageID;
        public uint Unknown0x10;

        public bool IsVersion2;

        public class Message
        {
            public uint ID;
            public string Text;
        }

        public NlocFile(Stream stream)
        {
            using (var reader = new FileReader(stream)) {
                Read(reader);
            }
        }

        public void Save(Stream stream)
        {
            using (var writer = new FileWriter(stream)) {
                Write(writer);
            }
        }

        private void Read(FileReader reader)
        {
            reader.SetByteOrder(true);

            string signature = reader.ReadSignature4();

            if (signature != "NLOC") //LM3 has no magic header
            {
                LanguageID = reader.ReadUInt32();
                IsVersion2 = true;
            }

            EncodingType = reader.ReadUInt32();
            // Check if byte order needs to be reversed or not
            if (EncodingType == 16777216)
            {
                EncodingType = 1;
                reader.SetByteOrder(false);
            }

            LanguageID = reader.ReadUInt32();
            uint numEntries = reader.ReadUInt32();
            Unknown0x10 = reader.ReadUInt32();

            IsBigEndian = reader.ByteOrder == ByteOrder.BigEndian;

            var headerSize = !IsVersion2 ? 0x14 : 0xC;
            var dataStart = headerSize + numEntries * 8;

            reader.SeekBegin(0x14);
            for (int i = 0; i < numEntries; i++)
            {
                Message textEntry = new Message();
                Messages.Add(textEntry);

                textEntry.ID = reader.ReadUInt32();
                uint offset = reader.ReadUInt32();

                using (reader.TemporarySeek(dataStart + (offset * 2), SeekOrigin.Begin)) {
                    if (EncodingType == 2)
                        textEntry.Text = ReadUTF32String(reader);
                    else
                        textEntry.Text = ReadUTF16String(reader);
                    Console.WriteLine($"{textEntry.Text}");
                }
            }
        }

        private void Write(FileWriter writer)
        {
            if (IsVersion2)
            {
                writer.Write(LanguageID);
                writer.Write(Messages.Count);
                writer.Write(Unknown0x10);
            }
            else
            {
                writer.WriteSignature("NLOC");
                writer.SetByteOrder(IsBigEndian);
                writer.Write(EncodingType);
                writer.Write(LanguageID);
                writer.Write(Messages.Count);
                writer.Write(Unknown0x10);
            }

            //Write empty data first
            writer.Write(new byte[8 * Messages.Count]);

            //Write string table
            List<uint> positions = new List<uint>();

            uint start = (uint)writer.Position;
            for (int i = 0; i < Messages.Count; i++)
            {
                positions.Add(((uint)writer.Position - start) / 2);
                if (EncodingType == 2)
                    writer.Write(WriteUTF32String(Messages[i].Text));
                else
                    writer.Write(WriteUTF16String(Messages[i].Text));
            }

            // Order by id
            var ordered = Messages.OrderBy(x => x.ID).ToList();

            writer.SeekBegin(0x14);
            for (int i = 0; i < Messages.Count; i++)
            {
                var indx = Messages.IndexOf(ordered[i]);

                writer.Write(ordered[i].ID);
                writer.Write(positions[indx]);
            }
        }
        private string ReadUTF32String(FileReader reader)
        {
            List<uint> chars = new List<uint>();
            while (true)
            {
                uint value = reader.ReadUInt32();
                if (value == 0)
                    break;
                chars.Add(value);
            }
            // Convert the ushort list to a string
            return new string(chars.Select(c => (char)c).ToArray());
        }
        private string ReadUTF16String(FileReader reader)
        {
            List<ushort> chars = new List<ushort>();
            while (true)
            {
                ushort value = reader.ReadUInt16();
                if (value == 0)
                    break;
                chars.Add(value);
            }
            // Convert the ushort list to a string
            return new string(chars.Select(c => (char)c).ToArray());
        }
        private ushort[] WriteUTF16String(string text)
        {
            if (text == null)
                return new ushort[] { 0 };

            ushort[] buffer = new ushort[text.Length + 1]; // +1 for null terminator
            for (int i = 0; i < text.Length; i++)
            {
                buffer[i] = (ushort)text[i];
            }

            buffer[text.Length] = 0;
            return buffer;
        }
        private uint[] WriteUTF32String(string text)
        {
            if (text == null)
                return new uint[] { 0 };

            uint[] buffer = new uint[text.Length + 1]; // +1 for null terminator
            for (int i = 0; i < text.Length; i++)
            {
                buffer[i] = (uint)text[i];
            }

            buffer[text.Length] = 0;
            return buffer;
        }

        public string ToXml()
        {
            List<MessageXml> messages = new List<MessageXml>();
            for (int i = 0; i < this.Messages.Count; i++)
                messages.Add(new MessageXml()
                {
                    Label = Hashing.GetString(this.Messages[i].ID),
                    Text = this.Messages[i].Text.Replace("\n", "&#xA;"),   
                });

            using (var writer = new System.IO.StringWriter())
            {
                var serializer = new XmlSerializer(typeof(List<MessageXml>));
                serializer.Serialize(writer, messages);
                writer.Flush();
                return writer.ToString();
            }
        }

        public void FromXml(string xml)
        {
            using (var stringReader = new StringReader(xml))
            {
                var serializer = new XmlSerializer(typeof(List<MessageXml>));
                List<MessageXml> messages = (List<MessageXml>)serializer.Deserialize(stringReader);

                this.Messages.Clear();
                foreach (var msg in messages)
                {
                    this.Messages.Add(new Message()
                    {
                        ID = Hashing.GetHashFromString(msg.Label, true),
                        Text = msg.Text,
                    });
                }
            }
        }

        public class MessageXml
        {
            public string Label { get; set; }
            public string Text { get; set; }
        }
    }
}
