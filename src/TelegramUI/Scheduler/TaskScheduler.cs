// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using static TelegramUI.Startup.Config;
using Timer = System.Threading.Timer;

namespace TelegramUI.Scheduler
{
    public class TaskScheduler
    {
        private List<Timer> Timers { get; } = new();
        public static TaskScheduler Instance { get; } = new();
        public static TaskScheduler QueueInstance { get; } = new();

        //New time method. Daily reset at 02:00 (host at UTC+2|+3)
        public void ScheduleDailyReset()
        {
            var now = DateTime.Now;
            var nextReset = new DateTime(now.Year, now.Month, now.Day, 2, 0, 0).AddDays(now.Hour >= 2 ? 1 : 0);
            var timeLeft = nextReset - now;

            // If bot start after 02:00, do DailyReset instantly
            if (now.Hour >= 2)
            {
                DailyReset();
            }

            var timer = new Timer(_ =>
            {
                DailyReset();
            }, null, timeLeft, TimeSpan.FromDays(1)); // do dailyReset every 24 hours

            Timers.Add(timer);
        }

        private static void DailyReset()
        {
            using var con = new SQLiteConnection(MainDb());
            con.Open();

            using var cmd = new SQLiteCommand(con)
            {
                CommandText = "UPDATE UsersInChats SET LastWishTime = NULL"
            };
            cmd.ExecuteNonQuery();

            con.Close();
        }

    }
}
