// SPDX-License-Identifier: MPL-2.0

using System;
using System.IO;
using System.Text.Json;
using System.Data.SQLite;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;
using TelegramUI.Strings.Items;
using static TelegramUI.Startup.Config;
using static TelegramUI.Commands.Language;

namespace TelegramUI.Commands
{
    public static class Trade
    {
        // TradeOffer structure
        private class TradeOffer
        {
            public long InitiatorId { get; set; }          // ID user who want to trade
            public long TargetId { get; set; }             // ID user who targeted for trade
            public string OfferItemId { get; set; }        // ID item that offers
            public int OfferQuantity { get; set; }         // Amount of offered item
            public string RequestItemId { get; set; }      // ID item that requested
            public int RequestQuantity { get; set; }       // Amount of requested items
            public long ChatId { get; set; }               // Chat ID, where trade initiates
            public int MessageId { get; set; }             // To know which message edit later
            public DateTime CreatedAt { get; set; }        // Time of creation
        }

        // Saves current trades
        private static readonly ConcurrentDictionary<string, TradeOffer> ActiveTrades = 
            new ConcurrentDictionary<string, TradeOffer>();

        // Class that returns result of InitiateTrade
        internal class TradeInitiationResult
        {
            public bool Success { get; set; }
            public string ErrorMessage { get; set; }
            public InlineKeyboardMarkup ReplyMarkup { get; set; }
            public string TradeKey { get; set; } // Додано для простоти передачі ключа
            // Offered and Requested items names
            public string OfferItemName { get; set; }
            public string RequestItemName { get; set; }
            // Offered and Requested items quantity
            public int OfferItemQuantity { get; set; }
            public int RequestItemQuantity { get; set; }
        }

        internal static async Task<TradeInitiationResult> InitiateTrade(Message message)
        {
            var result = new TradeInitiationResult
            {
                Success = false,
                ErrorMessage = string.Empty,
                ReplyMarkup = null
            };

            if (message.ReplyToMessage == null)
            {
                result.ErrorMessage = "Are you kidding?";
                return result;
            }

            // Split for attributes
            // Message format: /trade [quantity1] [itemId1] [quantity2] [itemId2]
            var parts = message.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 3)
            {
                result.ErrorMessage = "Invalid format. Use: /trade [quantity1] [itemId1] [quantity2] [itemId2]";
                return result;
            }
            
            string language = GetLanguage(message);
            // Gather all after /trade in one string and trim by digits and words
            var rawInput = message.Text.Substring(parts[0].Length).Trim();
            
            int offerQuantity = 1;  // Default quantity
            int requestQuantity = 1;
            
            // Try to get first quantity
            var tokens = rawInput.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            int tokenIndex = 0;

            if (int.TryParse(tokens[0], out int parsedOffer))
            {
                offerQuantity = parsedOffer;
                tokenIndex = 1;
            }
            
            // Find where ending offerQuery - by digit or existing item
            // Strategy: try gradually add more tokens for offer
            string bestOfferId = null, bestOfferName = null;
            int splitPoint = -1;

            for (int end = tokenIndex + 1; end <= tokens.Count; end++)
            {
                string candidate = string.Join(" ", tokens.GetRange(tokenIndex, end - tokenIndex));
                var query = Inventory.FindItemByQuery(candidate, language);
                if (query.HasValue && query.Value.found == 1)
                {
                    bestOfferId = query.Value.id;
                    bestOfferName = query.Value.name;
                    splitPoint = end;
                }
            }
            
            if (bestOfferId == null)
            {
                result.ErrorMessage = "❌ Could not find the first item. Try `/get_id` or use the exact name.";
                return result;
            }
            
            // After splitPoint - quantity and second item
            int remainder = tokenIndex;
            remainder = splitPoint;

            if (remainder < tokens.Count && int.TryParse(tokens[remainder], out int parsedReq))
            {
                requestQuantity = parsedReq;
                remainder++;
            }

