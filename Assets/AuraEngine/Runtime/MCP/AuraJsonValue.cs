using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AuraEngine.MCP
{
    public sealed class AuraJsonValue
    {
        private readonly bool _bool;
        private readonly double _number;
        private readonly string _string;
        private readonly List<AuraJsonValue> _array;
        private readonly Dictionary<string, AuraJsonValue> _object;

        private AuraJsonValue(AuraJsonKind kind, bool boolValue, double number, string text,
            List<AuraJsonValue> array, Dictionary<string, AuraJsonValue> obj)
        {
            Kind = kind;
            _bool = boolValue;
            _number = number;
            _string = text;
            _array = array;
            _object = obj;
        }

        public AuraJsonKind Kind { get; }

        public static readonly AuraJsonValue Null = new AuraJsonValue(AuraJsonKind.Null, false, 0d, null, null, null);

        public static AuraJsonValue Bool(bool value) =>
            new AuraJsonValue(AuraJsonKind.Bool, value, 0d, null, null, null);

        public static AuraJsonValue Number(double value) =>
            new AuraJsonValue(AuraJsonKind.Number, false, value, null, null, null);

        public static AuraJsonValue String(string value) =>
            new AuraJsonValue(AuraJsonKind.String, false, 0d, value ?? string.Empty, null, null);

        public static AuraJsonValue Array(List<AuraJsonValue> items) =>
            new AuraJsonValue(AuraJsonKind.Array, false, 0d, null, items ?? new List<AuraJsonValue>(), null);

        public static AuraJsonValue Object(Dictionary<string, AuraJsonValue> values) =>
            new AuraJsonValue(AuraJsonKind.Object, false, 0d, null, null, values ?? new Dictionary<string, AuraJsonValue>());

        public static AuraJsonValue Obj(params (string key, AuraJsonValue value)[] pairs)
        {
            var values = new Dictionary<string, AuraJsonValue>();
            foreach (var pair in pairs)
                values[pair.key] = pair.value;
            return Object(values);
        }

        public static AuraJsonValue Arr(params AuraJsonValue[] items) =>
            Array(new List<AuraJsonValue>(items ?? System.Array.Empty<AuraJsonValue>()));

        public double AsDouble() => Kind == AuraJsonKind.Number ? _number : 0d;

        public int AsInt() => (int)Math.Round(AsDouble());

        public float AsFloat() => (float)AsDouble();

        public bool AsBool() => Kind == AuraJsonKind.Bool && _bool;

        public string AsString() => Kind == AuraJsonKind.String ? _string : string.Empty;

        public int Count => Kind == AuraJsonKind.Array ? _array.Count : 0;

        public AuraJsonValue this[int index] =>
            Kind == AuraJsonKind.Array && index >= 0 && index < _array.Count ? _array[index] : Null;

        public AuraJsonValue this[string key] =>
            Kind == AuraJsonKind.Object && _object.TryGetValue(key, out var value) ? value : Null;

        public bool Has(string key) => Kind == AuraJsonKind.Object && _object.ContainsKey(key);

        public Dictionary<string, AuraJsonValue> ObjectValues() => _object;

        public static AuraJsonValue Parse(string text) => AuraJsonParser.Parse(text ?? string.Empty);

        public string ToJson()
        {
            var builder = new StringBuilder();
            Write(builder);
            return builder.ToString();
        }

        private void Write(StringBuilder builder)
        {
            switch (Kind)
            {
                case AuraJsonKind.Null:
                    builder.Append("null");
                    break;
                case AuraJsonKind.Bool:
                    builder.Append(_bool ? "true" : "false");
                    break;
                case AuraJsonKind.Number:
                    builder.Append(_number.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case AuraJsonKind.String:
                    WriteString(builder, _string);
                    break;
                case AuraJsonKind.Array:
                    builder.Append('[');
                    for (var index = 0; index < _array.Count; index++)
                    {
                        if (index > 0)
                            builder.Append(',');
                        _array[index].Write(builder);
                    }
                    builder.Append(']');
                    break;
                case AuraJsonKind.Object:
                    builder.Append('{');
                    var first = true;
                    foreach (var pair in _object)
                    {
                        if (!first)
                            builder.Append(',');
                        first = false;
                        WriteString(builder, pair.Key);
                        builder.Append(':');
                        pair.Value.Write(builder);
                    }
                    builder.Append('}');
                    break;
            }
        }

        private static void WriteString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (var character in value)
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < ' ')
                            builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            builder.Append(character);
                        break;
                }
            }
            builder.Append('"');
        }
    }
}
