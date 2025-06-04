// SPDX-License-Identifier: MPL-2.0

using System;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramUI.Commands;
using TelegramUI.Scheduler;
using TelegramUI.Telegram;
using static TelegramUI.Startup.Config;

namespace TelegramUI
{
    public static class Program
    {
        private static void Main()
        {
            TaskScheduler.Instance.ScheduleDailyReset(); // Reset wish at startup
            TaskScheduler.Instance.ScheduleDailyRewardReset(); // Reset daily rewards at startup

            //[Obsolete]
            // Need to rewrite this in future versions
            Bot.OnMessage += TelegramCommands.BotOnMessage;
            Bot.OnCallbackQuery += BotOnCallbackQueryReceived;

            Bot.StartReceiving();
            Console.ReadLine();
            Bot.StopReceiving();
        }

        //Callback handler (for inline buttons and callback queries)
        private static async void BotOnCallbackQueryReceived(object sender, CallbackQueryEventArgs e)
        {
            var callbackQuery = e.CallbackQuery;

            if (callbackQuery.Data.StartsWith("toggle_autowish:"))
            {
                var parts = callbackQuery.Data.Split(':');
                if (parts.Length == 3 && long.TryParse(parts[1], out long userId) && long.TryParse(parts[2], out long chatId))
                {
                    // Toggle the setting
                    Wish.ToggleAutoWishSetting(userId, chatId);

                    // Get the new status
                    bool newStatus = Wish.GetAutoWishSetting(userId, chatId);
                    string statusText = newStatus ? "on" : "off";
                    string buttonText = newStatus ? "Turn off auto-wish" : "Turn on auto-wish";

                    // Update the keyboard
                    var keyboard = new InlineKeyboardMarkup(new[]
                    {
                        new []
                        {
                            InlineKeyboardButton.WithCallbackData(buttonText, $"toggle_autowish:{userId}:{chatId}")
                        }
                    });

                    // Update the message
                    await Bot.EditMessageTextAsync(
                        chatId: e.CallbackQuery.Message.Chat.Id,
                        messageId: e.CallbackQuery.Message.MessageId,
                        text: $"Personal setting of user {e.CallbackQuery.From.FirstName}.\nAuto-wish status: {statusText}.",
                        replyMarkup: keyboard);

                    // Answer the callback query
                    await Bot.AnswerCallbackQueryAsync(
                        callbackQueryId: e.CallbackQuery.Id,
                        text: $"Auto-wish setting changed: {statusText}");
                }
            }

            // Handler trade_accept and trade_decline
            else if (callbackQuery.Data.StartsWith("t_a_") || callbackQuery.Data.StartsWith("t_d_"))
            {
                string message;
                bool isAccepted;

                // Call method for handling and process trade response
                if (Trade.HandleTradeResponse(callbackQuery.Data, callbackQuery.From.Id, out message, out isAccepted))
                {
                    if (isAccepted)
                    {
                        // Message about success
                        await Bot.EditMessageTextAsync(
                            callbackQuery.Message.Chat.Id,
                            callbackQuery.Message.MessageId,
                            $"✅ Trade complete! {message}",
                            parseMode: ParseMode.Html,
                            replyMarkup: null);
                    }
                    else
                    {
                        // Message about decline
                        await Bot.EditMessageTextAsync(
                            callbackQuery.Message.Chat.Id,
                            callbackQuery.Message.MessageId,
                            $"❌ Trade declined. {message}",
                            parseMode: ParseMode.Html,
                            replyMarkup: null);
                    }

                    // Answer нcallback
                    await Bot.AnswerCallbackQueryAsync(
                        callbackQuery.Id,
                        message);
                }
                else
                {
                    await Bot.AnswerCallbackQueryAsync(
                        callbackQuery.Id,
                        message,
                        true);
                }
            }
        }
    }
}