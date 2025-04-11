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
using Telegram.Bot.Types.ReplyMarkups;

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
                                "This bot is a Genshin Impact Wish Simulator that works only in the group chats. Feel free to add it to your chat (privacy mode is on so no spying on whatever conversations you may have).\n\nUse <code>/wish</code> command to make a wish. You can wish every 2 hours per chat.\nUse <code>/inv</code> command to see your inventory. Inventories are bound to the chats.\nUse <code>/lang [locale]</code> to change the language in a chat. Only chat admins can do so (make sure you're not anonymous!). Available locales: en, ua.",
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
                    case "/inv" or "/inventory":
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
                            int userBalance = Wish.GetStarglitter(userId, chatId);
                            //Get last wish time info
                            DateTime lastWish;
                            int hourDiff, minuteDiff;

                            using var con0 = new SQLiteConnection(MainDb());
                            con0.Open();
                            using var cmd0 = new SQLiteCommand(con0);
                            cmd0.Parameters.AddWithValue("@user", userId);
                            cmd0.Parameters.AddWithValue("@chat", chatId);
                            cmd0.CommandText = "SELECT LastWishTime FROM UsersInChats WHERE UserId = @user AND ChatId = @chat";
                            var lastWishTime = cmd0.ExecuteScalar();
                            con0.Close();

                            lastWish = DateTime.Parse(lastWishTime.ToString());
                            DateTime nextWish = lastWish.AddHours(2); // add two hour wish interval
                            TimeSpan remaining = nextWish - DateTime.Now;

                            hourDiff = remaining.Hours;
                            minuteDiff = remaining.Minutes;

                            if (hourDiff < 0) hourDiff = 0;
                            if (minuteDiff < 0) minuteDiff = 0;

                            // Variant 1: Auto-wish on, starglitter balance >10.
                            if (Wish.GetAutoWishSetting(userId,chatId)&&Wish.GetStarglitter(userId,chatId) >= 10) // If user have 10 or more Starglitters
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
                                    // Exceprion ignored
                                    //Console.WriteLine("Error while sending message. Starglitter: " + exception.ToString());
                                }

                            }
                            // Variant 2: Not enough starglitter (<10)
                            else if (Wish.GetStarglitter(userId, chatId) <= 10)
                            {
                                // Tell user they need to wait
                                await Bot.SendTextMessageAsync(
                                    e.Message.Chat.Id,
                                    string.Format(textsList[1], userBalance, hourDiff, minuteDiff),
                                    replyToMessageId: e.Message.MessageId);
                                return;
                            }
                            // Variant 3: Auto-wish off
                            else if (!Wish.GetAutoWishSetting(userId, chatId))
                            {
                                // Tell user they need to wait
                                await Bot.SendTextMessageAsync(
                                    e.Message.Chat.Id,
                                    string.Format(textsList[9], userBalance, hourDiff, minuteDiff),
                                    replyToMessageId: e.Message.MessageId);
                                return;
                            }
                            else
                            {
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

                        if (balance>= 100) // Check Starglitter balance
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

                    case "/trade":
                        await Bot.SendTextMessageAsync(
                        e.Message.Chat.Id,
                        string.Format(textsList[13]), // Message about /trade command
                        replyToMessageId: e.Message.MessageId);
                        break;

                    case string s when s.StartsWith("/trade "):
                        if (e.Message.ReplyToMessage == null)
                        {
                            await Bot.SendTextMessageAsync(
                                e.Message.Chat.Id,
                                "You need to reply to a user's message to trade with them.",
                                replyToMessageId: e.Message.MessageId);
                            break;
                        }

                        var tradeResult = await Trade.InitiateTrade(e.Message);

                        if (tradeResult.Success)
                        {
                            var offerParts = e.Message.Text.Split(' ');
                            var offerQuantity = 1;
                            string offerItemId, requestItemId;
                            var requestQuantity = 1;

                            if (offerParts.Length >= 3)
                            {
                                if (int.TryParse(offerParts[1], out int qty))
                                {
                                    offerQuantity = qty;
                                    offerItemId = offerParts[2];
                                }
                                else
                                {
                                    offerItemId = offerParts[1];
                                }

                                if (offerParts.Length >= 4)
                                {
                                    if (int.TryParse(offerParts[offerParts.Length - 2], out int reqQty))
                                    {
                                        requestQuantity = reqQty;
                                        requestItemId = offerParts[offerParts.Length - 1];
                                    }
                                    else
                                    {
                                        requestItemId = offerParts[offerParts.Length - 1];
                                    }
                                }
                                else
                                {
                                    requestItemId = offerParts[offerParts.Length - 1];
                                }

                                var offerItem = Language.GetItemName(offerItemId, GetLanguage(e.Message));
                                var requestItem = Language.GetItemName(requestItemId, GetLanguage(e.Message));

                                var userIdFrom = e.Message.From.Id;
                                var userIdTo = e.Message.ReplyToMessage.From.Id;
                                var chatIdTrade = e.Message.Chat.Id;

                                var confirmTradeButton = InlineKeyboardButton.WithCallbackData(
                                    "✅ Accept trade",
                                    $"confirm_trade:{userIdFrom}:{userIdTo}:{offerItemId}:{offerQuantity}:{requestItemId}:{requestQuantity}"
                                );

                                var cancelTradeButton = InlineKeyboardButton.WithCallbackData(
                                    "❌ Decline",
                                    $"cancel_trade:{userIdTo}:{userIdFrom}"
                                );

                                var tradeKeyboard = new InlineKeyboardMarkup(new[]
                                {
                                    new[] { confirmTradeButton },
                                    new[] { cancelTradeButton }
                                });

                                var tradeMessage = await Bot.SendTextMessageAsync(
                                    e.Message.Chat.Id,
                                    $"{HttpUtility.HtmlEncode(e.Message.From.FirstName)} offers {offerQuantity}x {offerItem} to {HttpUtility.HtmlEncode(e.Message.ReplyToMessage.From.FirstName)} in exchange for {requestQuantity}x {requestItem}",
                                    replyToMessageId: e.Message.ReplyToMessage.MessageId,
                                    parseMode: ParseMode.Html,
                                    replyMarkup: tradeResult.ReplyMarkup);

                                // Update MessageId and start timer
                                await Trade.UpdateMessageIdAndStartTimer(
                                    tradeResult.TradeKey,
                                    tradeMessage.MessageId,
                                    e.Message.From.FirstName,
                                    e.Message.ReplyToMessage.From.FirstName);
                            }
                        }
                        else
                        {
                            await Bot.SendTextMessageAsync(
                                e.Message.Chat.Id,
                                tradeResult.ErrorMessage,
                                replyToMessageId: e.Message.MessageId);
                        }
                        break;
                    case "/sell":
                        await Bot.SendTextMessageAsync(
                                       e.Message.Chat.Id,
                                       textsList[17], // "Usage: /sell [amount] <itemId>"
                                       replyToMessageId: e.Message.MessageId);
                        break;

                    case string command when command.StartsWith("/sell "):
                        string sellParams = e.Message.Text.Substring("/sell ".Length).Trim();
                        string[] parameters = sellParams.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                        string itemId;
                        int amount = 1; // Default amount

                        if (parameters.Length == 1)
                        {
                            // Only item ID provided, use default amount = 1
                            itemId = parameters[0];
                        }
                        else if (parameters.Length >= 2)
                        {
                            // Try to parse first parameter as amount
                            if (int.TryParse(parameters[0], out int parsedAmount) && parsedAmount > 0)
                            {
                                amount = parsedAmount;
                                itemId = parameters[1];
                            }
                            else
                            {
                                // First parameter is not a valid amount, assume it's the item ID
                                itemId = parameters[0];
                            }
                        }
                        else
                        {
                            // No parameters provided
                            while (true)
                            {
                                try
                                {
                                    await Bot.SendTextMessageAsync(
                                        e.Message.Chat.Id,
                                        textsList[17], // "Usage: /sell [amount] <itemId>"
                                        replyToMessageId: e.Message.MessageId);
                                    break;
                                }
                                catch (Exception exception)
                                {
                                    // ignored
                                }
                            }
                            break;
                        }

                        // Execute sell operation
                        string sellResult = Inventory.SellItem(e.Message, itemId, amount);

                        while (true)
                        {
                            try
                            {
                                await Bot.SendTextMessageAsync(
                                    e.Message.Chat.Id,
                                    sellResult,
                                    replyToMessageId: e.Message.MessageId);
                                break;
                            }
                            catch (Exception exception)
                            {
                                // ignored
                            }
                        }
                        break;

                    case "/get_id":
                        await Bot.SendTextMessageAsync(
                                        e.Message.Chat.Id,
                                        textsList[12], // "Usage: /get_id <item_name>"
                                        replyToMessageId: e.Message.MessageId);
                        break;
                        
                    case string command when command.StartsWith("/get_id "):
                        string itemName = e.Message.Text.Substring("/get_id ".Length).Trim();
                        if (!string.IsNullOrEmpty(itemName))
                        {
                            string idResult = Inventory.GetItemId(e.Message, itemName);

                            while (true)
                            {
                                try
                                {
                                    await Bot.SendTextMessageAsync(
                                        e.Message.Chat.Id,
                                        idResult,
                                        replyToMessageId: e.Message.MessageId);
                                    break;
                                }
                                catch (Exception exception)
                                {
                                    // ignored
                                }
                            }
                        }
                        else
                        {
                            // Empty parameter, send usage instruction
                            while (true)
                            {
                                try
                                {
                                    await Bot.SendTextMessageAsync(
                                        e.Message.Chat.Id,
                                        textsList[12], // "Usage: /get_id <item_name>"
                                        replyToMessageId: e.Message.MessageId);
                                    break;
                                }
                                catch (Exception exception)
                                {
                                    // ignored
                                }
                            }
                        }
                        break;
                    case "/get_info" or "/get_inv":
                        if (e.Message.ReplyToMessage != null)
                        {
                            var targetUser = e.Message.ReplyToMessage.From;
                            var targetUserId = targetUser.Id;
                            string targetUsername = targetUser.FirstName;

                            var userInv = Inventory.InventoryFetch(e.Message, targetUserId, targetUsername);

                            while (true)
                            {
                                try
                                {
                                    await Bot.SendTextMessageAsync(
                                        e.Message.Chat,
                                        string.Format(textsList[18], HttpUtility.HtmlEncode(e.Message.Chat.Title), HttpUtility.HtmlEncode(targetUsername), userInv),
                                        replyToMessageId: e.Message.MessageId,
                                        parseMode: ParseMode.Html);
                                    break;
                                }
                                catch (Exception exception)
                                {
                                    // ignored
                                }
                            }
                        }
                        else
                        {
                            // If command sent not in reply
                            await Bot.SendTextMessageAsync(
                                e.Message.Chat.Id,
                                "This command must be used in reply to another user's message.",
                                replyToMessageId: e.Message.MessageId);
                        }
                        break;
                    case "/resetUser":
                        if (e.Message.From.Id.ToString() != AdminId()) return;
                        Admin.ResetUser(e.Message);
                        await Bot.SendTextMessageAsync(
                            e.Message.Chat.Id,
                            $"Wish reset for {e.Message.ReplyToMessage.From.FirstName}!",
                            replyToMessageId: e.Message.MessageId);
                        break;
                    case "/resetChat":
                        if (e.Message.From.Id.ToString() != AdminId()) return;
                        Admin.ResetChat(e.Message);
                        await Bot.SendTextMessageAsync(
                            e.Message.Chat.Id,
                            $"Wish reset for everyone in {e.Message.Chat.Title}!",
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

                    case "/settings":
                        var userIdSettings = e.Message.From.Id;
                        var chatIdSettings = e.Message.Chat.Id;
                        bool autoWishEnabled = Wish.GetAutoWishSetting(userIdSettings, chatIdSettings);
                        string statusText = autoWishEnabled ? "On" : "off";
                        string buttonText = autoWishEnabled ? "Turn off auto-wish" : "Turn on auto-wish";

                        var keyboard = new InlineKeyboardMarkup(new[]
                        {
                          new []
                                {
                              InlineKeyboardButton.WithCallbackData(buttonText, $"toggle_autowish:{userIdSettings}:{chatIdSettings}")
                                }
                            });

                        await Bot.SendTextMessageAsync(
                            chatId: e.Message.Chat.Id,
                            text: $"Personal setting of user {e.Message.From.FirstName}.\nAuto-wish status: {statusText}.",
                            replyMarkup: keyboard,
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

                // This will handle FAQ responses when users reply to the bot's messages
                // Check if message is a reply to the bot's message
                if (e.Message.ReplyToMessage != null && e.Message.ReplyToMessage.From.Id == Bot.BotId)
                {
                    // Convert message to lowercase for case-insensitive matching
                    string lowerMessage = e.Message.Text.ToLower();

                    //Jokes (rofls)
                    if (lowerMessage.Contains("анекдот")||
                        lowerMessage.Contains("жарт")||
                        lowerMessage.Contains("смішнявка")||
                        lowerMessage.Contains("сміх")||
                        lowerMessage.Contains("rofl")||
                        lowerMessage.Contains("joke"))
                    {
                        await Language.RandomJoke(e);
                        return;
                    }

                    // FAQ patterns for selling items
                    if (lowerMessage.Contains("як продати") ||
                        lowerMessage.Contains("how to sell") ||
                        lowerMessage.Contains("продати") ||
                        lowerMessage.Contains("продаж") ||
                        lowerMessage.Contains("sell"))
                    {
                        try
                        {
                            string sellHelpMessage = string.Format(textsList[19],
                                                                  textsList[20], // 5-star reward info
                                                                  textsList[21], // 4-star reward info
                                                                  textsList[22]); // 3-star reward info
                            await Bot.SendTextMessageAsync(
                                e.Message.Chat.Id,
                                sellHelpMessage,
                                replyToMessageId: e.Message.MessageId,
                                parseMode: ParseMode.Html);
                        }
                        catch (Exception exception)
                        {
                            // ignored
                        }
                    }
                    // FAQ patterns for inventory
                    else if (lowerMessage.Contains("інвентар") ||
                             lowerMessage.Contains("inventory") ||
                             lowerMessage.Contains("предмети") ||
                             lowerMessage.Contains("items"))
                    {
                        try
                        {
                            await Bot.SendTextMessageAsync(
                                e.Message.Chat.Id,
                                textsList[23], // Inventory help text
                                replyToMessageId: e.Message.MessageId,
                                parseMode: ParseMode.Html);
                        }
                        catch (Exception exception)
                        {
                            // ignored
                        }
                    }
                    // FAQ patterns for item IDs
                    else if (lowerMessage.Contains("id") ||
                             lowerMessage.Contains("ід") ||
                             lowerMessage.Contains("айді"))
                    {
                        try
                        {
                            await Bot.SendTextMessageAsync(
                                e.Message.Chat.Id,
                                textsList[24], // ID help text
                                replyToMessageId: e.Message.MessageId,
                                parseMode: ParseMode.Html);
                        }
                        catch (Exception exception)
                        {
                            // ignored
                        }
                    }
                    // FAQ patterns for wishing
                    else if (lowerMessage.Contains("бажання") ||
                             lowerMessage.Contains("wish") ||
                             lowerMessage.Contains("молитв") ||
                             lowerMessage.Contains("банер") ||
                            lowerMessage.Contains("віш"))
                    {
                        try
                        {
                            await Bot.SendTextMessageAsync(
                                e.Message.Chat.Id,
                                textsList[25], // Wish help text
                                replyToMessageId: e.Message.MessageId,
                                parseMode: ParseMode.Html);
                        }
                        catch (Exception exception)
                        {
                            // ignored
                        }
                    }
                    // FAQ patterns for trading (assuming there's a trade feature)
                    else if (lowerMessage.Contains("трейд") ||
                             lowerMessage.Contains("trade") ||
                             lowerMessage.Contains("обмін") ||
                             lowerMessage.Contains("обменять"))
                    {
                        try
                        {
                            await Bot.SendTextMessageAsync(
                                e.Message.Chat.Id,
                                textsList[26], // Trade help text
                                replyToMessageId: e.Message.MessageId,
                                parseMode: ParseMode.Html);
                        }
                        catch (Exception exception)
                        {
                            // ignored
                        }
                    }
                    // FAQ patterns for general help
                    else if (lowerMessage.Contains("допомога") ||
                             lowerMessage.Contains("help") ||
                             lowerMessage.Contains("помощь") ||
                             lowerMessage == "?")
                    {
                        try
                        {
                            await Bot.SendTextMessageAsync(
                                e.Message.Chat.Id,
                                textsList[27], // General help text with all commands
                                replyToMessageId: e.Message.MessageId,
                                parseMode: ParseMode.Html);
                        }
                        catch (Exception exception)
                        {
                            // ignored
                        }
                    }
                }

                switch (e.Message.Text.Split(' ')[0]) // Get first part of message (before space)
                {
                    case "/addStar":
                        // Check admin rights
                        if (e.Message.From.Id.ToString() != AdminId()) return;

                        // Check if message send as reply
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
                                //ignored
                            }
                        }

                        // Get user and chat ID
                        var targetUserId = e.Message.ReplyToMessage.From.Id;
                        var targetChatId = e.Message.Chat.Id;
                        int amount = 0;

                        // Check if attributes (amount) exists
                        var args = e.Message.Text.Split(' '); // Split message on parts
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

                        // Call add starglitter method
                        Wish.AddStarglitter(targetUserId, targetChatId, amount);
                        int newBalance = Wish.GetStarglitter(targetUserId, targetChatId);

                        // Sent message about succesful adding starglitter
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