namespace MemeGodBot.ConsoleApp.Helpers
{
    public static class BotConstants
    {
        public static class Commands
        {
            public const string Start = "/start";
        }

        public static class Buttons
        {
            public const string GetMeme = "🎲 Дай мем";
            public const string Stats = "📊 Статистика";

            public const string Like = "🔥 Годно";
            public const string Dislike = "💩 Баян";
        }

        public static class Callbacks
        {
            public const string Like = "like";
            public const string Dislike = "dislike";
        }

        public static class Messages
        {
            public const string Welcome = "Привет! Я нейро-мемный бот. Жми на кнопку!";
            public const string Error = "Произошла ошибка. Попробуй позже.";
            public const string NoMemes = "😔 Мемы закончились. Заходи позже!";
            public const string MemeDeleted = "Мем был удален.";
        }
    }
}