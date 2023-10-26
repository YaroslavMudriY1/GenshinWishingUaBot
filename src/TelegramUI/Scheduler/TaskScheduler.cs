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
        public static int LastRolledHour;
        public static int LastRolledMinute;

        public int LastRolledH() { return LastRolledHour; }
        public int LastRolledM() { return LastRolledMinute; }
        public void ScheduleTask(int hour, double hourInterval)
        {
            LastRolledHour = DateTime.Now.Hour;
            LastRolledMinute=DateTime.Now.Minute;
            var now = DateTime.Now;
            var run = new DateTime(now.Year, now.Month, now.Day, hour, now.Minute, 0);
            //TimeSpan 2hours = TimeSpan(2, 0, 0);
            if (now > run)
            {
                run = run.AddHours(hourInterval);
                //run += 2hours
            }

            var timeLeft = run - now;
            if (timeLeft < TimeSpan.Zero)
            {
                timeLeft = TimeSpan.Zero;
            }

            var timer = new Timer(_ =>
            {
                DailyReset();
            }, null, timeLeft, TimeSpan.FromHours(2)); ;

            Timers.Add(timer);

        }


        private static void DailyReset()
        {
            using var con = new SQLiteConnection(MainDb());
            con.Open();

            using var cmd = new SQLiteCommand(con)
            {
                CommandText = "UPDATE UsersInChats SET HasRolled = 0"
            };
            cmd.ExecuteNonQuery();

            con.Close();
        }
    }
}
