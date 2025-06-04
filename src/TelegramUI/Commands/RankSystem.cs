using System;
using System.Data;
using System.Data.SQLite;
using Telegram.Bot.Types;
using TelegramUI.Commands;
using static TelegramUI.Startup.Config;

namespace TelegramUI.Commands
{
    public static class RankSystem
    {
        // Constants for experience points based on item star rarity
        private const int EXP_3_STAR = 1;
        private const int EXP_4_STAR = 3;
        private const int EXP_5_STAR = 10;

        // Base experience required for level-up (each new level requires more EXP)
        private const int BASE_EXP_REQUIRED = 15;

        public struct RankUpResult
        {
            public bool LeveledUp { get; set; }
            public int NewLevel { get; set; }
            public int CurrentExp { get; set; }
            public int ExpToNext { get; set; }
            public string RewardMessage { get; set; }
        }

        /// <summary>
        /// Add EXP to user based on item star rarity.
        /// </summary>
        /// <param name="userId">user ID</param>
        /// <param name="chatId">chat ID</param>
        /// <param name="starRarity">item rarity (3, 4, or 5) OR total EXP amount</param>
        /// <returns>Result with level up info</returns>
        public static RankUpResult AddExperience(long userId, long chatId, int starRarity)
        {
            var result = new RankUpResult();

            int expToAdd;

            // Check if this is a direct EXP amount (for wish10) or star rarity (for single wish)
            // If the value is greater than 5, treat it as direct EXP amount
            if (starRarity > 5)
            {
                expToAdd = starRarity; // Direct EXP amount from wish10
            }
            else
            {
                // Convert star rarity to EXP for single wishes
                expToAdd = starRarity switch
                {
                    3 => EXP_3_STAR,
                    4 => EXP_4_STAR,
                    5 => EXP_5_STAR,
                    _ => 0
                };
            }

            if (expToAdd == 0) return result;

            using var con = new SQLiteConnection(MainDb());
            con.Open();

            // Create table if not exists
            CreateRankTableIfNotExists(con);

            // Get user's current rank and experience
            var (currentLevel, currentExp) = GetUserRank(con, userId, chatId);

            // Add exp
            int newExp = currentExp + expToAdd;
            int newLevel = currentLevel;

            // Check if enough experience to level up (may level up multiple times)
            while (true)
            {
                int expRequired = GetExpRequiredForLevel(newLevel + 1);
                if (newExp >= expRequired)
                {
                    newExp -= expRequired;
                    newLevel++;
                    result.LeveledUp = true;
                }
                else
                {
                    break;
                }
            }

            // Update data in DB
            UpdateUserRank(con, userId, chatId, newLevel, newExp);

            // If leveled up, give reward
            if (result.LeveledUp)
            {
                var reward = GiveRankReward(con, userId, chatId, currentLevel, newLevel);
                result.RewardMessage = reward;
            }

            result.NewLevel = newLevel;
            result.CurrentExp = newExp;
            result.ExpToNext = GetExpRequiredForLevel(newLevel + 1) - newExp;

            con.Close();
            return result;
        }

        /// <summary>
        /// Overloaded method specifically for adding direct EXP amount (for wish10)
        /// </summary>
        /// <param name="userId">user ID</param>
        /// <param name="chatId">chat ID</param>
        /// <param name="expAmount">direct EXP amount to add</param>
        /// <returns>Result with level up info</returns>
        public static RankUpResult AddExperienceDirect(long userId, long chatId, int expAmount)
        {
            return AddExperience(userId, chatId, expAmount);
        }


        // Get user rank info from DB
        public static (int level, int exp, int expToNext) GetUserRankInfo(long userId, long chatId)
        {
            using var con = new SQLiteConnection(MainDb());
            con.Open();

            CreateRankTableIfNotExists(con);
            var (level, exp) = GetUserRank(con, userId, chatId);
            int expToNext = GetExpRequiredForLevel(level + 1) - exp;

            con.Close();
            return (level, exp, expToNext);
        }