            string requestCandidate = string.Join(" ", tokens.GetRange(remainder, tokens.Count - remainder));
            var requestResult = Inventory.FindItemByQuery(requestCandidate, language);

            if (!requestResult.HasValue || requestResult.Value.found != 1)
            {
                string hint = requestResult.HasValue
                    ? $"Did you mean: {requestResult.Value.name}?"
                    : "Item not found.";
                result.ErrorMessage = $"❌ Could not identify the second item. {hint}";
                return result;
            }
            
            string requestItemId = requestResult.Value.id;
            string offerItemId = bestOfferId;
            
            // Check if items exists
            if (!ItemExists(offerItemId) || !ItemExists(requestItemId))
            {
                result.ErrorMessage = "One or both items don't exist.";
                return result;
            }

            var initiatorId = message.From.Id;
            var targetId = message.ReplyToMessage.From.Id;
            var chatId = message.Chat.Id;

            // Check if user trades with himself
            if (initiatorId == targetId)
            {
                result.ErrorMessage = "You cannot trade with yourself.";
                return result;
            }

            // Check if user have enough items for offer
            if (!HasItem(initiatorId, chatId, offerItemId, offerQuantity))
            {
                result.ErrorMessage = $"You don't have enough {GetItemName(offerItemId, GetLanguage(message))}.";
                return result;
            }

            // Check requested item quantity
            if (!HasItem(targetId, chatId, requestItemId, requestQuantity))
            {
                result.ErrorMessage = $"The user doesn't have enough {GetItemName(requestItemId, GetLanguage(message))}.";
                return result;
            }

            result.OfferItemName = bestOfferName;
            result.OfferItemQuantity = offerQuantity;
            result.RequestItemName = requestResult.Value.name;
            result.RequestItemQuantity = requestQuantity;
            
            // Create unique authenticator key
            string guid = Guid.NewGuid().ToString("N").Substring(0, 8); // Short authenticator (8 символів)
            string tradeKey = $"{initiatorId}_{targetId}_{guid}";
            result.TradeKey = tradeKey;

            // Save trade info
            var tradeOffer = new TradeOffer
            {
                InitiatorId = initiatorId,
                TargetId = targetId,
                OfferItemId = offerItemId,
                OfferQuantity = offerQuantity,
                RequestItemId = requestItemId,
                RequestQuantity = requestQuantity,
                ChatId = chatId,
                CreatedAt = DateTime.UtcNow
                // MessageId will be set later
            };

            ActiveTrades.TryAdd(tradeKey, tradeOffer); 

            // Create inline buttons
            result.ReplyMarkup = new InlineKeyboardMarkup(new[]
            {
                new []
                {
                    InlineKeyboardButton.WithCallbackData("✅ Accept", $"t_a_{tradeKey}"), // "t_a_" instead of "trade_accept_"
                    InlineKeyboardButton.WithCallbackData("❌ Decline", $"t_d_{tradeKey}") // "t_d_" intead of "trade_decline_"
                }
            });

            result.Success = true;
            return result;
        }

        // Method for update MessageId and start timer
        internal static async Task UpdateMessageIdAndStartTimer(
            string tradeKey, int messageId, string initiatorName, string targetName)
        {
            if (ActiveTrades.ContainsKey(tradeKey))
            {
                var trade = ActiveTrades[tradeKey];
                trade.MessageId = messageId;
                ActiveTrades[tradeKey] = trade;

                // Sending tradeKey so timer can check trade status
                _ = StartTradeTimeoutAsync(tradeKey, trade.ChatId, messageId, initiatorName, targetName);
            }
        }

