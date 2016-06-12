using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace Iris.NET
{
    public static class IrisExtensions
    {
        public static MemoryStream SerializeToMemoryStream(this object o)
        {
            MemoryStream stream = new MemoryStream();
            IFormatter formatter = new BinaryFormatter();
            formatter.Serialize(stream, o);
            return stream;
        }

        public static T DeserializeFromMemoryStream<T>(this MemoryStream stream) => (T)DeserializeFromMemoryStream(stream);

        public static object DeserializeFromMemoryStream(this MemoryStream stream)
        {
            IFormatter formatter = new BinaryFormatter();
            stream.Seek(0, SeekOrigin.Begin);
            object o = formatter.Deserialize(stream);
            return o;
        }
    }
}
