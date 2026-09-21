// Minimal RFC 8259 JSON reader/writer for Unity Mono / .NET Framework.
// No external dependencies. Only used to load the character's
// <char>.vhforges.json and to emit the BoneDumper report.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BodyForge
{
    public static class MiniJson
    {
        public enum NodeType { Object, Array, String, Number, Bool, Null }

        public sealed class Node
        {
            public NodeType Type;
            public IDictionary<string, Node> Object;
            public IList<Node> Array;
            public string Str;
            public double Number;
            public bool Bool;

            public static Node MakeObject() => new Node { Type = NodeType.Object, Object = new Dictionary<string, Node>() };
            public static Node MakeArray() => new Node { Type = NodeType.Array, Array = new List<Node>() };
            public static Node MakeString(string s) => new Node { Type = NodeType.String, Str = s };
            public static Node MakeNumber(double d) => new Node { Type = NodeType.Number, Number = d };
            public static Node MakeBool(bool b) => new Node { Type = NodeType.Bool, Bool = b };
            public static readonly Node Null = new Node { Type = NodeType.Null };

            public double AsNumber(double fallback = 0.0)
            {
                var o = Deref();
                return o.Type == NodeType.Number ? o.Number : fallback;
            }

            public string AsString(string fallback = null)
            {
                var o = Deref();
                return o.Type == NodeType.String ? o.Str : fallback;
            }

            public bool AsBool(bool fallback = false)
            {
                var o = Deref();
                return o.Type == NodeType.Bool ? o.Bool : fallback;
            }

            public Node Get(string key) => Deref().Object != null && Deref().Object.TryGetValue(key, out var v) ? v : null;

            private Node Deref()
            {
                Node n = this;
                while (n.Type == NodeType.Null) return Null;
                return n;
            }
        }

        private sealed class Parser
        {
            private readonly string s;
            private int i;

            public Parser(string text) { s = text; }

            public Node Parse()
            {
                SkipWs();
                var n = ParseValue();
                SkipWs();
                if (i < s.Length) throw Error("unexpected trailing characters");
                return n;
            }

            private void SkipWs()
            {
                while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
            }

            private Node ParseValue()
            {
                if (i >= s.Length) throw Error("unexpected end of input");
                char c = s[i];
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return Node.MakeString(ParseString());
                    case 't': Expect("true", 4); return Node.MakeBool(true);
                    case 'f': Expect("false", 5); return Node.MakeBool(false);
                    case 'n': Expect("null", 4); return Node.Null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
                        throw Error("unexpected token '" + c + "'");
                }
            }

            private Node ParseObject()
            {
                i++;
                SkipWs();
                var obj = Node.MakeObject();
                if (i < s.Length && s[i] == '}') { i++; return obj; }
                while (true)
                {
                    SkipWs();
                    if (i >= s.Length || s[i] != '"') throw Error("expected key string");
                    string key = ParseString();
                    SkipWs();
                    if (i >= s.Length || s[i] != ':') throw Error("expected ':'");
                    i++;
                    SkipWs();
                    obj.Object[key] = ParseValue();
                    SkipWs();
                    if (i >= s.Length) throw Error("unterminated object");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return obj; }
                    throw Error("expected ',' or '}'");
                }
            }

            private Node ParseArray()
            {
                i++;
                SkipWs();
                var arr = Node.MakeArray();
                if (i < s.Length && s[i] == ']') { i++; return arr; }
                while (true)
                {
                    SkipWs();
                    arr.Array.Add(ParseValue());
                    SkipWs();
                    if (i >= s.Length) throw Error("unterminated array");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return arr; }
                    throw Error("expected ',' or ']'");
                }
            }

            private Node ParseNumber()
            {
                int start = i;
                if (i < s.Length && s[i] == '-') i++;
                while (i < s.Length && char.IsDigit(s[i])) i++;
                if (i < s.Length && s[i] == '.') { i++; while (i < s.Length && char.IsDigit(s[i])) i++; }
                if (i < s.Length && (s[i] == 'e' || s[i] == 'E')) {
                    i++;
                    if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
                    while (i < s.Length && char.IsDigit(s[i])) i++;
                }
                string num = s.Substring(start, i - start);
                if (!double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                    throw Error("invalid number '" + num + "'");
                return Node.MakeNumber(d);
            }

            private string ParseString()
            {
                i++;
                var sb = new StringBuilder();
                while (i < s.Length)
                {
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c == '\\')
                    {
                        if (i >= s.Length) throw Error("bad escape");
                        char e = s[i++];
                        switch (e)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u':
                                if (i + 4 > s.Length) throw Error("bad unicode escape");
                                string hex = s.Substring(i, 4);
                                if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int cp))
                                    throw Error("bad unicode escape '" + hex + "'");
                                i += 4;
                                sb.Append((char)cp);
                                break;
                            default: throw Error("bad escape '\\" + e + "'");
                        }
                    }
                    else sb.Append(c);
                }
                throw Error("unterminated string");
            }

            private void Expect(string word, int len)
            {
                if (i + len > s.Length || s.Substring(i, len) != word) throw Error("expected '" + word + "'");
                i += len;
            }

            private Exception Error(string msg) => new FormatException("JSON: " + msg + " at offset " + i);
        }

        public static Node Parse(string text) => new Parser(text).Parse();

        public static string Serialize(Node root)
        {
            var sb = new StringBuilder();
            Write(sb, root);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, Node n)
        {
            switch (n.Type)
            {
                case NodeType.Object:
                    sb.Append('{');
                    bool first = true;
                    foreach (var kv in n.Object)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        WriteString(sb, kv.Key);
                        sb.Append(':');
                        Write(sb, kv.Value);
                    }
                    sb.Append('}');
                    break;
                case NodeType.Array:
                    sb.Append('[');
                    first = true;
                    foreach (var it in n.Array)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        Write(sb, it);
                    }
                    sb.Append(']');
                    break;
                case NodeType.String: WriteString(sb, n.Str); break;
                case NodeType.Number: sb.Append(n.Number.ToString("R", CultureInfo.InvariantCulture)); break;
                case NodeType.Bool: sb.Append(n.Bool ? "true" : "false"); break;
                case NodeType.Null: sb.Append("null"); break;
            }
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}