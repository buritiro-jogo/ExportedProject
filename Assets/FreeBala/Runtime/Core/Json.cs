// JSON mínimo (sem dependências): objetos viram Dictionary<string, object>, listas viram List<object>, números double, true/false bool.
// Usado para ler os dados exportados do jogo web (fb_data.json, anims.json, controllers.json, level.json, avatar/sheet dos personagens).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FreeBala
{
    public static class Json
    {
        public static object Parse(string s) { int i = 0; var v = Value(s, ref i); return v; }

        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        static object Value(string s, ref int i)
        {
            Ws(s, ref i);
            if (i >= s.Length) return null;
            char c = s[i];
            if (c == '{') return Obj(s, ref i);
            if (c == '[') return Arr(s, ref i);
            if (c == '"') return Str(s, ref i);
            if (c == 't' && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (c == 'f' && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (c == 'n' && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            return Num(s, ref i);
        }

        static Dictionary<string, object> Obj(string s, ref int i)
        {
            var d = new Dictionary<string, object>(); i++;
            while (true)
            {
                Ws(s, ref i);
                if (s[i] == '}') { i++; return d; }
                string k = Str(s, ref i); Ws(s, ref i); i++; // ':'
                d[k] = Value(s, ref i); Ws(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return d; }
                throw new FormatException("JSON: esperado , ou } em " + i);
            }
        }

        static List<object> Arr(string s, ref int i)
        {
            var l = new List<object>(); i++;
            while (true)
            {
                Ws(s, ref i);
                if (s[i] == ']') { i++; return l; }
                l.Add(Value(s, ref i)); Ws(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return l; }
                throw new FormatException("JSON: esperado , ou ] em " + i);
            }
        }

        static string Str(string s, ref int i)
        {
            var sb = new StringBuilder(); i++;
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                    default: sb.Append(e); break;
                }
            }
            return sb.ToString();
        }

        static object Num(string s, ref int i)
        {
            int a = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(a, i - a), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        // ---- leitura conveniente
        public static Dictionary<string, object> O(object o, string k) => (o as Dictionary<string, object>) != null && ((Dictionary<string, object>)o).TryGetValue(k, out var v) ? v as Dictionary<string, object> : null;
        public static List<object> L(object o, string k) => (o as Dictionary<string, object>) != null && ((Dictionary<string, object>)o).TryGetValue(k, out var v) ? v as List<object> : null;
        public static object Get(object o, string k) => (o as Dictionary<string, object>) != null && ((Dictionary<string, object>)o).TryGetValue(k, out var v) ? v : null;
        public static float F(object o, string k, float def = 0) { var v = Get(o, k); return v is double d ? (float)d : v is bool b ? (b ? 1 : 0) : def; }
        public static int I(object o, string k, int def = 0) { var v = Get(o, k); return v is double d ? (int)d : def; }
        public static bool B(object o, string k, bool def = false) { var v = Get(o, k); return v is bool b ? b : v is double d ? d != 0 : def; }
        public static string S(object o, string k, string def = null) { var v = Get(o, k); return v as string ?? def; }
        public static float Fv(object v, float def = 0) => v is double d ? (float)d : def;
        public static float[] Floats(List<object> l) { if (l == null) return new float[0]; var r = new float[l.Count]; for (int j = 0; j < r.Length; j++) r[j] = Fv(l[j]); return r; }
    }
}
