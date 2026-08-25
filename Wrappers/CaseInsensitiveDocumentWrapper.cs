using System.Dynamic;
using System.Linq;

namespace AxarDB.Wrappers
{
    public class CaseInsensitiveDocumentWrapper : DocumentWrapper
    {
        public CaseInsensitiveDocumentWrapper(Dictionary<string, object> document) : base(document)
        {
        }

        public override bool TryGetMember(GetMemberBinder binder, out object? result)
        {
            string? matchedKey = null;
            foreach (var key in Data.Keys)
            {
                if (string.Equals(key, binder.Name, System.StringComparison.OrdinalIgnoreCase))
                {
                    matchedKey = key;
                    break;
                }
            }

            if (matchedKey != null && Data.TryGetValue(matchedKey, out var val))
            {
                var unwrapped = Unwrap(val);
                if (unwrapped is string strVal)
                {
                    result = TurkishNormalize(strVal);
                    return true;
                }
                result = unwrapped;
                return true;
            }
            result = null;
            return true;
        }

        public static string TurkishNormalize(string str)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;

            bool needsNormalization = false;
            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                if (c == 'I' || c == 'İ' || c == 'ı' || 
                    c == 'Ö' || c == 'ö' || 
                    c == 'Ü' || c == 'ü' || 
                    c == 'Ç' || c == 'ç' || 
                    c == 'Ş' || c == 'ş' || 
                    c == 'Ğ' || c == 'ğ' || 
                    char.IsUpper(c))
                {
                    needsNormalization = true;
                    break;
                }
            }

            if (!needsNormalization) return str;

            char[] chars = new char[str.Length];
            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                if (c == 'I' || c == 'İ' || c == 'ı' || c == 'i')
                {
                    chars[i] = 'i';
                }
                else if (c == 'Ö' || c == 'ö')
                {
                    chars[i] = 'ö';
                }
                else if (c == 'Ü' || c == 'ü')
                {
                    chars[i] = 'ü';
                }
                else if (c == 'Ç' || c == 'ç')
                {
                    chars[i] = 'ç';
                }
                else if (c == 'Ş' || c == 'ş')
                {
                    chars[i] = 'ş';
                }
                else if (c == 'Ğ' || c == 'ğ')
                {
                    chars[i] = 'ğ';
                }
                else
                {
                    chars[i] = char.ToLowerInvariant(c);
                }
            }
            return new string(chars);
        }
    }
}
