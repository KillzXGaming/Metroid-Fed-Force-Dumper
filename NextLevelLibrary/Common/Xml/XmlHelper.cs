using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace FileConverter
{
    public class XmlHelper<T> where T : class
    {
        public static void SaveXml(T obj, string path)
        {
            using (var writer = new System.IO.StreamWriter(path))
            {
                var serializer = new XmlSerializer(obj.GetType());
                serializer.Serialize(writer, obj);
                writer.Flush();
            }
        }

        public static T LoadXml(string path)
        {
            using (var stream = System.IO.File.OpenRead(path))
            {
                var serializer = new XmlSerializer(typeof(T));
                return serializer.Deserialize(stream) as T;
            }
        }
    }
}
