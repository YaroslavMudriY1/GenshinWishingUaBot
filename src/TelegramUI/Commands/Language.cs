// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramUI.Strings.Items;
using static TelegramUI.Startup.Config;

namespace TelegramUI.Commands
{
    public class Language
    {
        internal static void AddChat(Message message)
        {
            using var con = new SQLiteConnection(MainDb());
            con.Open();

            using var cmd = new SQLiteCommand(con);
            cmd.Parameters.Add(new SQLiteParameter("@chat", message.Chat.Id));

            cmd.CommandText = "INSERT OR IGNORE INTO Chats(ChatId) VALUES(@chat)";
            cmd.ExecuteNonQuery();

            con.Close();
        }

        internal static void ChangeLanguage(Message message, string locale)
        {
            using var con = new SQLiteConnection(MainDb());
            con.Open();

            using var cmd = new SQLiteCommand(con);
            cmd.Parameters.Add(new SQLiteParameter("@chat", message.Chat.Id));
            cmd.Parameters.Add(new SQLiteParameter("@locale", locale));

            cmd.CommandText = "UPDATE Chats SET Language = @locale WHERE ChatId = @chat";
            cmd.ExecuteNonQuery();

            con.Close();
        }

        internal static string GetLanguage(Message message)
        {
            using var con = new SQLiteConnection(MainDb());
            con.Open();
            var language = "en";

            if (!string.IsNullOrEmpty(message.Text) && message.Text.StartsWith("/lang"))
            {
                // Handle /lang command without specifying a language
                return language;
            }

            using var cmd = new SQLiteCommand(con);
            cmd.Parameters.Add(new SQLiteParameter("@chat", message.Chat.Id));

            cmd.CommandText = "SELECT Language FROM Chats WHERE ChatId = @chat";
            using var rdr = cmd.ExecuteReader();

            if (rdr.Read() && !rdr.IsDBNull(0))
            {
                var languageObj = rdr.GetValue(0);
                if (languageObj != null)
                {
                    language = languageObj.ToString();
                }
            }

            con.Close();

            return language;
        }

        internal static string GetItemName(string itemId, string language)
        {
            var items = typeof(Wish).Assembly.GetManifestResourceStream($"TelegramUI.Strings.Items.{language}.json");
            var sR = new StreamReader(items);
            var itemsText = sR.ReadToEnd();
            sR.Close();

            var itemsList = JsonSerializer.Deserialize<List<Items>>(itemsText);
            var item = itemsList.Find(x => x.Id.ToLower() == itemId.ToLower());

            return item?.Name ?? itemId;
        }

        public static async Task RandomJoke(MessageEventArgs e)
        {
            try
            {
                var resourceName = $"TelegramUI.Strings.Misc.jokes_{GetLanguage(e.Message)}.json";
                var jokes = LoadEmbeddedJsonList(resourceName);

                if (jokes != null && jokes.Count > 0)
                {
                    var rnd = new Random();
                    var randomJoke = jokes[rnd.Next(jokes.Count)];

                    await Bot.SendTextMessageAsync(
                        e.Message.Chat.Id,
                        randomJoke,
                        replyToMessageId: e.Message.MessageId);
                }
                else
                {
                    await Bot.SendTextMessageAsync(
                        e.Message.Chat.Id,
                        "Jokes list is empty. Never late to make a new one, eh?",
                        replyToMessageId: e.Message.MessageId);
                }
            }
            catch (Exception exception)
            {
                // Handle exceptions here
            }         


            }
        public static async Task RandomPaimonPhrase(MessageEventArgs e)
        {
            try
            {
                var resourceName = $"TelegramUI.Strings.Misc.paimonResponses_{GetLanguage(e.Message)}.json";
                var assembly = typeof(Wish).Assembly;
                var responses = LoadEmbeddedJsonList(resourceName);

                if (responses != null && responses.Count > 0)
                {
                    var rnd = new Random();
                    var randomPhrase = responses[rnd.Next(responses.Count)];

                    await Bot.SendTextMessageAsync(
                        e.Message.Chat.Id,
                        randomPhrase,
                        replyToMessageId: e.Message.MessageId);
                }
                else
                {
                    await Bot.SendTextMessageAsync(
                        e.Message.Chat.Id,
                        "Paimon has nothing to say right now.",
                        replyToMessageId: e.Message.MessageId);
                }
            }
            catch (Exception exception)
            {
                // Handle exceptions here
            }

        }
        public static List<string> LoadEmbeddedJsonList(string resourceKey)
        {
            var assembly = typeof(Wish).Assembly;
            var resourceStream = assembly.GetManifestResourceStream(resourceKey);

            if (resourceStream == null)
            {
                Console.WriteLine($"Resource not found: {resourceKey}");
                return null;
            }

            using var sReader = new StreamReader(resourceStream);
            var content = sReader.ReadToEnd();
            return JsonSerializer.Deserialize<List<string>>(content);
        }

    }
    }