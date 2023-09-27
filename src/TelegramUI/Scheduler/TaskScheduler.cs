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

   private DateTime lastWishTime = DateTime.MinValue;
    private static readonly int hoursInterval = 2; // Статичний інтервал в годинах

    public void ScheduleTask(int hour, double hourInterval)
    {
        var now = DateTime.Now;

        // Отримуємо наступний можливий час виклику "/wish" згідно із статичним інтервалом
        var nextWishTime = lastWishTime.AddHours(2);

        // Перевіряємо, чи маємо чекати до наступного можливого часу
        if (now < nextWishTime)
        {
            var timeLeft = nextWishTime - now;

            var timer = new Timer(_ =>
            {
                DailyReset();
            }, null, timeLeft, TimeSpan.FromHours(hoursInterval));

            Timers.Add(timer);
        }
        else
        {
            // Викликаємо DailyReset() відразу, якщо час минув
            DailyReset();
        }
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