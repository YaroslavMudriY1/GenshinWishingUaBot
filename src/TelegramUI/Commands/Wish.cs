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
using System.Globalization;
using System.Text;

namespace TelegramUI.Commands
{
    public static class Wish
    {
        //Type randomizer (character or weapon)
        private static int RandomizerType()
        {
            return new Random(Guid.NewGuid().GetHashCode()).Next(0, 2); // 0 - Character, 1 - Weapon
        }

        //Chars rarity (stars) randomizer
        private static int RandomizerCharChance()
        {
            var rnd = new Random(Guid.NewGuid().GetHashCode()).Next(1, 1000);
            return rnd switch
            {
                < 16 => 5,  // 5* characters (1.6%)
                < 130 => 4, // 4* characters (13%)
                _ => 3     // 3* weapon (85.4%)
            };
        }

        //Weapon rarity (stars) randomizer
        private static int RandomizerWeaponChance()
        {
            var rnd = new Random(Guid.NewGuid().GetHashCode()).Next(1, 1000);
            return rnd switch
            {
                < 18 => 5,  // 5* weappon (1.8%)
                < 145 => 4,  // 4* weapon (14.5%)
                _ => 3      // 3* weapon (83.7%)
            };
        }

        // 50|50 randomizer for event 5*
        private static bool FiftyFiftyRandomizer()
        {
            return new Random(Guid.NewGuid().GetHashCode()).Next(0, 2) == 0; // 50% шанс виграшу
        }

        private const int FourStarPityThreshold = 6; // One four star every 7 wishes | 7th wish is guaranteed 4* or higher
        private const int FiveStarPityThreshold = 30; // One five star every 31 wishes | 31th wish is guaranteed 5*

        internal static string[] GetCharacterPull(Message message, bool oneWish)
        {

                var result = new string[2]; //If one wish return wish description and htlm.preview of image

                var result10 = new string[4]; //If 10 wishes return name, stars, type, starglitter
            

                var type = RandomizerType(); // Select type (character or weapon)
            int rate = type == 0 ? RandomizerCharChance() : RandomizerWeaponChance(); // Select radomizer type

            //If randomizers give 3* chars, convert to 3* weapon (no 3* chars in list)
            if (type == 0 && rate == 3)
            {
                type = 1;
            }

            using var con = new SQLiteConnection(MainDb());
            con.Open();
            
            using var cmd = new SQLiteCommand(con);
            cmd.Parameters.Add(new SQLiteParameter("@user", message.From.Id));
            cmd.Parameters.Add(new SQLiteParameter("@chat", message.Chat.Id));


            // Check if the user hit pity counter
            cmd.CommandText = "SELECT FourPity, FivePity, FiftyLose FROM UsersInChats WHERE UserId = @user AND ChatId = @chat";
            using var rdr = cmd.ExecuteReader();
            bool FiftyLose = false;

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
                FiftyLose = rdr.GetBoolean(2);
            }

            //Loading list of items
            var items = typeof(Wish).Assembly.GetManifestResourceStream($"TelegramUI.Strings.Items.{GetLanguage(message)}.json");
            var sR = new StreamReader(items);
            var itemsText = sR.ReadToEnd();
            sR.Close();
            
            var itemsList = JsonSerializer.Deserialize<List<Items>>(itemsText);

            bool isEvent = rate == 5 && (!FiftyLose && FiftyFiftyRandomizer() || FiftyLose);

            //List filtration by type and rarity (stars)
            var filteredList = itemsList.Where(x => x.Stars == rate && x.TypeId == (type == 0 ? "character" : "weapon")).ToList();

            if (rate == 5)
            {
                if (isEvent)
                {
                    filteredList = filteredList.Where(x => x.IsEvent == true).ToList(); // Only event 5★
                }
                else
                {
                    filteredList = filteredList.Where(x => x.IsEvent == false).ToList(); // Only standart 5★
                }
            }

            var rnd = new Random(Guid.NewGuid().GetHashCode()).Next(filteredList.Count);
            var wish = filteredList[rnd];

