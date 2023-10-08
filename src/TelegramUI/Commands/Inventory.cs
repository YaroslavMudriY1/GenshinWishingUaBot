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
       // private static int lastFiveStarPity = 0;
       // private static int lastFourStarPity = 0;
       
        internal static string InventoryFetch(Message message)
        {
            var result = new string[3];
            var resultArray = new string[3];
            var itemStarCount = new int[3];
            
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
            cmd.Parameters.Add(new SQLiteParameter("@userId", message.From.Id));
            cmd.Parameters.Add(new SQLiteParameter("@chatId", message.Chat.Id));
            
            // Getting user's inventory IDs and count
            cmd.CommandText = "SELECT ItemId, Count FROM InventoryItems WHERE UserId = @userId AND ChatId = @chatId";
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                itemIds.Add(rdr.GetString(0));
                countIds.Add(rdr.GetInt32(1));
            }
            
            using var cmd2 = new SQLiteCommand(con);

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
                        switch (item.TypeId)
                        {
                            case "character":
                                resultCharacters[0] += $"{item.Name} x{countIds[i]}, ";
                                itemStarCount[0]+=countIds[i];
                                break;
                            case "weapon":
                                resultWeapons[0] += $"{item.Name} x{countIds[i]}, ";
                                itemStarCount[0]+=countIds[i];
                                break;
                        }
                        break;
                    case 4:
                        switch (item.TypeId)
                        {
                            case "character":
                                resultCharacters[1] += $"{item.Name} x{countIds[i]}, ";
                                itemStarCount[1] += countIds[i];
                                break;
                            case "weapon":
                                resultWeapons[1] += $"{item.Name} x{countIds[i]}, ";
                                itemStarCount[1] += countIds[i];
                                break;
                        }
                        break;
                    case 3:
                        switch (item.TypeId)
                        {
                            case "character":
                                resultCharacters[2] += $"{item.Name} x{countIds[i]}, ";
                                itemStarCount[2] += countIds[i];
                                break;
                            case "weapon":
                                resultWeapons[2] += $"{item.Name} x{countIds[i]}, ";
                                itemStarCount[2] += countIds[i];
                                break;
                        }
                        break;
                }
            } 
            
            //con.Close();

            result[0] = resultCharacters[0] + resultWeapons[0];
            result[1] = resultCharacters[1] + resultWeapons[1];
            result[2] = resultCharacters[2] + resultWeapons[2];

           if (result[0] != "")
            {
                resultArray[0] = $"\U00002b50\U00002b50\U00002b50\U00002b50\U00002b50 ({itemStarCount[0]})\n{result[0]}";
                resultArray[0] = resultArray[0].Substring(0, resultArray[0].Length - 2) + "\n\n";
                //resultArray[0] += $"Last 5* Pity: {lastFiveStarPity}\n";
            }
            if (result[1] != "")
            {
                resultArray[1] = $"\U00002b50\U00002b50\U00002b50\U00002b50 ({itemStarCount[1]})\n{result[1]}";
                resultArray[1] = resultArray[1].Substring(0, resultArray[1].Length - 2) + "\n\n";
                //resultArray[1] += $"Last 4* Pity: {lastFourStarPity}\n";
            }
            if (result[2] != "")
            {
                resultArray[2] = $"\U00002b50\U00002b50\U00002b50 ({itemStarCount[2]})\n{result[2]}";
                resultArray[2] = resultArray[2].Substring(0, resultArray[2].Length - 2) + "\n\n";
            }
            // Check if the user hit pity counter
using var dbCon = new SQLiteConnection(MainDb());
dbCon.Open();

using var dbCmd = new SQLiteCommand(dbCon);
dbCmd.Parameters.Add(new SQLiteParameter("@user", message.From.Id));
dbCmd.Parameters.Add(new SQLiteParameter("@chat", message.Chat.Id));

dbCmd.CommandText = "SELECT FourPity, FivePity From UsersInChats WHERE UserId = @user AND ChatId = @chat";
using var dbRdr = dbCmd.ExecuteReader();

// Зберігаємо значення Pity в локальні змінні
int lastFiveStarPity = 0;
int lastFourStarPity = 0;

while (dbRdr.Read())
{
    lastFiveStarPity = dbRdr.GetInt32(1);
    lastFourStarPity = dbRdr.GetInt32(0);
}

// Закриваємо з'єднання з базою даних
dbCon.Close();

            // Виводимо значення Pity
resultArray[2] += $"Last 5* Pity: {lastFiveStarPity}\n";
resultArray[2] += $"Last 4* Pity: {lastFourStarPity}\n";

            var results = resultArray[0] + resultArray[1] + resultArray[2];
            
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