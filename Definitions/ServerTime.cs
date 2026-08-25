using System;

namespace AxarDB.Definitions
{
    public static class ServerTime
    {
        public static DateTime Now => DateTime.UtcNow.AddHours(ConfigHelper.CurrentTimezoneOffset);
    }
}
