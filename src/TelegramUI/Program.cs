// SPDX-License-Identifier: MPL-2.0

using System;
using TelegramUI.Scheduler;
using TelegramUI.Telegram;
using static TelegramUI.Startup.Config;

namespace TelegramUI
{
    public static class Program
    {
        //[Obsolete]
        private static void Main()
        {
            TaskScheduler.Instance.ScheduleTask(0,2);

            Bot.OnMessage += TelegramCommands.BotOnMessage;

            Bot.StartReceiving();
            Console.ReadLine();
            Bot.StopReceiving();
        }
    }
}