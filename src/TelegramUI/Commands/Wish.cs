// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Web;
using Telegram.Bot.Types;
using TelegramUI.Strings.Items;
using static TelegramUI.Startup.Config;
using static TelegramUI.Commands.Language;
using static System.Net.WebRequestMethods;

namespace TelegramUI.Commands
{
    public static class Wish
    {
        private static int Randomizer()
        {
            var rnd = new Random(Guid.NewGuid().GetHashCode()).Next(1, 1000);
            return rnd switch
            {
                < 17 => 5,  //5* characters or weapon (1.725%)
                < 145 => 4, //4* characters or weapon (14.5%)
                _ => 3      //3* weapon
            };
        }

        private const int FourStarPityThreshold = 6;
        private const int FiveStarPityThreshold = 30;

        internal static string[] GetCharacterPull(Message message)
        {
            var result = new string[2];
            var rate = Randomizer();
            
            using var con = new SQLiteConnection(MainDb());
            con.Open();
            
            using var cmd = new SQLiteCommand(con);
            cmd.Parameters.Add(new SQLiteParameter("@user", message.From.Id));
            cmd.Parameters.Add(new SQLiteParameter("@chat", message.Chat.Id));
            

            // Check if the user hit pity counter
            cmd.CommandText = "SELECT FourPity, FivePity From UsersInChats WHERE UserId = @user AND ChatId = @chat";
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                if (rdr.GetInt32(0) >= FourStarPityThreshold)
                {
                    rate = 4;
                }
                if (rdr.GetInt32(1) >= FiveStarPityThreshold)
                {
                    rate = 5;
                }
            }
            
            var items = typeof(Wish).Assembly.GetManifestResourceStream($"TelegramUI.Strings.Items.{GetLanguage(message)}.json");
            var sR = new StreamReader(items);
            var itemsText = sR.ReadToEnd();
            sR.Close();
            
            var itemsList = JsonSerializer.Deserialize<List<Items>>(itemsText);
            
            var filteredList = itemsList.Where(x => x.Stars == rate).ToList();
            var rnd = new Random(Guid.NewGuid().GetHashCode()).Next(filteredList.Count);
            var wish = filteredList[rnd];

            using var cmd3 = new SQLiteCommand(con);
            cmd3.Parameters.Add(new SQLiteParameter("@wish", wish.Id));
            cmd3.Parameters.Add(new SQLiteParameter("@user", message.From.Id));
            cmd3.Parameters.Add(new SQLiteParameter("@chat", message.Chat.Id));
            
            // Adding user to a DB if it doesn't exist
            cmd3.CommandText = "INSERT OR IGNORE INTO UsersInChats(UserId, ChatId) VALUES(@user, @chat)";
            cmd3.ExecuteNonQuery();
            
            // Update that user has rolled in the chat today
            cmd3.CommandText = "UPDATE UsersInChats SET HasRolled = 1 WHERE UserId = @user AND ChatId = @chat";
            cmd3.ExecuteNonQuery();
            
            // Adding the item to the user's inventory
            cmd3.CommandText = "INSERT OR IGNORE INTO InventoryItems(UserId, ChatId, ItemId) VALUES(@user, @chat, @wish)";
            cmd3.ExecuteNonQuery();
            
            // Updating item count in user's inventory
            cmd3.CommandText = "UPDATE InventoryItems SET Count = Count + 1 WHERE UserId = @user AND ChatId = @chat AND ItemId = @wish";
            cmd3.ExecuteNonQuery();

            // Increment the pity counter based on wish star result
            switch (wish.Stars)
            {
                case 3:
                    cmd3.CommandText = "UPDATE UsersInChats SET FourPity = FourPity + 1, FivePity = FivePity + 1 WHERE UserId = @user AND ChatId = @chat";
                    cmd3.ExecuteNonQuery();
                    break;
                case 4:
                    cmd3.CommandText = "UPDATE UsersInChats SET FourPity = 0, FivePity = FivePity + 1 WHERE UserId = @user AND ChatId = @chat";
                    cmd3.ExecuteNonQuery();
                    break;
                case 5:
                    cmd3.CommandText = "UPDATE UsersInChats SET FivePity = 0, FourPity = FourPity + 1 WHERE UserId = @user AND ChatId = @chat";
                    cmd3.ExecuteNonQuery();
                    break;
            }

            // Отримуємо поточну кількість предметів
            cmd3.CommandText = "SELECT Count FROM InventoryItems WHERE UserId = @user AND ChatId = @chat AND ItemId = @wish";
            using var rdr2 = cmd3.ExecuteReader();
            int itemCount = 0;
            if (rdr2.Read())
            {
                itemCount = rdr2.GetInt32(0);
            }

            rdr2.Close();