        // Trade handler (accept or deny)
        internal static bool HandleTradeResponse(string callbackData, long userId, out string message,
            out bool isAccepted)
        {
            message = string.Empty;
            isAccepted = false;

            var parts = callbackData.Split('_');
            if (parts.Length < 3)
            {
                message = "Invalid callback data format.";
                return false;
            }

            // t_a_ for accept and  t_d_ for  decline
            string action = parts[1]; // will be "a" or "d"

            // Get trade key (all after "t_a_" or "t_d_")
            string tradeKey = string.Join("_", parts, 2, parts.Length - 2);

            //Console.WriteLine($"Action: {action}, TradeKey: {tradeKey}"); // For debug

            // Check if trade exist by key
            if (!ActiveTrades.ContainsKey(tradeKey))
            {
                message = "This trade offer is no longer available.";
                return false;
            }

            var trade = ActiveTrades[tradeKey];

            // Check if user have right to answer on offer
            if (userId != trade.TargetId && userId != trade.InitiatorId)
            {
                message = "This trade offer is not for you.";
                return false;
            }

            if (action == "d") // Decline
            {
                ActiveTrades.TryRemove(tradeKey, out _);
                
                if (userId == trade.InitiatorId)
                    message = "You cancelled your trade offer.";
                else
                    message = "You declined the trade.";
                
                return true;
            }
            else if (action == "a") // Accept
            {
                if (userId != trade.TargetId)
                {
                    message = "Only the target user can accept this trade.";
                    return false;
                }
                // Check if have all needed items
                if (!HasItem(trade.InitiatorId, trade.ChatId, trade.OfferItemId, trade.OfferQuantity))
                {
                    ActiveTrades.TryRemove(tradeKey, out _);     
                    message = "The trade initiator no longer has the offered item.";
                    return false;
                }

                if (!HasItem(trade.TargetId, trade.ChatId, trade.RequestItemId, trade.RequestQuantity))
                {
                    ActiveTrades.TryRemove(tradeKey, out _);     
                    message = "You no longer have the requested item.";
                    return false;
                }

                // Do trade
                if (ExecuteTrade(trade))
                {
                    isAccepted = true;
                    ActiveTrades.TryRemove(tradeKey, out _);     
                    message = "Trade successful!";
                    return true;
                }
                else
                {
                    ActiveTrades.TryRemove(tradeKey, out _);     
                    message = "There was an error executing the trade.";
                    return false;
                }
            }

            return false;
        }

        
        private static bool ExecuteTrade(TradeOffer trade)
        {
            using var con = new SQLiteConnection(MainDb());
            con.Open();

            using var transaction = con.BeginTransaction();

            try
            {
                // Delete offered item from initiator
                UpdateItemCount(con, trade.InitiatorId, trade.ChatId, trade.OfferItemId, -trade.OfferQuantity);

                // Delete requested item from target
                UpdateItemCount(con, trade.TargetId, trade.ChatId, trade.RequestItemId, -trade.RequestQuantity);

                // Add offered item to target
                UpdateItemCount(con, trade.TargetId, trade.ChatId, trade.OfferItemId, trade.OfferQuantity);

                // Add requested item to initiator
                UpdateItemCount(con, trade.InitiatorId, trade.ChatId, trade.RequestItemId, trade.RequestQuantity);

                transaction.Commit();
                return true;
            }
            catch (Exception)
            {
                transaction.Rollback();
                return false;
            }
            finally
            {
                con.Close();
            }
        }

        private static void UpdateItemCount(SQLiteConnection connection, long userId, long chatId, string itemId, int delta)
        {
            using var cmd = new SQLiteCommand(connection);
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@chatId", chatId);
            cmd.Parameters.AddWithValue("@itemId", itemId);
            cmd.Parameters.AddWithValue("@delta", delta);

            string itemType = GetItemType(itemId);
            cmd.Parameters.AddWithValue("@type", itemType);

            if (delta > 0)
            {
                // Check if entry exist
                cmd.CommandText = "SELECT COUNT(*) FROM InventoryItems WHERE UserId = @userId AND ChatId = @chatId AND ItemId = @itemId";
                int count = Convert.ToInt32(cmd.ExecuteScalar());

                if (count == 0)
                {
                    // Insert new
                    cmd.CommandText = "INSERT INTO InventoryItems(UserId, ChatId, ItemId, Type, Count) VALUES(@userId, @chatId, @itemId, @type, @delta)";
                }
                else
                {
                    // Update existing
                    cmd.CommandText = "UPDATE InventoryItems SET Count = Count + @delta WHERE UserId = @userId AND ChatId = @chatId AND ItemId = @itemId";
                }
            }
            else
            {
                // Update existing (substract)
                cmd.CommandText = "UPDATE InventoryItems SET Count = Count + @delta WHERE UserId = @userId AND ChatId = @chatId AND ItemId = @itemId";
                cmd.ExecuteNonQuery();

                // Delete entry if quantity <=0
                cmd.CommandText = "DELETE FROM InventoryItems WHERE UserId = @userId AND ChatId = @chatId AND ItemId = @itemId AND Count <= 0";
            }

            cmd.ExecuteNonQuery();
        }

