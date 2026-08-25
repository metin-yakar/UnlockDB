using System.Security.Cryptography;
using System.Text;
using AxarDB.Definitions;

namespace AxarDB.Helpers
{
    public static class ScriptUtils
    {
        public static string MD5(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            using var md5 = System.Security.Cryptography.MD5.Create();
            var inputBytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = md5.ComputeHash(inputBytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        public static string SHA256(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var inputBytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = sha256.ComputeHash(inputBytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        public static string ToString(object obj)
        {
            return obj?.ToString() ?? "";
        }

        public static int RandomNumber(int min, int max)
        {
            return Random.Shared.Next(min, max);
        }

        public static decimal RandomDecimal(string minStr, string maxStr)
        {
            if (!decimal.TryParse(minStr, out var min) || !decimal.TryParse(maxStr, out var max))
                return 0;
            
            // Decimal random generation using double approximation for simplicity within range
            // For higher precision needs, a custom implementation would be required but this fits general use
            var randomDouble = Random.Shared.NextDouble();
            return min + (decimal)randomDouble * (max - min);
        }

        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[Random.Shared.Next(s.Length)]).ToArray());
        }

        public static string ToBase64(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            var bytes = Encoding.UTF8.GetBytes(input);
            return Convert.ToBase64String(bytes);
        }

        public static string FromBase64(string base64)
        {
            if (string.IsNullOrEmpty(base64)) return "";
            try {
                var bytes = Convert.FromBase64String(base64);
                return Encoding.UTF8.GetString(bytes);
            } catch { return ""; }
        }

        public static string Encrypt(string text, string salt)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(salt)) return "";
            // Simple XOR + Base64 for demonstration "encryption" as per common lightweight requirements
            // For strong security, AES should be used, but keeping it simple/portable as utility
            // If user meant strong encryption we would use AES. 
            // Given "salt" parameter, let's do a basic AES-like or salted hash? 
            // The prompt asks for "encrypt" into "encryptedString". Let's use AES with salt derived Key/IV.
            
            try 
            {
                using var aes = Aes.Create();
                var saltBytes = Encoding.UTF8.GetBytes(salt);
                // Derive key and IV from salt (insecure for prod but fits "salt" param usage without separate pass)
                // Using SHA256 of salt to get 32 bytes key, and MD5 of salt for 16 bytes IV
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                using var md5 = System.Security.Cryptography.MD5.Create();
                
                aes.Key = sha256.ComputeHash(saltBytes);
                aes.IV = md5.ComputeHash(saltBytes);

                var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using var ms = new MemoryStream();
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                using (var sw = new StreamWriter(cs, Encoding.UTF8))
                {
                    sw.Write(text);
                }
                return Convert.ToBase64String(ms.ToArray());
            } 
            catch { return ""; }
        }

        public static string Decrypt(string encryptedBase64, string salt)
        {
            if (string.IsNullOrEmpty(encryptedBase64) || string.IsNullOrEmpty(salt)) return "";
            try 
            {
                using var aes = Aes.Create();
                var saltBytes = Encoding.UTF8.GetBytes(salt);
                
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                using var md5 = System.Security.Cryptography.MD5.Create();
                
                aes.Key = sha256.ComputeHash(saltBytes);
                aes.IV = md5.ComputeHash(saltBytes);

                var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using var ms = new MemoryStream(Convert.FromBase64String(encryptedBase64));
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var sr = new StreamReader(cs, Encoding.UTF8);
                return sr.ReadToEnd();
            } 
            catch { return ""; }
        }

