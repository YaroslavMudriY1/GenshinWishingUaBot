// SPDX-License-Identifier: MPL-2.0

using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Web;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramUI.Strings.Items;
using static TelegramUI.Startup.Config;
using static TelegramUI.Commands.Language;
using System.Globalization;
using System;

namespace TelegramUI.Commands
{
    public static class Inventory
    {

        internal static string InventoryFetch(Message message, long? targetUserId = null, string targetUsername = null)
        {
            var result = new string[3];
            var resultArray = new string[5];
            var itemStarCount = new int[3];
            var characterCount = new int[3];
            var weaponCount = new int[3];

            // 5 Stars
            result[0] = "";
            itemStarCount[0] = 0;
            // 4 Stars
            result[1] = "";
            itemStarCount[1] = 0;
            // 3 Stars
            result[2] = "";
            itemStarCount[2] = 0;

            var resultCharacters = new string[3];
            var resultWeapons = new string[3];

            var itemIds = new List<string>();
            var countIds = new List<int>();

            // Use target user ID, if added
            long userIdToCheck = targetUserId ?? message.From.Id;

            using var con = new SQLiteConnection(MainDb());
            con.Open();

            using var cmd = new SQLiteCommand(con);
            {
                cmd.Parameters.Add(new SQLiteParameter("@userId", userIdToCheck));
                cmd.Parameters.Add(new SQLiteParameter("@chatId", message.Chat.Id));

                // Getting user's inventory IDs and count
                cmd.CommandText = "SELECT ItemId, Count FROM InventoryItems WHERE UserId = @userId AND ChatId = @chatId";
                using var rdr = cmd.ExecuteReader();
                {
                    while (rdr.Read())
                    {
                        itemIds.Add(rdr.GetString(0));
                        countIds.Add(rdr.GetInt32(1));
                    }
                }
            }

            // Linking item IDs to actual data
            for (var i = 0; i <= itemIds.Count - 1; i++)
            {
                var id = itemIds[i];

                var items = typeof(Wish).Assembly.GetManifestResourceStream($"TelegramUI.Strings.Items.{GetLanguage(message)}.json");
                var sR = new StreamReader(items);
                var itemsText = sR.ReadToEnd();
                sR.Close();

                var itemsList = JsonSerializer.Deserialize<List<Items>>(itemsText);
                var item = itemsList.Find(x => x.Id.Contains(id));

                switch (item.Stars)
                {
                    case 5:
                        if (item.TypeId == "character")
                        {
                            resultCharacters[0] += $"{item.Name} x{countIds[i]}, ";
                            itemStarCount[0] += countIds[i];
                            characterCount[0] += countIds[i];
                        }
                        else if (item.TypeId == "weapon")
                        {
                            resultWeapons[0] += $"{item.Name} x{countIds[i]}, ";
                            itemStarCount[0] += countIds[i];
                            weaponCount[0] += countIds[i];
                        }
                        break;
                    case 4:
                        if (item.TypeId == "character")
                        {
                            resultCharacters[1] += $"{item.Name} x{countIds[i]}, ";
                            itemStarCount[1] += countIds[i];
                            characterCount[1] += countIds[i];
                        }
                        else if (item.TypeId == "weapon")
                        {
                            resultWeapons[1] += $"{item.Name} x{countIds[i]}, ";
                            itemStarCount[1] += countIds[i];
                            weaponCount[1] += countIds[i];
                        }
                        break;
                    case 3:
                        if (item.TypeId == "weapon")
                        {
                            resultWeapons[2] += $"{item.Name} x{countIds[i]}, ";
                            itemStarCount[2] += countIds[i];
                        }
                        break;
                }
            }

            result[0] = resultCharacters[0] + resultWeapons[0];
            result[1] = resultCharacters[1] + resultWeapons[1];
            result[2] = resultCharacters[2] + resultWeapons[2];

            if (result[0] != "") //5*
            {
                resultArray[0] = $"\U00002b50\U00002b50\U00002b50\U00002b50\U00002b50 ({itemStarCount[0]})\n{result[0]}";
                resultArray[0] = resultArray[0].Substring(0, resultArray[0].Length - 2) + "\n\n";
            }
            if (result[1] != "") //4*
            {
                resultArray[1] = $"\U00002b50\U00002b50\U00002b50\U00002b50 ({itemStarCount[1]})\n{result[1]}";
                resultArray[1] = resultArray[1].Substring(0, resultArray[1].Length - 2) + "\n\n";
            }
            if (result[2] != "") //3*
            {
                resultArray[2] = $"\U00002b50\U00002b50\U00002b50 ({itemStarCount[2]})\n{result[2]}";
                resultArray[2] = resultArray[2].Substring(0, resultArray[2].Length - 2) + "\n\n";
            }

            int totalWishes = 0;

            using (var cmd2 = new SQLiteCommand(con))
            {
                cmd2.Parameters.AddWithValue("@userId", userIdToCheck);
                cmd2.Parameters.AddWithValue("@chatId", message.Chat.Id);
                cmd2.CommandText = "SELECT TotalWishes FROM UsersInChats WHERE UserId = @userId AND ChatId = @chatId";

                using (var rdr = cmd2.ExecuteReader())
                {
                    if (rdr.Read())
                    {
                        totalWishes = rdr.GetInt32(0);
                    }
                }
            }

            var language = GetLanguage(message);
            var generalStrings = typeof(Wish).Assembly.GetManifestResourceStream($"TelegramUI.Strings.General.{language}.json");
            var sRGeneral = new StreamReader(generalStrings);
            var generalText = sRGeneral.ReadToEnd();
            sRGeneral.Close();
            var generalList = JsonSerializer.Deserialize<List<string>>(generalText);
            //Used strings for different languages
            resultArray[3] = string.Format(generalList[5], characterCount[0], weaponCount[0], characterCount[1], weaponCount[1], totalWishes);

            // Save Pity as variable
            int lastFiveStarPity = 0;
            int lastFourStarPity = 0;
            bool isEventPity = false;

            //Check if User hit Pity variable
            using (var cmd3 = new SQLiteCommand(con))
            {
                cmd3.Parameters.AddWithValue("@userId", userIdToCheck);
                cmd3.Parameters.AddWithValue("@chatId", message.Chat.Id);
                cmd3.CommandText = "SELECT FourPity, FivePity, FiftyLose FROM UsersInChats WHERE UserId = @userId AND ChatId = @chatId";

                using (var rdr = cmd3.ExecuteReader())
                {
                    if (rdr.Read())
                    {
                        lastFourStarPity = rdr.GetInt32(0);
                        lastFiveStarPity = rdr.GetInt32(1);
                        isEventPity = rdr.GetBoolean(2);
                    }
                }
            }

            // Get Starglitter balance
            int starglitter = 0;
            using (var cmd4 = new SQLiteCommand(con))
            {
                cmd4.Parameters.AddWithValue("@userId", userIdToCheck);
                cmd4.Parameters.AddWithValue("@chatId", message.Chat.Id);
                cmd4.CommandText = "SELECT Starglitter FROM UsersInChats WHERE UserId = @userId AND ChatId = @chatId";

                using (var rdr = cmd4.ExecuteReader())
                {
                    if (rdr.Read())
                    {
                        starglitter = rdr.GetInt32(0);
                    }
                }
            }

            // Get LastWishTime
            string lastWishTimeFormatted = "N/A"; // If there null in BD, output N/A

            using (var cmd5 = new SQLiteCommand(con))
            {
                cmd5.Parameters.AddWithValue("@userId", userIdToCheck);
                cmd5.Parameters.AddWithValue("@chatId", message.Chat.Id);
                cmd5.CommandText = "SELECT LastWishTime FROM UsersInChats WHERE UserId = @userId AND ChatId = @chatId";

                using (var rdr = cmd5.ExecuteReader())
                {
                    if (rdr.Read() && !rdr.IsDBNull(0))
                    {
                        var lastWishTime = rdr.GetString(0);
                        var parsedTime = DateTime.ParseExact(lastWishTime, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                        lastWishTimeFormatted = parsedTime.ToString("HH:mm dd.MM.yyyy");
                    }
                }
            }

            con.Close();

            string eventPity = "";

            if (language == "en")
            {
                if (isEventPity)
                    eventPity = "yes";
                else if (!isEventPity)
                    eventPity = "no";
            }

            if (language == "ua")
            {
                if (isEventPity)
                    eventPity = "так";
                else if (!isEventPity)
                    eventPity = "ні";
            }

            //Use of Strings/General
            resultArray[4] = string.Format(generalList[6], eventPity, lastFiveStarPity, lastFourStarPity, starglitter, lastWishTimeFormatted);

            var results = resultArray[0] + resultArray[1] + resultArray[2] + resultArray[3] + resultArray[4];

            var texts = typeof(Wish).Assembly.GetManifestResourceStream($"TelegramUI.Strings.General.{GetLanguage(message)}.json");
            var sReader = new StreamReader(texts);
            var textsText = sReader.ReadToEnd();
            sReader.Close();
            var textsList = JsonSerializer.Deserialize<List<string>>(textsText);

            if (results == "")
            {
                results = textsList[3];
            }

            return results;
        }

        internal static string GetItemId(Message message, string itemName)
        {
            string result = "";
            string language = GetLanguage(message);

            try
            {
                // Load items data for current language
                var items = typeof(Wish).Assembly.GetManifestResourceStream($"TelegramUI.Strings.Items.{language}.json");
                var sR = new StreamReader(items);
                var itemsText = sR.ReadToEnd();
                sR.Close();

                var itemsList = JsonSerializer.Deserialize<List<Items>>(itemsText);

                // Find item by name (case insensitive)
                var item = itemsList.Find(x => x.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase));

                // Get the appropriate message string based on language
                var generalStrings = typeof(Wish).Assembly.GetManifestResourceStream($"TelegramUI.Strings.General.{language}.json");
                var sRGeneral = new StreamReader(generalStrings);
                var generalText = sRGeneral.ReadToEnd();
                sRGeneral.Close();
                var textList = JsonSerializer.Deserialize<List<string>>(generalText);

                if (item != null)
                {
                    result = string.Format(textList[10], item.Name, item.Id);
                }
                else
                {
                    result = string.Format(textList[11], itemName);
                }
            }
            catch (Exception ex)
            {
            }

            return result;
        }
        internal static string SellItem(Message message, string itemId, int amount = 1)
        {
            // Default values
            string result = "";
            var userId = message.From.Id;
            var chatId = message.Chat.Id;
            string language = GetLanguage(message);

            try
            {
                // Get language-specific messages
                var generalStrings = typeof(Wish).Assembly.GetManifestResourceStream($"TelegramUI.Strings.General.{language}.json");
                var sRGeneral = new StreamReader(generalStrings);
                var generalText = sRGeneral.ReadToEnd();
                sRGeneral.Close();
                var generalList = JsonSerializer.Deserialize<List<string>>(generalText);

                using var con = new SQLiteConnection(MainDb());
                con.Open();

                // 1. Check if user has the item and enough quantity
                using (var cmd = new SQLiteCommand(con))
                {
                    cmd.Parameters.Add(new SQLiteParameter("@userId", userId));
                    cmd.Parameters.Add(new SQLiteParameter("@chatId", chatId));
                    cmd.Parameters.Add(new SQLiteParameter("@itemId", itemId));

                    cmd.CommandText = "SELECT Count FROM InventoryItems WHERE UserId = @userId AND ChatId = @chatId AND ItemId = @itemId";
                    object countObj = cmd.ExecuteScalar();

                    if (countObj == null || countObj == DBNull.Value || Convert.ToInt32(countObj) < amount)
                    {
                        // User doesn't have the item or not enough quantity
                        con.Close();
                        return string.Format(generalList[14], itemId, amount); // "You don't have enough {0} (need {1})."
                    }
                }

                // 2. Get item stars to calculate starglitter amount
                int stars = 0;
                string itemName = "";

                // Load items data for current language
                var items = typeof(Wish).Assembly.GetManifestResourceStream($"TelegramUI.Strings.Items.{language}.json");
                var sR = new StreamReader(items);
                var itemsText = sR.ReadToEnd();
                sR.Close();

                var itemsList = JsonSerializer.Deserialize<List<Items>>(itemsText);
                var item = itemsList.Find(x => x.Id.Equals(itemId, StringComparison.OrdinalIgnoreCase));

                if (item != null)
                {
                    stars = item.Stars;
                    itemName = item.Name;
                }
                else
                {
                    con.Close();
                    return string.Format(generalList[15], itemId); // "Item with ID {0} not found."
                }

                // 3. Calculate starglitter amount based on rarity
                int starglitterAmount = 0;
                switch (stars)
                {
                    case 5:
                        starglitterAmount = 25 * amount;
                        break;
                    case 4:
                        starglitterAmount = 10 * amount;
                        break;
                    case 3:
                        starglitterAmount = 1 * amount;
                        break;
                    default:
                        starglitterAmount = 0;
                        break;
                }

                // 4. Update inventory: remove item or decrease count
                using (var cmd = new SQLiteCommand(con))
                {
                    cmd.Parameters.Add(new SQLiteParameter("@userId", userId));
                    cmd.Parameters.Add(new SQLiteParameter("@chatId", chatId));
                    cmd.Parameters.Add(new SQLiteParameter("@itemId", itemId));
                    cmd.Parameters.Add(new SQLiteParameter("@amount", amount));

                    cmd.CommandText = "UPDATE InventoryItems SET Count = Count - @amount WHERE UserId = @userId AND ChatId = @chatId AND ItemId = @itemId";
                    cmd.ExecuteNonQuery();

                    // Remove entry if count is 0
                    cmd.CommandText = "DELETE FROM InventoryItems WHERE UserId = @userId AND ChatId = @chatId AND ItemId = @itemId AND Count <= 0";
                    cmd.ExecuteNonQuery();
                }

                // 5. Add starglitter to user balance
                if (starglitterAmount > 0)
                {
                    using (var cmd = new SQLiteCommand(con))
                    {
                        cmd.Parameters.Add(new SQLiteParameter("@userId", userId));
                        cmd.Parameters.Add(new SQLiteParameter("@chatId", chatId));
                        cmd.Parameters.Add(new SQLiteParameter("@amount", starglitterAmount));

                        cmd.CommandText = "UPDATE UsersInChats SET Starglitter = Starglitter + @amount WHERE UserId = @userId AND ChatId = @chatId";
                        cmd.ExecuteNonQuery();
                    }
                }

                con.Close();

                // Get updated starglitter balance
                int newBalance = Wish.GetStarglitter(userId, chatId);

                // Return success message
                return string.Format(generalList[16], amount, itemName, starglitterAmount, newBalance); // "You sold {0} {1} and received {2} starglitter. Your new balance: {3} ✨"
            }
            catch (Exception ex)
            {
                //ignored
                return "Damn! Some eror happened while selling item! Please contact the dev and tell him \"Fix Bugs Please!\".";
            }
        }
    }
}