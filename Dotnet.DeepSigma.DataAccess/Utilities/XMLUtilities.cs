using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.IO;
using System.Xml;

namespace DeepSigma.DataAccess.Utilities
{
    public class XMLUtilities
    {

        public static T? GetObject<T>(string XMLFilePath)
        {
            using (FileStream fileStream = new(XMLFilePath, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read))
            {
                XmlSerializer serializer = new(typeof(T));
                return (T?)serializer.Deserialize(fileStream);
            }
        }

        public static string Serialize<T>(T obj)
        {
            XmlSerializer serializer = new(typeof(T));
            using (var string_writer = new StringWriter())
            {
                using (XmlTextWriter writer = new(string_writer) { Formatting = Formatting.Indented })
                {
                    serializer.Serialize(writer, obj);
                    return string_writer.ToString();
                }
            }
        }

    }
}