        private static bool HasItem(long userId, long chatId, string itemId, int quantity)
        {
            using var con = new SQLiteConnection(MainDb());
            con.Open();

            using var cmd = new SQLiteCommand(con);
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@chatId", chatId);
            cmd.Parameters.AddWithValue("@itemId", itemId);
            cmd.Parameters.AddWithValue("@quantity", quantity);

            cmd.CommandText = "SELECT COUNT(*) FROM InventoryItems WHERE UserId = @userId AND ChatId = @chatId AND ItemId = @itemId AND Count >= @quantity";
            int count = Convert.ToInt32(cmd.ExecuteScalar());

            con.Close();
            return count > 0;
        }

        private static bool ItemExists(string itemId)
        {
            var items = typeof(Wish).Assembly.GetManifestResourceStream($"TelegramUI.Strings.Items.en.json");
            var sR = new StreamReader(items);
            var itemsText = sR.ReadToEnd();
            sR.Close();

            var itemsList = JsonSerializer.Deserialize<List<Items>>(itemsText);
            return itemsList.Exists(x => x.Id.ToLower() == itemId.ToLower());
        }

        private static string GetItemName(string itemId, string language)
        {
            var items = typeof(Wish).Assembly.GetManifestResourceStream($"TelegramUI.Strings.Items.{language}.json");
            var sR = new StreamReader(items);
            var itemsText = sR.ReadToEnd();
            sR.Close();

            var itemsList = JsonSerializer.Deserialize<List<Items>>(itemsText);
            var item = itemsList.Find(x => x.Id.ToLower() == itemId.ToLower());

            return item?.Name ?? itemId;
        }

        private static string GetItemType(string itemId)
        {
            var items = typeof(Wish).Assembly.GetManifestResourceStream($"TelegramUI.Strings.Items.en.json");
            var sR = new StreamReader(items);
            var itemsText = sR.ReadToEnd();
            sR.Close();

            var itemsList = JsonSerializer.Deserialize<List<Items>>(itemsText);
            var item = itemsList.Find(x => x.Id.ToLower() == itemId.ToLower());

            return item?.TypeId ?? "weapon";
        }

        public static async Task StartTradeTimeoutAsync(
            string tradeKey, long chatId, int messageId, string userFromName, string userToName)
        {
            // Wait 15 minutes (trade active)
            await Task.Delay(TimeSpan.FromMinutes(15));
            
            // If trade ended (accept/decline) — ActiveTrades don't have key. End timer
            if (!ActiveTrades.ContainsKey(tradeKey))
                return;

            // Trade still active - time up, edit message
            ActiveTrades.TryRemove(tradeKey, out _);

            try
            {
                // Edit message
                await Bot.EditMessageTextAsync(
                    chatId, messageId,
                    $"⏱ Time's up! Offer between " +
                    $"<b>{HttpUtility.HtmlEncode(userFromName)}</b> and " +
                    $"<b>{HttpUtility.HtmlEncode(userToName)}</b> has expired.",
                    parseMode: ParseMode.Html);
                
                // Delete buttons
                await Bot.EditMessageReplyMarkupAsync(chatId, messageId, replyMarkup: null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Trade timeout error: {ex.Message}");
            }
        }
    }
}