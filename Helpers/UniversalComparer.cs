using System;
using System.Collections.Generic;
using System.Numerics;

namespace AxarDB.Helpers
{
    public class UniversalComparer : IComparer<object>
    {
        public static readonly UniversalComparer Instance = new UniversalComparer();

        public int Compare(object x, object y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            if (x is DateTime dx && y is DateTime dy) return dx.CompareTo(dy);
            if (x is DateTimeOffset dox && y is DateTimeOffset doy) return dox.CompareTo(doy);
            if (x is TimeSpan tx && y is TimeSpan ty) return tx.CompareTo(ty);
            
            if (x is IComparable cx && x.GetType() == y.GetType()) return cx.CompareTo(y);

            // Check if both are numeric
            if (IsNumeric(x) && IsNumeric(y))
            {
                if (x is BigInteger || y is BigInteger)
                {
                    try
                    {
                        BigInteger b1 = x is BigInteger bx ? bx : BigInteger.Parse(x.ToString());
                        BigInteger b2 = y is BigInteger by ? by : BigInteger.Parse(y.ToString());
                        return b1.CompareTo(b2);
                    }
                    catch { }
                }

                // Try converting to decimal for high precision
                try
                {
                    decimal d1 = Convert.ToDecimal(x);
                    decimal d2 = Convert.ToDecimal(y);
                    return d1.CompareTo(d2);
                }
                catch
                {
                    // Fallback to double (e.g. for BigInteger or extremely large floats)
                    try
                    {
                        double d1 = Convert.ToDouble(x);
                        double d2 = Convert.ToDouble(y);
                        return d1.CompareTo(d2);
                    }
                    catch
                    {
                        // Ignore
                    }
                }
            }

            if (x is string sx && y is string sy) 
            {
                bool isV7X = AxarDB.Helpers.GuidV7.IsVersion7(sx);
                bool isV7Y = AxarDB.Helpers.GuidV7.IsVersion7(sy);

                if (isV7X && !isV7Y) return -1; // UUIDv7 takes precedence (comes first)
                if (!isV7X && isV7Y) return 1;
                
                // If both are UUIDv7, or both are regular strings, standard Ordinal comparison works perfectly 
                // for chronological sorting of UUIDv7 because of its design.
                return string.Compare(sx, sy, StringComparison.Ordinal);
            }



            // Handle Jint Date objects by converting them to DateTime
            if (x.GetType().Name == "DateInstance" || y.GetType().Name == "DateInstance")
            {
                try
                {
                    DateTime dtX = ScriptUtils.ConvertToDateTime(x);
                    DateTime dtY = ScriptUtils.ConvertToDateTime(y);
                    return dtX.CompareTo(dtY);
                }
                catch { }
            }

            // Cannot compare, treat as equal to avoid exceptions during max/min
            return 0;
        }

        private bool IsNumeric(object value)
        {
            return value is sbyte
                || value is byte
                || value is short
                || value is ushort
                || value is int
                || value is uint
                || value is long
                || value is ulong
                || value is float
                || value is double
                || value is decimal
                || value is BigInteger;
        }
    }
}
