using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.IO;
using System.Dynamic;
using Newtonsoft.Json;

namespace TSVCEO.CloudPrint.Util
{
    public static class JsonHelper
    {
        private static dynamic ReadJsonArray(JsonReader reader)
        {
            List<dynamic> vals = new List<dynamic>();

            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndArray)
                {
                    break;
                }
                else
                {
                    vals.Add(ReadJsonValue(reader, true));
                }
            }

            if (vals.All(v => v is IDictionary<string, object>))
            {
                if (vals.OfType<IDictionary<string, object>>().All(v => v.ContainsKey("Key") && v.ContainsKey("Value")))
                {
                    ExpandoObject obj = new ExpandoObject();

                    foreach (IDictionary<string, object> dict in vals.OfType<IDictionary<string, object>>())
                    {
                        ((IDictionary<string, object>)obj).Add(dict["Key"].ToString(), dict["Value"]);
                    }

                    return obj;
                }
            }

            return vals;
        }

        private static ExpandoObject ReadJsonObject(JsonReader reader)
        {
            ExpandoObject obj = new ExpandoObject();
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.Comment: break;
                    case JsonToken.EndObject: return obj;
                    case JsonToken.PropertyName: ((IDictionary<string, object>)obj).Add(reader.Value as string, ReadJsonValue(reader)); break;
                    default: throw new JsonException("Malformed JSON");
                }
            }

            return obj;
        }

        private static dynamic ReadJsonValue(JsonReader reader, bool alreadyread = false)
        {
            while (alreadyread || reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.StartArray: return ReadJsonArray(reader);
                    case JsonToken.StartObject: return ReadJsonObject(reader);
                    case JsonToken.String: return reader.Value;
                    case JsonToken.Boolean: return reader.Value;
                    case JsonToken.Bytes: return reader.Value;
                    case JsonToken.Date: return reader.Value;
                    case JsonToken.Float: return reader.Value;
                    case JsonToken.Integer: return reader.Value;
                    case JsonToken.Null: return null;
                    case JsonToken.Comment: break;
                    default: throw new JsonException("Malformed JSON");
                }

                alreadyread = false;
            }

            throw new JsonException("Empty JSON");
        }

        public static dynamic ReadJson(TextReader reader)
        {
            return ReadJsonValue(new JsonTextReader(reader));
        }

        private static void WriteJsonArray(JsonWriter writer, IEnumerable list)
        {
            writer.WriteStartArray();
            foreach (object val in list)
            {
                WriteJsonValue(writer, val);
            }
            writer.WriteEndArray();
        }

        private static void WriteJsonDictionary(JsonWriter writer, IDictionary dict)
        {
            writer.WriteStartObject();
            foreach (DictionaryEntry de in dict)
            {
                writer.WritePropertyName(de.Key.ToString());
                WriteJsonValue(writer, de.Value);
            }
            writer.WriteEndObject();
        }

        private static void WriteJsonGenericDictionary(JsonWriter writer, IDictionary<string, object> dict)
        {
            writer.WriteStartObject();
            foreach (KeyValuePair<string, object> de in dict)
            {
                writer.WritePropertyName(de.Key.ToString());
                WriteJsonValue(writer, de.Value);
            }
            writer.WriteEndObject();
        }
        
        private static void WriteJsonObject(JsonWriter writer, object obj)
        {
            WriteJsonObject(writer, TypeDescriptor.GetProperties(obj).OfType<PropertyDescriptor>().ToDictionary(prop => prop.Name, prop => prop.GetValue(obj)));
        }

        private static void WriteJsonValue(JsonWriter writer, object value)
        {
            if (value == null || value is string || value is byte[])
            {
                writer.WriteValue(value);
            }
            else if (value is IDictionary)
            {
                WriteJsonDictionary(writer, (IDictionary)value);
            }
            else if (value is IDictionary<string, object>)
            {
                WriteJsonGenericDictionary(writer, (IDictionary<string, object>)value);
            }
            else if (value is IEnumerable)
            {
                WriteJsonArray(writer, (IEnumerable)value);
            }
            else if (value.GetType().IsPrimitive)
            {
                writer.WriteValue(value);
            }
            else
            {
                WriteJsonObject(writer, value);
            }

        }

        private static void WriteJson(JsonTextWriter writer, object value)
        {
            WriteJsonValue(writer, value);
            writer.Flush();
        }

        public static void WriteJson(TextWriter writer, object value)
        {
            WriteJson(new JsonTextWriter(writer), value);
            writer.Flush();
        }
    }
}
