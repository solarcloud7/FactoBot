using System;

namespace FactoBot.Exceptions
{
    public class UserException : Exception
    {
        public static string EMOJI_CRY = "😢";
        public static string EMOJI_PUZZLED = "🤔";

        public string UserFriendlyMessage;
        public string Emoji;

        public UserException(string consoleMessage, string UserFriendlyMessage, string EmojiReaction = null) : base(consoleMessage)
        {
            this.UserFriendlyMessage = UserFriendlyMessage;
            this.Emoji = EmojiReaction;
        }
    }
}
