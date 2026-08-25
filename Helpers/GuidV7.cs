using System.Security.Cryptography;

namespace AxarDB.Helpers
{
    /// <summary>
    /// RFC 9562 (UUIDv7) compliant GUID generator.
    ///
    /// UUIDv7 layout (128 bits):
    ///   [0..47]  : Unix epoch milliseconds, big-endian  — 48 bits
    ///   [48..51] : version = 0111 (7)                   —  4 bits
    ///   [52..63] : sub-millisecond sequence counter      — 12 bits
    ///   [64..65] : variant = 10                          —  2 bits
    ///   [66..127]: random data                           — 62 bits
    ///
    /// Monotonicity: when multiple UUIDs are generated within the same
    /// millisecond the sequence counter is incremented. If the counter
    /// overflows the timestamp is advanced by one millisecond.
    /// </summary>
    public static class GuidV7
    {
        // Lock and counters for monotonic generation
        private static readonly object _lock = new();
        private static long _lastMs = -1L;
        private static ushort _seq = 0;

        /// <summary>
        /// Generates a new UUID v7 using the current UTC time.
        /// </summary>
        public static Guid NewGuid() =>
            NewGuid(DateTimeOffset.UtcNow);

        /// <summary>
        /// Generates a new UUID v7 using the specified timestamp.
        /// The timestamp is normalised to UTC before encoding.
        /// </summary>
        public static Guid NewGuid(DateTimeOffset timestamp)
        {
            long ms = timestamp.ToUniversalTime().ToUnixTimeMilliseconds();
            ushort seq;

            lock (_lock)
            {
                if (ms < _lastMs)
                {
                    // When a past timestamp is supplied, sort order is not
                    // guaranteed — this is expected behaviour. Clamp to lastMs
                    // to preserve monotonicity for the current-time path.
                    ms = _lastMs;
                }

                if (ms == _lastMs)
                {
                    _seq++;
                    if (_seq > 0x0FFF) // 12-bit overflow: advance to next ms
                    {
                        _seq = 0;
                        ms = _lastMs + 1;
                        _lastMs = ms;
                    }
                    seq = _seq;
                }
                else
                {
                    _lastMs = ms;
                    _seq = 0;
                    seq = 0;
                }
            }

            return Build(ms, seq);
        }

        /// <summary>
        /// Extracts the UTC creation timestamp embedded in a UUID v7 string.
        /// Returns <see langword="null"/> if the input is not a valid v7 GUID.
        /// </summary>
        public static DateTimeOffset? GetTimestamp(string guidStr)
        {
            if (!Guid.TryParse(guidStr, out var g))
                return null;

            return GetTimestamp(g);
        }

        /// <summary>
        /// Extracts the UTC creation timestamp embedded in a <see cref="Guid"/> value.
        /// Returns <see langword="null"/> if the GUID is not version 7.
        /// </summary>
        public static DateTimeOffset? GetTimestamp(Guid g)
        {
            if (!IsVersion7(g))
                return null;

            var bytes = g.ToByteArray(); // .NET little-endian layout

            // .NET Guid byte layout : [3,2,1,0, 5,4, 7,6, 8,9, 10,11,12,13,14,15]
            // UUID wire order       : [0,1,2,3, 4,5, 6,7, 8,9, 10,11,12,13,14,15]
            // 48-bit timestamp occupies wire bytes 0-5
            long ms = ((long)(bytes[3]) << 40)
                    | ((long)(bytes[2]) << 32)
                    | ((long)(bytes[1]) << 24)
                    | ((long)(bytes[0]) << 16)
                    | ((long)(bytes[5]) << 8)
                    | ((long)(bytes[4]));

            return DateTimeOffset.FromUnixTimeMilliseconds(ms);
        }

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="guidStr"/> is a valid UUID version 7.
        /// </summary>
        public static bool IsVersion7(string guidStr) =>
            Guid.TryParse(guidStr, out var g) && IsVersion7(g);

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="g"/> is UUID version 7.
        /// </summary>
        public static bool IsVersion7(Guid g)
        {
            var bytes = g.ToByteArray();
            // byte[7] in .NET layout corresponds to wire byte 6, which holds the version nibble
            return (bytes[7] >> 4) == 7;
        }

        // ── private helpers ───────────────────────────────────────────────────────

        private static Guid Build(long ms, ushort seq)
        {
            // Build the 16-byte UUID in wire (big-endian) format
            Span<byte> wire = stackalloc byte[16];

            // Bytes 0-5: 48-bit Unix milliseconds, big-endian
            wire[0] = (byte)(ms >> 40);
            wire[1] = (byte)(ms >> 32);
            wire[2] = (byte)(ms >> 24);
            wire[3] = (byte)(ms >> 16);
            wire[4] = (byte)(ms >> 8);
            wire[5] = (byte)(ms);

            // Bytes 6-7: version nibble (0111) | 12-bit sequence
            // wire[6] = 0111xxxx  → version 7 | seq high nibble
            wire[6] = (byte)(0x70 | ((seq >> 8) & 0x0F));
            wire[7] = (byte)(seq & 0xFF);

            // Bytes 8-15: RFC 4122 variant bits (10) | 62 random bits
            Span<byte> rand = stackalloc byte[8];
            RandomNumberGenerator.Fill(rand);

            wire[8]  = (byte)(0x80 | (rand[0] & 0x3F)); // variant = 10xxxxxx
            wire[9]  = rand[1];
            wire[10] = rand[2];
            wire[11] = rand[3];
            wire[12] = rand[4];
            wire[13] = rand[5];
            wire[14] = rand[6];
            wire[15] = rand[7];

            // .NET Guid internal layout (ToByteArray): [a3,a2,a1,a0, b1,b0, c1,c0, d0..d7]
            // When constructing via Guid(int a, short b, short c, byte d0..d7):
            //   a      = wire[0..3] interpreted as big-endian int
            //   b      = wire[4..5] interpreted as big-endian short
            //   c      = wire[6..7] interpreted as big-endian short
            //   d0..d7 = wire[8..15] byte-for-byte

            int   a = (wire[0] << 24) | (wire[1] << 16) | (wire[2] << 8) | wire[3];
            short b = (short)((wire[4] << 8) | wire[5]);
            short c = (short)((wire[6] << 8) | wire[7]);

            return new Guid(a, b, c,
                wire[8], wire[9], wire[10], wire[11],
                wire[12], wire[13], wire[14], wire[15]);
        }
    }
}
