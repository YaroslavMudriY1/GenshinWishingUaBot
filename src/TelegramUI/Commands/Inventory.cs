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

namespace TelegramUI.Commands
{
    public static class Inventory
    {
       
        internal static string InventoryFetch(Message message)
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

            using var con = new SQLiteConnection(MainDb());
            con.Open();
            
            using var cmd = new SQLiteCommand(con);
            {
                cmd.Parameters.Add(new SQLiteParameter("@userId", message.From.Id));
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

            //using var cmd2 = new SQLiteCommand(con);

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
            
            //con.Close();

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

            //Subtotal 
            /*            resultArray[3] = $"Total:\n\n5★ Characters: {characterCount[0]}\n5★ Weapon: {weaponCount[0]}\n"
                    + $"4★ Characters: {characterCount[1]}\n4★ Weapon: {weaponCount[1]}\n"
                    + $"3★ Weapon: {weaponCount[2]}\n";*/
            
            int totalWishes = 0;

            using (var cmd2 = new SQLiteCommand(con))
            {
                cmd2.Parameters.AddWithValue("@userId", message.From.Id);
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
            resultArray[3] = string.Format(generalList[5], characterCount[0], weaponCount[0], characterCount[1], weaponCount[1],totalWishes);

            // Save Pity as variable
            int lastFiveStarPity = 0;
            int lastFourStarPity = 0;

            //Check if User hit Pity variable
            using (var cmd3 = new SQLiteCommand(con))
            {
                cmd3.Parameters.AddWithValue("@userId", message.From.Id);
                cmd3.Parameters.AddWithValue("@chatId", message.Chat.Id);
                cmd3.CommandText = "SELECT FourPity, FivePity FROM UsersInChats WHERE UserId = @userId AND ChatId = @chatId";

                using (var rdr = cmd3.ExecuteReader())
                {
                    if (rdr.Read())
                    {
                        lastFourStarPity = rdr.GetInt32(0);
                        lastFiveStarPity = rdr.GetInt32(1);
                    }
                }
            }

            // Get Starglitter balance
            int starglitter = 0;
            using (var cmd4 = new SQLiteCommand(con))
            {
                cmd4.Parameters.AddWithValue("@userId", message.From.Id);
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

            con.Close();

            /*
                        // Message output Pity
                        resultArray[4] += $"Last 5⭐️ Pity: {lastFiveStarPity}\n";
                        resultArray[4] += $"Last 4⭐️ Pity: {lastFourStarPity}\n";

                        // Message output Starglitter
                        resultArray[4] += $"\nStarglitter: {starglitter} ✨\n";*/

            //Used strings
            resultArray[4]= string.Format(generalList[6], lastFiveStarPity, lastFourStarPity, starglitter);

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
        
    }
}