            using var cmd3 = new SQLiteCommand(con);
            cmd3.Parameters.Add(new SQLiteParameter("@wish", wish.Id));
            cmd3.Parameters.Add(new SQLiteParameter("@user", message.From.Id));
            cmd3.Parameters.Add(new SQLiteParameter("@chat", message.Chat.Id));
            cmd3.Parameters.Add(new SQLiteParameter("@type", wish.TypeId));
            cmd3.Parameters.Add(new SQLiteParameter("@isStandard", !isEvent));
            
            //Update 50|50 state
            if (rate == 5)
            {
                cmd3.CommandText = "UPDATE UsersInChats SET FiftyLose = @isStandard WHERE UserId = @user AND ChatId = @chat";
                cmd3.ExecuteNonQuery();
            }

            // Adding user to a DB if it doesn't exist
            cmd3.CommandText = "INSERT OR IGNORE INTO UsersInChats(UserId, ChatId) VALUES(@user, @chat)";
            cmd3.ExecuteNonQuery();

            /*            // Update that user has rolled in the chat today
                        cmd3.CommandText = "UPDATE UsersInChats SET HasRolled = 1 WHERE UserId = @user AND ChatId = @chat";
                        cmd3.ExecuteNonQuery();*/

            // Adding the item to the user's inventory
            cmd3.CommandText = "INSERT OR IGNORE INTO InventoryItems(UserId, ChatId, ItemId, Type) VALUES(@user, @chat, @wish, @type)";
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

            //Total Wishes +1
            cmd3.CommandText = "UPDATE UsersInChats SET TotalWishes = TotalWishes + 1 WHERE UserId = @user AND ChatId = @chat";
            cmd3.ExecuteNonQuery();

            // Get current items count
            cmd3.CommandText = "SELECT Count, Type FROM InventoryItems WHERE UserId = @user AND ChatId = @chat AND ItemId = @wish";
            using var rdr2 = cmd3.ExecuteReader();
            int itemCount = 0;
            string itemType = "weapon";
            if (rdr2.Read())
            {
                itemCount = rdr2.GetInt32(0);
                itemType = rdr2.GetString(1);
            }
            rdr2.Close();

            // Starglitter
            int starglitterReward = 0;
            if (itemCount == 1) // Fisrt copy of item
            {
                starglitterReward = wish.Stars switch
                {
                    5 => 10, // 10 Starglitter for first 5*
                    4 => 3,  // 3 Starglitter for firtst 4*
                    _ => 0
                };
            }
            else if (itemCount >= 2 && ((itemType == "weapon" && itemCount <= 5) || (itemType == "character" && itemCount <= 7)))
            {
                //For weapon 2<x<5 count
                //For characters 2<x<7 count

                starglitterReward = wish.Stars switch
                {
                    5 => 10, // 10 Starglitter for 5*
                    4 => 2, // 2 Starglitter for 4*
                    _ => 0
                };
            }
            else if ((itemType == "weapon" && itemCount > 5) || (itemType == "character" && itemCount > 7))
            {
                //For weapon - if more than R5 count
                //For characters - if more than C6 count
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
            result[0] = string.Format(textsList[0], wish.Description, HttpUtility.HtmlEncode(message.From.FirstName), wish.Name, wish.Title, wish.Stars, wish.Type, wish.TypeDesc, wish.Region, starglitterReward);
            
            result10[0] = wish.Name;
            result10[1] = wish.Stars.ToString();
            result10[2] = wish.TypeId;
            result10[3] = starglitterReward.ToString();

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

            if (oneWish)
            {
                return result;
            }
            else return result10;
            
        }

