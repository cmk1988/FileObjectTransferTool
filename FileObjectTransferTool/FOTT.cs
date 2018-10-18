using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace FileObjectTransferTool
{
    public class FOTT
    {
        class ObjectMetadata
        {
            public string FullTypeName { get; set; }
            public int Size { get; set; }
            public int Adress { get; set; }
        }

        class Header
        {
            public byte Mode { get; set; }
            public List<ObjectMetadata> objects { get; set; }
        }

        public static T DeepLoadObjectFromFile<T>(string fileName)
        {
            using (var ms = File.OpenRead(fileName))
            {
                var formatter = new BinaryFormatter();
                ms.Position = 0;
                return (T)formatter.Deserialize(ms);
            }
        }

        public static IEnumerable<object> DeepLoadObjectsFromFile(string fileName)
        {
            using (var fs = File.OpenRead(fileName))
            {
                var list = new List<object>();
                var header = readHeader(fs);
                foreach (var obj in header.objects)
                {
                    var array = new byte[obj.Size];
                    fs.Read(array, obj.Adress, obj.Size);
                    using (var ms = new MemoryStream(array))
                    {
                        var formatter = new BinaryFormatter();
                        ms.Position = 0;
                        list.Add(formatter.Deserialize(ms));
                    }
                }
                return list;
            }
        }

        public static void DeepCopyToFile(string fileName, object obj)
        {
            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, obj);
                ms.Position = 0;
                File.WriteAllBytes(fileName, ms.ToArray());
            }
        }

        public static void DeepCopyToFile(string fileName, List<object> objs)
        {

            var headerOffset = 4 + 1 + 4;
            var headerArray = new byte[headerOffset + objs.Count * 8];
            var magic = new byte[] { 70, 79, 84, 84 };
            magic.CopyTo(headerArray, 0);
            magic[4] = 0;
            var countArray = BitConverter.GetBytes(objs.Count);
            countArray.CopyTo(headerArray, 5);
            var list = new List<byte[]>();
            var formatter = new BinaryFormatter();
            int posi = headerOffset + objs.Count * 8;
            int i = 0;
            foreach (var obj in objs)
            {
                using (var ms = new MemoryStream())
                {
                    formatter.Serialize(ms, obj);
                    ms.Position = 0;
                    var array = ms.ToArray();
                    var size = array.Length;
                    BitConverter.GetBytes(posi).CopyTo(headerArray, headerOffset + i * 8);
                    BitConverter.GetBytes(size).CopyTo(headerArray, headerOffset + i * 8 + 4);
                    list.Add(array);
                    i++;
                    posi += size;
                }
            }
            File.WriteAllBytes(fileName, headerArray);
            using (var stream = new FileStream(fileName, FileMode.Append))
            {
                foreach (var obj in list)
                {
                    stream.Write(obj, 0, obj.Length);
                }
            }
        }

        private static Header readHeader(Stream s)
        {
            s.Position = 0;
            var magic = new byte[4];
            s.Read(magic, 0, 4);
            if (!magic.SequenceEqual(new byte[] { 70, 79, 84, 84 }))
                throw new Exception("Format not supported.");
            var header = new Header();
            header.Mode = (byte)s.ReadByte();
            var objectCountArray = new byte[4];
            s.Read(objectCountArray, 5, 4);
            var objectCount = BitConverter.ToInt32(objectCountArray, 0);
            var headerOffset = 4 + 1 + 4;
            var rawHeader = new byte[headerOffset + 8 * objectCount];
            header.objects = new List<ObjectMetadata>();
            for (int i = 0; i < objectCount; i++)
            {
                var array = new byte[8];
                s.Read(array, headerOffset + i * 8, 8);
                var adress = BitConverter.ToInt32(array, 0);
                var size = BitConverter.ToInt32(array, 4);
                header.objects.Add(new ObjectMetadata { Size = size, Adress = adress });
            }
            return header;
        }
    }
}
