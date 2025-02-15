// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;
using TelegramUI.Commands;
using static TelegramUI.Startup.Config;
using static TelegramUI.Commands.Language;
using System.Data.SQLite;
using Telegram.Bot.Types;

namespace TelegramUI.Telegram
{
    public static class TelegramCommands
    {
        internal static async void BotOnMessage(object sender, MessageEventArgs e)
        {
            if (e.Message == null || e.Message.Type != MessageType.Text) return;
            
            var entity = JsonSerializer.Serialize(e.Message, new JsonSerializerOptions()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
            });

            try
            {
                var msg = e.Message.Text;
                if (msg.EndsWith(BotUsername()))
                {
                    msg = msg.Substring(0, msg.Length - BotUsername().Length);
                }

                if (e.Message.Text == "/get" && e.Message.From.Id.ToString() == AdminId())
                {
                    await Bot.SendTextMessageAsync(
                        e.Message.Chat,
                        entity,
                        replyToMessageId: e.Message.MessageId);
                }

                if (e.Message.Chat.Type == ChatType.Private)
                {
                    switch (e.Message.Text)
                    {
                        case "/start":
                            await Bot.SendTextMessageAsync(
                                e.Message.Chat,
                                "Use /help to see what this bot can do.");
                            break;
                        case "/help":
                            await Bot.SendTextMessageAsync(
                                e.Message.Chat,
                                "This bot is a Genshin Impact Wish Simulator that works only in the group chats. Feel free to add it to your chat (privacy mode is on so no spying on whatever conversations you may have).\n\nUse <code>/wish</code> command to make a wish. You can wish every 2 hours per chat. The timer resets at the beginning of each hour..\nUse <code>/inv</code> command to see your inventory. Inventories are bound to the chats.\nUse <code>/lang [locale]</code> to change the language in a chat. Only chat admins can do so (make sure you're not anonymous!). Available locales: en, ua.",
                                ParseMode.Html);
                            break;
                        default:
                            if (e.Message.From.Id.ToString() == AdminId() && e.Message.ReplyToMessage != null)
                            {
                                await Bot.SendTextMessageAsync(
                                    e.Message.ReplyToMessage.ForwardFrom.Id,
                                    e.Message.Text);
                            }
                            break;
                    }
                    return;
                }

                if (e.Message.Chat.Type != ChatType.Group && e.Message.Chat.Type != ChatType.Supergroup) return;

                AddChat(e.Message);
                // Declare textsList outside of the try block to make it accessible later
                List<string> textsList = null;

                try
                {
                    string messageText = e.Message.Text;

                    var resourceName = $"TelegramUI.Strings.General.{GetLanguage(e.Message)}.json";
                    var assembly = typeof(Wish).Assembly;
                    var resourceStream = assembly.GetManifestResourceStream(resourceName);

                    if (resourceStream != null)
                    {
                        using var sReader = new StreamReader(resourceStream);
                        var textsText = sReader.ReadToEnd();
                        textsList = JsonSerializer.Deserialize<List<string>>(textsText);

                        // Now you can use textsList for further processing
                    }
                    else
                    {
                        Console.WriteLine($"Failed to retrieve the resource: {resourceName}");
                        return;  // Exit early as we can't proceed without the resource
                    }

                    // Rest of your code using textsList
                    if (e.Message.Chat.Type != ChatType.Group && e.Message.Chat.Type != ChatType.Supergroup)
                        return;


                }
                catch (Exception exception)
                {
                    // Handle exceptions here
                }


                switch (msg)
                {
                    case "/inv":
                        var results = Inventory.InventoryFetch(e.Message);
                        
                        while (true)
                        {
                            try
                            {
                                await Bot.SendTextMessageAsync(
                                    e.Message.Chat,
                                    string.Format(textsList[2], HttpUtility.HtmlEncode(e.Message.Chat.Title), HttpUtility.HtmlEncode(e.Message.From.FirstName), results),
                                    replyToMessageId: e.Message.MessageId,
                                    parseMode: ParseMode.Html);
                                break;
                            }
                            catch (Exception exception)
                            {
                                // ignored
                            }
                        }
                        break;

                    case "/wish":
                        var userId = e.Message.From.Id;
                        var chatId = e.Message.Chat.Id;

                        if (Wish.HasRolled(e.Message) == 1) // Check if user already rolled
                        {
                            if (Wish.GetStarglitter(userId,chatId) >= 10) // If user have 10 or more Starglitters
                            {
                                Wish.UseStarglitter(userId,chatId, 10); // Use it for extra wish (ignores timer)

                                int newBalance = Wish.GetStarglitter(userId, chatId); // Check balance after use

                                    try
                                    {
                                        await Bot.SendTextMessageAsync(
                                            e.Message.Chat.Id,
                                            string.Format(textsList[4], newBalance),
                                            replyToMessageId: e.Message.MessageId);

                                    }
                                    catch (Exception exception)
                                    {
                                    // ignored
                                    //Console.WriteLine("Помилка при відправці повідомлення Starglitter: " + exception.ToString());
                                }

                            }
                            else
                            {
                                using var con = new SQLiteConnection(MainDb());
                                con.Open();
                                using var cmd = new SQLiteCommand(con);
                                cmd.Parameters.AddWithValue("@user", userId);
                                cmd.Parameters.AddWithValue("@chat", chatId);
                                cmd.CommandText = "SELECT LastWishTime FROM UsersInChats WHERE UserId = @user AND ChatId = @chat";
                                var lastWishTime = cmd.ExecuteScalar();

                                DateTime lastWish = DateTime.Parse(lastWishTime.ToString());
                                DateTime nextWish = lastWish.AddHours(2); //add two hour wish interval
                                TimeSpan remaining = nextWish - DateTime.Now;

                                int hourDiff = remaining.Hours;
                                int minuteDiff = remaining.Minutes;

                                if (hourDiff < 0) hourDiff = 0;
                                if (minuteDiff < 0) minuteDiff = 0;

                                while (true)
                                {
                                    try
                                    {
                                        await Bot.SendTextMessageAsync(
                                            e.Message.Chat.Id,
                                            string.Format(textsList[1], hourDiff, minuteDiff),
                                            replyToMessageId: e.Message.MessageId);
                                        break;
                                    }
                                    catch (Exception exception)
                                    {
                                        // ignored
                                    }
                                }

                                return;
                            }
                        }

                        //If wish availiable, update wish time
                        Wish.SetWishTime(userId, chatId);

                        var onePull = Wish.GetCharacterPull(e.Message,true);

                        while (true)
                        {
                            try
                            {
                                await Bot.SendTextMessageAsync(
                                    e.Message.Chat.Id,
                                    text: $"{onePull[0]}<a href=\"{onePull[1]}\">\u200b</a>",
                                    parseMode: ParseMode.Html,
                                    disableWebPagePreview: false,
                                    replyToMessageId: e.Message.MessageId);
                                break;
                            }
                            catch (Exception exception)
                            {
                                // ignored
                            }
                        }
                        break;

                    case "/wish10":
                        var userId10 = e.Message.From.Id;
                        var chatId10 = e.Message.Chat.Id;
                        int balance = Wish.GetStarglitter(userId10, chatId10);

                        if ( balance>= 100) // Check Starglitter balance
                        {
                            Wish.UseStarglitter(userId10,chatId10, 100); // Substract 100 Starglitter

                            List<string[]> pulls = new List<string[]>();
                            for (int i = 0; i < 10; i++)
                            {
                                var pull = Wish.GetCharacterPull(e.Message, false);
                                pulls.Add(pull);
                            }
                            string getWish10Sum = Wish.GetWish10Summary(pulls);

                            int newBalance10 = Wish.GetStarglitter(userId10,chatId10); // Get new balance. Wishes may add starglitter as cashback

                            string resultMessage = string.Format(textsList[7], newBalance10, getWish10Sum);
                            
                            //If wish availiable, update wish time
                            Wish.SetWishTime(userId10, chatId10);

                            try
                            {
                                await Bot.SendTextMessageAsync(
                                    e.Message.Chat.Id,
                                    resultMessage,
                                    parseMode: ParseMode.Html,
                                    disableWebPagePreview: true,
                                    replyToMessageId: e.Message.MessageId);
                            }
                            catch (Exception exception)
                            {
                                // ignored
                            }
                        }
                        else
                        {
                            try
                            {
                                await Bot.SendTextMessageAsync(
                                    e.Message.Chat.Id,
                                    string.Format(textsList[8],balance), // Message about insufficient Starglitter
                                    replyToMessageId: e.Message.MessageId);
                            }
                            catch (Exception exception)
                            {
                                // ignored
                            }
                        }
                        break;

                    case "/resetUser":
                        if (e.Message.From.Id.ToString() != AdminId()) return;
                        Admin.ResetUser(e.Message);
                        await Bot.SendTextMessageAsync(
                            e.Message.Chat.Id,
                            $"Daily wish reset for {e.Message.ReplyToMessage.From.FirstName}!",
                            replyToMessageId: e.Message.MessageId);
                        break;
                    case "/resetChat":
                        if (e.Message.From.Id.ToString() != AdminId()) return;
                        Admin.ResetChat(e.Message);
                        await Bot.SendTextMessageAsync(
                            e.Message.Chat.Id,
                            $"Daily wish reset for everyone in {e.Message.Chat.Title}!",
                            replyToMessageId: e.Message.MessageId);
                        break;

                    case "/balance":
                        var userIdcheck = e.Message.From.Id;
                        var chatIdcheck = e.Message.Chat.Id;
                        int balanceCheck = Wish.GetStarglitter(userIdcheck, chatIdcheck);

                        await Bot.SendTextMessageAsync(
                        e.Message.Chat.Id,
                        $"{ e.Message.From.FirstName}, you have {balanceCheck} Starglitter✨ in {e.Message.Chat.Title}!",
                        replyToMessageId: e.Message.MessageId);
                        break;

                    default:
                        if (e.Message.Text.StartsWith("/lang "))
                        {
                            var admins = await Bot.GetChatAdministratorsAsync(e.Message.Chat.Id);
                            foreach (var cM in admins)
                            {
                                if (cM.User.Id == e.Message.From.Id)
                                {
                                    switch (msg)
                                    {
                                        case "/lang en":
                                            ChangeLanguage(e.Message, "en");
                                            await Bot.SendTextMessageAsync(
                                                e.Message.Chat,
                                                "Language changed to English.");
                                            break;
                                        case "/lang ua":
                                            ChangeLanguage(e.Message, "ua");
                                            await Bot.SendTextMessageAsync(
                                                e.Message.Chat,
                                                "Мова бота тепер державна.");
                                            break;
                                    }
                                }
                            }
                        }
                        break;
                }

                switch (e.Message.Text.Split(' ')[0]) // Беремо першу частину повідомлення (до пробілу)
                {
                    case "/addStar":
                        // Перевірка на правильність адміністраторських прав
                        if (e.Message.From.Id.ToString() != AdminId()) return;

                        // Перевірка, чи команда відправлена у відповідь на повідомлення користувача
                        if (e.Message.ReplyToMessage == null || e.Message.ReplyToMessage.From == null)
                        {
                            try
                            {
                                await Bot.SendTextMessageAsync(
                                    e.Message.Chat.Id,
                                    "This command must be used in response to user message!",
                                    replyToMessageId: e.Message.MessageId);
                                return;
                            }
                            catch (Exception exception)
                            {
                            }
                        }

                        // Отримуємо ID користувача та чат
                        var targetUserId = e.Message.ReplyToMessage.From.Id;
                        var targetChatId = e.Message.Chat.Id;
                        int amount = 0;

                        // Перевіряємо чи є параметри (amount) після команди
                        var args = e.Message.Text.Split(' '); // Розбиваємо повідомлення на частини
                        if (args.Length < 2 || !int.TryParse(args[1], out amount) || amount <= 0)
                        {
                            try
                            {
                                await Bot.SendTextMessageAsync(
                                    e.Message.Chat.Id,
                                    "Please use the format command: /addStar[amount]",
                                    replyToMessageId: e.Message.MessageId);
                                return;
                            }
                            catch (Exception exception) { }
                        }

                        // Викликаємо метод для додавання Starglitter
                        Wish.AddStarglitter(targetUserId, targetChatId, amount);
                        int newBalance = Wish.GetStarglitter(targetUserId, targetChatId);

                        // Відправка повідомлення про успішне виконання команди
                        try
                        {
                            await Bot.SendTextMessageAsync(
                                e.Message.Chat.Id,
                                $"{amount}✨ have been sent to the user {e.Message.ReplyToMessage.From.FirstName}!\n New balance is: {newBalance}✨",
                                replyToMessageId: e.Message.MessageId);
                        }
                        catch (Exception exception) { }

                        break;
                }

            }
            catch (Exception exception)
            {
                if (exception.Message.Contains("Response status code does not indicate success: 429 (Too Many Requests)"))
                {
                    return;
                }
                
                await Bot.SendTextMessageAsync(
                    AdminId(),
                    $"Error: {exception.Message}\n{exception.StackTrace}\n\nEntity: {entity}");
            }
        }
    }
}