        // Create table in DB if not exists
        private static void CreateRankTableIfNotExists(SQLiteConnection con)
        {
            using var cmd = new SQLiteCommand(con);
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS UserRanks (
                    UserId INTEGER,
                    ChatId INTEGER,
                    Level INTEGER DEFAULT 1,
                    Experience INTEGER DEFAULT 0,
                    PRIMARY KEY (UserId, ChatId)
                )";
            cmd.ExecuteNonQuery();
        }

        // Get current user level and experience
        private static (int level, int exp) GetUserRank(SQLiteConnection con, long userId, long chatId)
        {
            using var cmd = new SQLiteCommand(con);
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@chatId", chatId);
            cmd.CommandText = "SELECT Level, Experience FROM UserRanks WHERE UserId = @userId AND ChatId = @chatId";

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return (reader.GetInt32("Level"), reader.GetInt32("Experience"));
            }

            // If no entry exists, create a new one
            reader.Close();
            cmd.CommandText = @"
                INSERT OR IGNORE INTO UserRanks (UserId, ChatId, Level, Experience) 
                VALUES (@userId, @chatId, 1, 0)";
            cmd.ExecuteNonQuery();

            return (1, 0);
        }

        // Update user's level and experience
        private static void UpdateUserRank(SQLiteConnection con, long userId, long chatId, int level, int exp)
        {
            using var cmd = new SQLiteCommand(con);
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@chatId", chatId);
            cmd.Parameters.AddWithValue("@level", level);
            cmd.Parameters.AddWithValue("@exp", exp);
            cmd.CommandText = @"
                INSERT OR REPLACE INTO UserRanks (UserId, ChatId, Level, Experience) 
                VALUES (@userId, @chatId, @level, @exp)";
            cmd.ExecuteNonQuery();
        }

        // Calcalates how much experience is required to reach a certain level
        private static int GetExpRequiredForLevel(int level)
        {
            // Proggressive system: each new level requires more experience
            return BASE_EXP_REQUIRED + (level - 1) * 5;
        }

        // Give rank reward based on the level increase
        private static string GiveRankReward(SQLiteConnection con, long userId, long chatId, int oldLevel, int newLevel)
        {
            int starglitterReward = 0;
            string additionalReward = "";

            // Caltulate rewards based on level progression
            for (int level = oldLevel + 1; level <= newLevel; level++)
            {
                if (level % 10 == 0) // Every 10th level
                {
                    starglitterReward += 100 * (level/10);
                    additionalReward += $"🎊 Milestone Level {level}! ";
                }
                else if (level % 5 == 0) // Every 5th level
                {
                    starglitterReward += 50 * (level/5);
                    additionalReward += $"⭐ Level {level} bonus! ";
                }
                else
                {
                    starglitterReward += 10 + (level / 10) * 2; // Base reward + bonus for high level
                }
            }

            // Add Starglitter to user's account
            if (starglitterReward > 0)
            {
                Wish.AddStarglitter(userId, chatId, starglitterReward);
            }

            return $"+{starglitterReward}✨ {additionalReward}".Trim();
        }

        // Get rank title based on level
        public static string GetRankTitle(int level)
        {
            return level switch
            {
                >= 100 => "🌟 Celestial Master",
                >= 80 => "⚡ Archon",
                >= 60 => "🗡️ Adeptus",
                >= 40 => "🛡️ Knight",
                >= 25 => "⚔️ Adventurer",
                >= 15 => "🎯 Hunter",
                >= 10 => "🌱 Novice",
                >= 5 => "👤 Beginner",
                _ => "🆕 Newcomer"
            };
        }

        // Progress bar shows current experience, total experience for the level, and a visual representation of the progress.
        public static string GetExpProgressBar(int currentExp, int expToNext, int barLength = 10)
        {
            int totalExpForCurrentLevel = currentExp + expToNext;
            int filledBars = totalExpForCurrentLevel > 0 ? (currentExp * barLength) / totalExpForCurrentLevel : 0;

            string progressBar = "";
            for (int i = 0; i < barLength; i++)
            {
                progressBar += i < filledBars ? "█" : "░";
            }

            return $"[{progressBar}] {currentExp}/{totalExpForCurrentLevel}";
        }
    }
}