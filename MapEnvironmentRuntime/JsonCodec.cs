using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System;
using System.Runtime.Serialization;
using System.Collections.ObjectModel;
using System.Reflection;
using System.CodeDom;
using UnityEngine;

namespace MapEnvironmentRuntime
{
    // KKS's Unity serializer can discard custom field arrays. Keep the wire format managed.
    public static class JsonCodec
    {
        private static DataContractJsonSerializer Serializer(System.Type type) =>
            new DataContractJsonSerializer(type, new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = int.MaxValue, UseSimpleDictionaryFormat = true, DataContractSurrogate = new UnityFields() });
        public sealed class VectorFields { public float x, y, z; }
        public sealed class VectorTwoFields { public float x, y; }
        public sealed class ColorFields { public float r, g, b, a; }
        private sealed class UnityFields : IDataContractSurrogate
        {
            public Type GetDataContractType(Type type) => type == typeof(Vector3) ? typeof(VectorFields) : type == typeof(Vector2) ? typeof(VectorTwoFields) : type == typeof(Color) ? typeof(ColorFields) : type;
            public object GetObjectToSerialize(object value, Type targetType)
            {
                if (value is Vector3 vector) return new VectorFields { x = vector.x, y = vector.y, z = vector.z };
                if (value is Vector2 pair) return new VectorTwoFields { x = pair.x, y = pair.y };
                if (value is Color color) return new ColorFields { r = color.r, g = color.g, b = color.b, a = color.a };
                return value;
            }
            public object GetDeserializedObject(object value, Type targetType)
            {
                if (value is VectorFields vector) return new Vector3(vector.x, vector.y, vector.z);
                if (value is VectorTwoFields pair) return new Vector2(pair.x, pair.y);
                if (value is ColorFields color) return new Color(color.r, color.g, color.b, color.a);
                return value;
            }
            public object GetCustomDataToExport(MemberInfo memberInfo, Type dataContractType) => null;
            public object GetCustomDataToExport(Type clrType, Type dataContractType) => null;
            public void GetKnownCustomDataTypes(Collection<Type> customDataTypes) { }
            public Type GetReferencedTypeOnImport(string typeName, string typeNamespace, object customData) => null;
            public CodeTypeDeclaration ProcessImportedType(CodeTypeDeclaration typeDeclaration, CodeCompileUnit compileUnit) => typeDeclaration;
        }
        public static T FromJson<T>(string json)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                return (T)Serializer(typeof(T)).ReadObject(stream);
        }
        public static string ToJson(object value, bool pretty = false)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8, false, pretty))
                { Serializer(value.GetType()).WriteObject(writer, value); writer.Flush(); }
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}
