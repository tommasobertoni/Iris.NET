using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace Iris.NET
{
    public static class NetworkExtensions
    {
        public static MemoryStream SerializeToMemoryStream(this object o)
        {
            MemoryStream stream = new MemoryStream();
            IFormatter formatter = new BinaryFormatter();
            formatter.Serialize(stream, o);
            return stream;
        }

        public static T DeserializeFromMemoryStream<T>(this MemoryStream stream) where T : class => DeserializeFromMemoryStream(stream) as T;

        public static object DeserializeFromMemoryStream(this MemoryStream stream)
        {
            IFormatter formatter = new BinaryFormatter();
            stream.Seek(0, SeekOrigin.Begin);
            object o = formatter.Deserialize(stream);
            return o;
        }

        public static MemoryStream ReadNext(this Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            MemoryStream ms = new MemoryStream();
            int read = input.Read(buffer, 0, buffer.Length);
            ms.Write(buffer, 0, read);
            return ms;
        }
    }
}
