using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AuraEngine.MCP
{
    public sealed class AuraJsonParser
    {
        private readonly string _text;
        private int _index;

        private AuraJsonParser(string text)
        {
            _text = text;
        }

        public static AuraJsonValue Parse(string text)
        {
            var parser = new AuraJsonParser(text ?? string.Empty);
            return parser.ParseValue();
        }

        private AuraJsonValue ParseValue()
        {
            SkipWhitespace();
            if (_index >= _text.Length)
                return AuraJsonValue.Null;

            var character = _text[_index];
            switch (character)
            {
                case '{': return ParseObject();
                case '[': return ParseArray();
                case '"': return AuraJsonValue.String(ParseString());
                case 't':
                    Expect("true");
                    return AuraJsonValue.Bool(true);
                case 'f':
                    Expect("false");
                    return AuraJsonValue.Bool(false);
                case 'n':
                    Expect("null");
                    return AuraJsonValue.Null;
                default: return AuraJsonValue.Number(ParseNumber());
            }
        }

        private AuraJsonValue ParseObject()
        {
            var values = new Dictionary<string, AuraJsonValue>();
            _index++;
            SkipWhitespace();
            if (Peek() == '}')
            {
                _index++;
                return AuraJsonValue.Object(values);
            }

            while (_index < _text.Length)
            {
                SkipWhitespace();
                var key = ParseString();
                SkipWhitespace();
                ExpectChar(':');
                values[key] = ParseValue();
                SkipWhitespace();
                var next = Peek();
                if (next == ',')
                {
                    _index++;
                    continue;
                }

                if (next == '}')
                    _index++;
                break;
            }

            return AuraJsonValue.Object(values);
        }

        private AuraJsonValue ParseArray()
        {
            var items = new List<AuraJsonValue>();
            _index++;
            SkipWhitespace();
            if (Peek() == ']')
            {
                _index++;
                return AuraJsonValue.Array(items);
            }

            while (_index < _text.Length)
            {
                items.Add(ParseValue());
                SkipWhitespace();
                var next = Peek();
                if (next == ',')
                {
                    _index++;
                    continue;
                }

                if (next == ']')
                    _index++;
                break;
            }

            return AuraJsonValue.Array(items);
        }

        private string ParseString()
        {
            SkipWhitespace();
            ExpectChar('"');
            var builder = new StringBuilder();
            while (_index < _text.Length)
            {
                var character = _text[_index++];
                if (character == '"')
                    break;
                if (character != '\\')
                {
                    builder.Append(character);
                    continue;
                }

                if (_index >= _text.Length)
                    break;

                var escape = _text[_index++];
                switch (escape)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'u':
                        if (_index + 4 <= _text.Length)
                        {
                            var hex = _text.Substring(_index, 4);
                            _index += 4;
                            builder.Append((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        }
                        break;
                    default: builder.Append(escape); break;
                }
            }

            return builder.ToString();
        }

        private double ParseNumber()
        {
            var start = _index;
            while (_index < _text.Length && "+-0123456789.eE".IndexOf(_text[_index]) >= 0)
                _index++;
            return double.Parse(_text.Substring(start, _index - start), CultureInfo.InvariantCulture);
        }

        private char Peek() => _index < _text.Length ? _text[_index] : '\0';

        private void SkipWhitespace()
        {
            while (_index < _text.Length && char.IsWhiteSpace(_text[_index]))
                _index++;
        }

        private void ExpectChar(char expected)
        {
            if (_index < _text.Length && _text[_index] == expected)
                _index++;
        }

        private void Expect(string literal)
        {
            if (_index + literal.Length <= _text.Length && _text.Substring(_index, literal.Length) == literal)
                _index += literal.Length;
        }
    }
}