            // Нарахування Starglitter
            int starglitterReward = 0;
            if (itemCount == 1) // Перша копія предмета
            {
                starglitterReward = wish.Stars switch
                {
                    5 => 10, // 10 Starglitter за перший 5*
                    4 => 3,  // 3 Starglitter за перший 4*
                    _ => 0
                };
            }
            else if (itemCount >= 2 && itemCount <= 6) // Дублікати від 2 до 6
            {
                starglitterReward = wish.Stars switch
                {
                    5 => 10, // 10 Starglitter за 5*
                    4 => 2, // 2 Starglitter за 4*
                    _ => 0
                };
            }
            else if (itemCount > 6) // Дублікати більше 6
            {
                starglitterReward = wish.Stars switch
                {
                    5 => 25, // 25 Starglitter за 5*
                    4 => 5,  // 5 Starglitter за 4*
                    _ => 0
                };
            }

            if (starglitterReward > 0)
            {
                cmd3.CommandText = "UPDATE UsersInChats SET Starglitter = Starglitter + @starglitter WHERE UserId = @user AND ChatId = @chat";
                cmd3.Parameters.Add(new SQLiteParameter("@starglitter", starglitterReward));
                cmd3.ExecuteNonQuery();
            }


            con.Close();
            
            var texts = typeof(Wish).Assembly.GetManifestResourceStream($"TelegramUI.Strings.General.{GetLanguage(message)}.json");
            var sReader = new StreamReader(texts);
            var textsText = sReader.ReadToEnd();
            sReader.Close();
            var textsList = JsonSerializer.Deserialize<List<string>>(textsText);
            
            //Output result as message
            result[0] = string.Format(textsList[0], wish.Description, HttpUtility.HtmlEncode(message.From.FirstName), wish.Name, wish.Title, wish.Stars, wish.Type, wish.TypeDesc, wish.Region);

            //Choosing skin|asset 
            Random wishSkin = new Random();

            string baseUrl= "https://raw.githubusercontent.com/YaroslavMudriY1/GenshinWishingUaBot/main/assets/images/";
           
            result[1] = $"{baseUrl}{wish.Id}.webp";

            if (wish.Id is "jean" or "mona" or "amber" or "rosaria")
            {
                if (wishSkin.Next(2) == 0) // 50% chance of alternate skin
                {
                    result[1] = $"{baseUrl}{wish.Id}-alternate.webp";
                }
            }

            if (wish.Id is "barbara" or "jean" or "klee" or "kaeya" or "fischl" or "kirara" or "nilou")
            {
                if (wishSkin.Next(3) == 0) // 33% chance for alternate skin|asset
                {
                    result[1] = $"{baseUrl}{wish.Id}-summer.webp"; //summer events skins
                }
            }
            
            if (wish.Id is "keqing" or "ningguang" or "shenhe" or "ganyu" or "xingqiu" or "hutao" or "xiangling")
            {
                if (wishSkin.Next(3) == 0) //33% chance
                {
                    result[1] = $"{baseUrl}{wish.Id}-lanternrite.webp"; //lanter rite 2.4, 4.4 and 5.3 skins
                }
            }

             if (wish.Id is "ayaka" or "lisa")
            {
                if (wishSkin.Next(3) == 0) //33% chance
                {
                    result[1] = $"{baseUrl}{wish.Id}-skin.webp"; //other skins
                }
            }

            if (wish.Id is "diluc")
            {
                if (wishSkin.Next(5) == 0) //20% chance
                {
                    result[1] = $"{baseUrl}{wish.Id}-skin.webp"; //5* skins
                }
            }


            return result;
        }
        
        //Check if user already rolled today
        internal static int HasRolled(Message message)
        {
            var result = 0; //fallback value
            
            using var con = new SQLiteConnection(MainDb());
            con.Open();
            
            using var cmd = new SQLiteCommand(con);
            cmd.Parameters.Add(new SQLiteParameter("@user", message.From.Id));
            cmd.Parameters.Add(new SQLiteParameter("@chat", message.Chat.Id));
            
            cmd.CommandText = "SELECT HasRolled FROM UsersInChats WHERE UserId = @user AND ChatId = @chat";
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                result = rdr.GetInt32(0);
            }
            
            con.Close();
            
            return result;
        }

        //Check user Starglitter balance
        internal static int GetStarglitter(long userId)
        {
            int balance = 0;

            using var con = new SQLiteConnection(MainDb());
            con.Open();

            using var cmd = new SQLiteCommand(con);
            cmd.Parameters.Add(new SQLiteParameter("@user", userId));
            cmd.CommandText = "SELECT Starglitter FROM UsersInChats WHERE UserId = @user";

            using var rdr = cmd.ExecuteReader();
            if (rdr.Read())
            {
                balance = rdr.GetInt32(0);
            }

            con.Close();

            return balance;
        }

        //If user have 10+ Starglitter, use it for wish
        internal static void UseStarglitter(long userId, int amount)
        {
            using var con = new SQLiteConnection(MainDb());
            con.Open();

            using var cmd = new SQLiteCommand(con);
            cmd.Parameters.Add(new SQLiteParameter("@user", userId));
            cmd.Parameters.Add(new SQLiteParameter("@amount", amount));

            cmd.CommandText = "UPDATE UsersInChats SET Starglitter = Starglitter - @amount WHERE UserId = @user AND Starglitter >= @amount";
            cmd.ExecuteNonQuery();

            con.Close();
        }

    }
}