        public static string[] Split(string text, string separator)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<string>();
            return text.Split(new[] { separator }, StringSplitOptions.None);
        }

        public static decimal ToDecimal(string val)
        {
            if (decimal.TryParse(val, out var result)) return result;
            return 0;
        }

        public static object? SafeDeserializeJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var settings = new Newtonsoft.Json.JsonSerializerSettings
                {
                    Converters = new System.Collections.Generic.List<Newtonsoft.Json.JsonConverter> 
                    { 
                        new Newtonsoft.Json.Converters.ExpandoObjectConverter() 
                    }
                };
                return Newtonsoft.Json.JsonConvert.DeserializeObject<object>(json, settings);
            }
            catch
            {
                return json;
            }
        }

        public static object? DeepCopy(object? obj)
        {
            if (obj == null) return null;
            var json = System.Text.Json.JsonSerializer.Serialize(obj);
            
            var options = new System.Text.Json.JsonSerializerOptions();
            options.Converters.Add(new AxarDB.Storage.CustomObjectConverter());
            
            return System.Text.Json.JsonSerializer.Deserialize<object>(json, options);
        }

        // --- Date Utilities ---

        public static DateTime ConvertToDateTime(object dateObj)
        {
            if (dateObj == null) return ServerTime.Now;
            if (dateObj is DateTime dt) return dt;
            if (dateObj is string s && DateTime.TryParse(s, out var parsed)) return parsed;
            // Handle Jint Date instance if passed as object? 
            // Usually Jint marshals Date to DateTime automatically if typed, but as object it might be different.
            // For now rely on standard conversions.
            try { return Convert.ToDateTime(dateObj); } catch { return ServerTime.Now; }
        }

        public static DateTime AddMinutes(object dateObj, double minutes)
        {
            return ConvertToDateTime(dateObj).AddMinutes(minutes);
        }

        public static DateTime AddHours(object dateObj, double hours)
        {
            return ConvertToDateTime(dateObj).AddHours(hours);
        }

        public static DateTime AddDays(object dateObj, double days)
        {
            return ConvertToDateTime(dateObj).AddDays(days);
        }

        public static object Csv(object input)
        {
            if (input == null) return string.Empty;

            if (input is string csvString)
            {
                var lines = Split(csvString.Replace("\r\n", "\n"), "\n").Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
                if (lines.Count == 0) return new List<Dictionary<string, object>>();
                
                var headers = Split(lines[0], ",").Select(h => h.Trim()).ToList();
                var result = new List<Dictionary<string, object>>();

                for (int i = 1; i < lines.Count; i++)
                {
                    var values = Split(lines[i], ",");
                    var row = new Dictionary<string, object>();
                    for (int j = 0; j < headers.Count; j++)
                    {
                        if (j < values.Length)
                        {
                            var val = values[j].Trim();
                            if (decimal.TryParse(val, out var num)) row[headers[j]] = num;
                            else if (bool.TryParse(val, out var b)) row[headers[j]] = b;
                            else row[headers[j]] = val;
                        }
                        else
                        {
                            row[headers[j]] = string.Empty;
                        }
                    }
                    result.Add(row);
                }
                return result;
            }
            else
            {
                try
                {
                    var serialized = System.Text.Json.JsonSerializer.Serialize(input);
                    var deserializedObj = SafeDeserializeJson(serialized);
                    var deserialized = new List<IDictionary<string, object>>();
                    
                    if (deserializedObj is IEnumerable<object> list)
                    {
                        foreach (var item in list)
                        {
                            if (item is IDictionary<string, object> dict) deserialized.Add(dict);
                        }
                    }
                    else if (deserializedObj is IDictionary<string, object> singleDict)
                    {
                        deserialized.Add(singleDict);
                    }
                    
                    if (deserialized == null || deserialized.Count == 0) return string.Empty;

                    var headers = deserialized[0].Keys.ToList();
                    var sb = new StringBuilder();
                    sb.AppendLine(string.Join(",", headers));

                    foreach (var row in deserialized)
                    {
                        var values = new List<string>();
                        foreach (var header in headers)
                        {
                            if (row.TryGetValue(header, out var val) && val != null)
                            {
                                var valStr = val.ToString() ?? string.Empty;
                                if (valStr.Contains(",") || valStr.Contains("\"") || valStr.Contains("\n"))
                                {
                                    valStr = $"\"{valStr.Replace("\"", "\"\"")}\"";
                                }
                                values.Add(valStr);
                            }
                            else
                            {
                                values.Add(string.Empty);
                            }
                        }
                        sb.AppendLine(string.Join(",", values));
                    }
                    return sb.ToString().TrimEnd();
                }
                catch
                {
                    return string.Empty;
                }
            }
        }
    }
}