        //Check if user already rolled today
        internal static int HasRolled(Message message)
        {
            using var con = new SQLiteConnection(MainDb());
            con.Open();
            using var cmd = new SQLiteCommand(con);
            cmd.Parameters.AddWithValue("@user", message.From.Id);
            cmd.Parameters.AddWithValue("@chat", message.Chat.Id);

            // Get last wish time
            cmd.CommandText = "SELECT LastWishTime FROM UsersInChats WHERE UserId = @user AND ChatId = @chat";
            var lastWishTime = cmd.ExecuteScalar();

            if (lastWishTime == DBNull.Value || lastWishTime == null)
            {
                return 0; // User didn't do wish
            }

            // Try parsing the date with the exact format
            var lastWish = DateTime.ParseExact(lastWishTime.ToString(), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var nextWishAvailable = lastWish.AddHours(2); // Timer for two hours

            return DateTime.Now < nextWishAvailable ? 1 : 0;
        }

        //Set wish time in database
        internal static void SetWishTime(long userId, long chatId)
        {
            using var con = new SQLiteConnection(MainDb());
            con.Open();
            using var cmd = new SQLiteCommand(con);
            cmd.Parameters.AddWithValue("@user", userId);
            cmd.Parameters.AddWithValue("@chat", chatId);

            // Save current time in "yyyy-MM-dd HH:mm:ss" format
            var currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            cmd.Parameters.AddWithValue("@time", currentTime);
            cmd.CommandText = "UPDATE UsersInChats SET LastWishTime = @time WHERE UserId = @user AND ChatId = @chat";
            cmd.ExecuteNonQuery();
        }

        //Check user Starglitter balance
        internal static int GetStarglitter(long userId, long chatId)
        {
            int balance = 0;

            using var con = new SQLiteConnection(MainDb());
            con.Open();

            using var cmd = new SQLiteCommand(con);
            cmd.Parameters.Add(new SQLiteParameter("@user", userId));
            cmd.Parameters.Add(new SQLiteParameter("@chat", chatId));
            cmd.CommandText = "SELECT Starglitter FROM UsersInChats WHERE UserId = @user AND ChatId = @chat";

            using var rdr = cmd.ExecuteReader();
            if (rdr.Read())
            {
                balance = rdr.GetInt32(0);
            }

            con.Close();

            return balance;
        }

        //Add starglitter to user
        internal static void AddStarglitter(long userId,long chatId, int amount)
        {

            using var con = new SQLiteConnection(MainDb());
            con.Open();

            using var cmd = new SQLiteCommand(con);
            cmd.Parameters.Add(new SQLiteParameter("@user", userId));
            cmd.Parameters.Add(new SQLiteParameter("@chat", chatId));
            cmd.Parameters.Add(new SQLiteParameter("@amount", amount));

            cmd.CommandText = "UPDATE UsersInChats SET Starglitter = Starglitter + @amount WHERE UserId = @user AND ChatId = @chat";
            cmd.ExecuteNonQuery();

            con.Close();
        }

        //If user have 10+ Starglitter, use it for wish
        internal static void UseStarglitter(long userId, long chatId, int amount)
        {
            using var con = new SQLiteConnection(MainDb());
            con.Open();

            using var cmd = new SQLiteCommand(con);
            cmd.Parameters.Add(new SQLiteParameter("@user", userId));
            cmd.Parameters.Add(new SQLiteParameter("@chat", chatId));
            cmd.Parameters.Add(new SQLiteParameter("@amount", amount));

            cmd.CommandText = "UPDATE UsersInChats SET Starglitter = Starglitter - @amount WHERE UserId = @user AND ChatId=@chat AND Starglitter >= @amount";
            cmd.ExecuteNonQuery();

            con.Close();
        }

        internal static string GetWish10Summary(List<string[]> pulls)
        {
            var groupedPulls = pulls.GroupBy(p => p[1]) // Групуємо за кількістю зірок
                                    .OrderByDescending(g => int.Parse(g.Key)) // Сортуємо від більшого до меншого
                                    .ToDictionary(g => g.Key, g => g.ToList());

            var result = new StringBuilder();

            foreach (var starGroup in groupedPulls)
            {
                int stars = int.Parse(starGroup.Key);
                int count = starGroup.Value.Count;
                string starEmoji = new string('⭐', stars);

                var characters = starGroup.Value.Where(p => p[2] == "character").Select(p => p[0]).ToList();
                var weapons = starGroup.Value.Where(p => p[2] == "weapon").Select(p => p[0]).ToList();

                result.AppendLine($"{starEmoji} ({count})");
                if (characters.Count > 0)
                    result.AppendLine($"Characters: {string.Join(", ", characters)}");
                if (weapons.Count > 0)
                    result.AppendLine($"Weapons: {string.Join(", ", weapons)}");
                result.AppendLine();
            }

            return result.ToString().Trim();
        }


    }
}
