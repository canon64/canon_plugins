using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

namespace MainGameDevelopmentBridge
{
    // Uses the JSON reader shipped with KKS; no System.Web.Extensions dependency.
    internal sealed class BridgeJson
    {
        public T Deserialize<T>(string json) => (T)Deserialize(json, typeof(T));
        public object Deserialize(string json, Type type)
        {
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json), XmlDictionaryReaderQuotas.Max))
            {
                var document = new XmlDocument(); document.Load(reader);
                return ConvertValue(Read(document.DocumentElement), type);
            }
        }
        private static object Read(XmlElement element)
        {
            switch (element.GetAttribute("type"))
            {
                case "null": return null;
                case "boolean": return bool.Parse(element.InnerText);
                case "number":
                    if (int.TryParse(element.InnerText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)) return integer;
                    return double.Parse(element.InnerText, CultureInfo.InvariantCulture);
                case "array": return element.ChildNodes.OfType<XmlElement>().Select(Read).ToArray();
                case "object": return element.ChildNodes.OfType<XmlElement>().ToDictionary(e => e.LocalName == "item" && e.HasAttribute("item") ? e.GetAttribute("item") : XmlConvert.DecodeName(e.LocalName), Read);
                default: return element.InnerText;
            }
        }
        private static object ConvertValue(object value, Type type)
        {
            if (value == null || type.IsInstanceOfType(value)) return value;
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type.IsEnum) return Enum.Parse(type, Convert.ToString(value, CultureInfo.InvariantCulture));
            if (type.IsArray)
            {
                var items = ((IEnumerable)value).Cast<object>().ToArray(); var array = Array.CreateInstance(type.GetElementType(), items.Length);
                for (int index = default(int); index < items.Length; index++) array.SetValue(ConvertValue(items[index], type.GetElementType()), index);
                return array;
            }
            if (value is Dictionary<string, object> fields)
            {
                var result = Activator.CreateInstance(type);
                foreach (var pair in fields)
                {
                    var field = type.GetField(pair.Key); var property = type.GetProperty(pair.Key);
                    if (field != null) field.SetValue(result, ConvertValue(pair.Value, field.FieldType));
                    else if (property?.CanWrite == true) property.SetValue(result, ConvertValue(pair.Value, property.PropertyType), null);
                    else throw new MissingMemberException(type.FullName, pair.Key);
                }
                return result;
            }
            return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }
        public string Serialize(object value) { var text = new StringBuilder(); Write(text, value, default(int)); return text.ToString(); }
        private static void Write(StringBuilder text, object value, int depth)
        {
            if (depth > 32) throw new InvalidOperationException("JSON nesting too deep; return handles for runtime objects");
            if (value == null) { text.Append("null"); return; }
            if (value is string || value is char || value is Enum)
            {
                using (var stream = new MemoryStream())
                { new DataContractJsonSerializer(typeof(string)).WriteObject(stream, value.ToString()); text.Append(Encoding.UTF8.GetString(stream.ToArray())); }
                return;
            }
            if (value is bool flag) { text.Append(flag ? "true" : "false"); return; }
            if (value.GetType().IsPrimitive || value is decimal) { text.Append(Convert.ToString(value, CultureInfo.InvariantCulture)); return; }
            if (value is IDictionary dictionary)
            {
                text.Append('{'); bool comma = false;
                foreach (DictionaryEntry pair in dictionary)
                { if (comma) text.Append(','); comma = true; Write(text, Convert.ToString(pair.Key), depth + 1); text.Append(':'); Write(text, pair.Value, depth + 1); }
                text.Append('}'); return;
            }
            if (value is IEnumerable sequence)
            {
                text.Append('['); bool comma = false;
                foreach (var item in sequence) { if (comma) text.Append(','); comma = true; Write(text, item, depth + 1); }
                text.Append(']'); return;
            }
            var members = value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.GetIndexParameters().Length == default(int))
                .ToDictionary(p => p.Name, p => p.GetValue(value, null));
            foreach (var field in value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)) members[field.Name] = field.GetValue(value);
            Write(text, members, depth + 1);
        }
    }
}
