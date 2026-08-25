using Jint;
using Jint.Native;
using Acornima.Ast;
using System.Text.RegularExpressions;

namespace AxarDB.Query
{
    public class QueryOptimizer
    {
        public struct AnalysisResult { public string Prop; public object Val; public string Op; }

        /// <summary>
        /// Inspects a script-defined predicate (arrow or function expression) and, when it is a
        /// single binary comparison against the document parameter (e.g. <c>x =&gt; x.age == 30</c>),
        /// returns the field name, the literal value and the operator so the query can be executed
        /// natively in C# instead of invoking the JavaScript predicate per document.
        ///
        /// The previous implementation relied on <c>predicate.ToString()</c>, but Jint returns
        /// <c>"function () { [native code] }"</c> for script functions — so the optimizer never
        /// triggered and every query fell back to the slow per-document JS evaluation. We now read
        /// the real Acornima AST via <c>ScriptFunction.FunctionDeclaration</c>.
        /// </summary>
        public static (string? prop, object? val, string op)? AnalyzePredicate(JsValue predicate)
        {
            if (predicate == null || !predicate.IsObject())
                return null;

            if (predicate.AsObject() is not Jint.Native.Function.ScriptFunction sf)
                return null;

            var fn = sf.FunctionDeclaration;
            if (fn == null) return null;

            var expr = GetReturnExpression(fn, out var paramName);
            if (expr is not BinaryExpression be) return null;

            var prop = GetField(be.Left, paramName);
            if (prop == null) return null;

            var op = MapOperator(be.Operator);
            if (op == null) return null;

            if (be.Right is not Literal lit) return null;
            return (prop, lit.Value, op);
        }

        private static Node? GetReturnExpression(Acornima.Ast.IFunction fn, out string? paramName)
        {
            paramName = fn.Params.Count > 0 && fn.Params[0] is Identifier id ? id.Name : null;

            if (fn is ArrowFunctionExpression arr)
            {
                if (arr.Body is Expression e) return e;
                if (arr.Body is BlockStatement block) return FirstReturn(block);
                return null;
            }

            if (fn.Body is BlockStatement block2) return FirstReturn(block2);
            if (fn.Body is Expression e2) return e2;
            return null;
        }

        private static Node? FirstReturn(BlockStatement block)
        {
            foreach (var s in block.Body)
                if (s is ReturnStatement r && r.Argument != null) return r.Argument;
            return null;
        }

        private static string? GetField(Node left, string? paramName)
        {
            if (left is MemberExpression me && !me.Computed &&
                me.Object is Identifier oi && (paramName == null || oi.Name == paramName))
            {
                if (me.Property is Identifier pi) return pi.Name;
                if (me.Property is Literal pl && pl.Value is string ps) return ps;
            }
            return null;
        }

        private static string? MapOperator(Acornima.Operator op) => op switch
        {
            Acornima.Operator.Equality => "==",
            Acornima.Operator.StrictEquality => "==",
            Acornima.Operator.Inequality => "!=",
            Acornima.Operator.StrictInequality => "!=",
            Acornima.Operator.GreaterThan => ">",
            Acornima.Operator.LessThan => "<",
            Acornima.Operator.GreaterThanOrEqual => ">=",
            Acornima.Operator.LessThanOrEqual => "<=",
            _ => null
        };


        public static HashSet<string> ExtractAccessedProperties(JsValue predicate)
        {
            var props = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            props.Add("_id"); // Always include ID

            if (predicate == null || !predicate.IsObject())
                return props;

            string code = predicate.ToString();

            // Extract the actual arrow function parameter name: "x => ...", "(x) => ...", "(u, i) => ..."
            // We only care about the first (document) parameter.
            var paramMatch = Regex.Match(code, @"^\s*\(?(\w+)");
            string paramName = (paramMatch.Success && paramMatch.Groups[1].Value != "function")
                ? Regex.Escape(paramMatch.Groups[1].Value)
                : @"(?:x|item|doc|u|s|p|o|d|e|v|n|r|t)"; // fallback to common names

            // Match dot notation: param.prop
            var dotPattern = $@"\b{paramName}\.(\w+)\b";
            var matches = Regex.Matches(code, dotPattern);
            foreach (Match match in matches)
            {
                props.Add(match.Groups[1].Value);
            }

            // Match bracket notation: param['prop'], param["prop"]
            var bracketPattern = $@"\b{paramName}\[[""'](\w+)[""']\]";
            var bracketMatches = Regex.Matches(code, bracketPattern);
            foreach (Match match in bracketMatches)
            {
                props.Add(match.Groups[1].Value);
            }

            return props;
        }

        public static bool Evaluate(Dictionary<string, object> doc, AnalysisResult analysis)
        {
            if (string.IsNullOrEmpty(analysis.Prop)) return false;
            if (!doc.TryGetValue(analysis.Prop, out var val) || val == null) return false;

            val = Unwrap(val);

            try
            {
                if (analysis.Op == "==" || analysis.Op == "===")
                {
                    return EqualsNormalized(val, analysis.Val);
                }

                if (analysis.Op == "!=" || analysis.Op == "!==")
                {
                    return !EqualsNormalized(val, analysis.Val);
                }

                double d1 = Convert.ToDouble(val);
                double d2 = Convert.ToDouble(analysis.Val);

                switch (analysis.Op)
                {
                    case ">": return d1 > d2;
                    case "<": return d1 < d2;
                    case ">=": return d1 >= d2;
                    case "<=": return d1 <= d2;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static object? Unwrap(object? value)
        {
            if (value is System.Text.Json.JsonElement element)
            {
                switch (element.ValueKind)
                {
                    case System.Text.Json.JsonValueKind.True: return true;
                    case System.Text.Json.JsonValueKind.False: return false;
                    case System.Text.Json.JsonValueKind.Number:
                        if (element.TryGetInt32(out var i)) return i;
                        if (element.TryGetInt64(out var l)) return l;
                        if (element.TryGetDouble(out var d)) return d;
                        return element.ToString();
                    case System.Text.Json.JsonValueKind.String: return element.GetString();
                    case System.Text.Json.JsonValueKind.Null: return null;
                    default: return element.ToString();
                }
            }
            return value;
        }

        private static bool EqualsNormalized(object? val1, object? val2)
        {
            if (val1 == null && val2 == null) return true;
            if (val1 == null || val2 == null) return false;

            if (val1.Equals(val2)) return true;

            if (val1 is string s1 && val2 is string s2)
            {
                return string.Equals(s1, s2, StringComparison.Ordinal);
            }

            try
            {
                return Math.Abs(Convert.ToDouble(val1) - Convert.ToDouble(val2)) < 1e-9;
            }
            catch
            {
                return val1.ToString() == val2.ToString();
            }
        }
    }
}
