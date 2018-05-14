using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace FactoBot
{
    public class AppConfig
    {
        public static string BotToken { get; set; }
        public static string DebugBotToken { get; set; }
        public static string FactorioDirectory { get; set; }
        public static string ConfigDirectory { get; set; }
        public static ulong GuildId { get; set; }
        public static ulong ChannelId { get; set; }
    }
}
