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

        //New time method. Daily reset at 02:00 at UTC+2|+3
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

        //Old time method. I tried adapt it to 2 hours.
        /*        public void ScheduleTask(int hour, double hourInterval)
                {
                    var now = DateTime.Now;
                    var run = new DateTime(now.Year, now.Month, now.Day, hour, now.Minute, 0);
                    if (now > run)
                    {
                        run = run.AddHours(2);
                    }

                    var timeLeft = run - now;
                    if (timeLeft < TimeSpan.Zero)
                    {
                        timeLeft = TimeSpan.Zero;
                    }

                    var timer = new Timer(_ =>
                    {
                        DailyReset();
                    }, null, timeLeft, TimeSpan.FromHours(hourInterval)); ;

                    Timers.Add(timer);
                }*/

        //Alternate time method, if lastResetDate saves in database
        /*        public void ScheduleDailyReset()
                {
                    var now = DateTime.Now;
                    var nextReset = new DateTime(now.Year, now.Month, now.Day, 2, 0, 0).AddDays(now.Hour >= 2 ? 1 : 0);
                    var timeLeft = nextReset - now;

                    // Перевіряємо останнє скидання в БД
                    string lastResetDate = GetLastResetDate();

                    if (lastResetDate != now.ToString("yyyy-MM-dd") && now.Hour >= 2)
                    {
                        DailyReset();
                        UpdateLastResetDate(now.ToString("yyyy-MM-dd"));
                    }

                    var timer = new Timer(_ =>
                    {
                        DailyReset();
                        UpdateLastResetDate(DateTime.Now.ToString("yyyy-MM-dd"));
                    }, null, timeLeft, TimeSpan.FromDays(1));

                    Timers.Add(timer);
                }

        private string GetLastResetDate()
        {
            using var con = new SQLiteConnection(MainDb());
            con.Open();

            using var cmd = new SQLiteCommand("SELECT LastResetDate FROM Config LIMIT 1", con);
            var result = cmd.ExecuteScalar();
            return result?.ToString() ?? "2000-01-01"; // Якщо немає запису, повертаємо стару дату
        }

        private void UpdateLastResetDate(string date)
        {
            using var con = new SQLiteConnection(MainDb());
            con.Open();

            using var cmd = new SQLiteCommand("UPDATE Config SET LastResetDate = @date", con);
            cmd.Parameters.AddWithValue("@date", date);
            cmd.ExecuteNonQuery();
        }*/

    }
}
