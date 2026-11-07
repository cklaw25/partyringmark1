// Tiny JSON writer for the state we send to the strategist (JsonUtility can't do dictionaries).
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public static class MiniJson
{
    public static string Write(object o) { var sb = new StringBuilder(); W(sb, o); return sb.ToString(); }

    static void W(StringBuilder sb, object o)
    {
        switch (o)
        {
            case null: sb.Append("null"); break;
            case string s: Str(sb, s); break;
            case bool b: sb.Append(b ? "true" : "false"); break;
            case float f: sb.Append(f.ToString("0.###", CultureInfo.InvariantCulture)); break;
            case double d: sb.Append(d.ToString("0.###", CultureInfo.InvariantCulture)); break;
            case int or long: sb.Append(System.Convert.ToString(o, CultureInfo.InvariantCulture)); break;
            case IDictionary dict:
                sb.Append('{'); bool first = true;
                foreach (DictionaryEntry kv in dict) { if (!first) sb.Append(','); first = false; Str(sb, kv.Key.ToString()); sb.Append(':'); W(sb, kv.Value); }
                sb.Append('}'); break;
            case IEnumerable list:
                sb.Append('['); bool f1 = true;
                foreach (var it in list) { if (!f1) sb.Append(','); f1 = false; W(sb, it); }
                sb.Append(']'); break;
            default: Str(sb, o.ToString()); break;
        }
    }

    static void Str(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': break;
                case '\t': sb.Append("\\t"); break;
                default: if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4")); else sb.Append(c); break;
            }
        }
        sb.Append('"');
    }
}
