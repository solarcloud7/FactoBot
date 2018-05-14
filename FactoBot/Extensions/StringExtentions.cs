using System;
using System.Collections.Generic;
using System.Text;

namespace FactoBot.Extensions
{
    public static class StringExtentions
    {
        public static string[] Split(this string toSplit, string splitOn)
        {
            return toSplit.Split(new string[] { splitOn }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
