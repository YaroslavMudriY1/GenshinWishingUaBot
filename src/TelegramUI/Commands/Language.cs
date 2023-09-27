﻿// SPDX-License-Identifier: MPL-2.0

using System.Data.SQLite;
using Telegram.Bot.Types;
using static TelegramUI.Startup.Config;

namespace TelegramUI.Commands
{
    public class Language
    {
        internal static void AddChat(Message message)
        {
            using var con = new SQLiteConnection(MainDb());
            con.Open();

            using var cmd = new SQLiteCommand(con);
            cmd.Parameters.Add(new SQLiteParameter("@chat", message.Chat.Id));
            
            cmd.CommandText = "INSERT OR IGNORE INTO Chats(ChatId) VALUES(@chat)";
            cmd.ExecuteNonQuery();
            
            con.Close();
        }
        
        internal static void ChangeLanguage(Message message, string locale)
        {
            using var con = new SQLiteConnection(MainDb());
            con.Open();

            using var cmd = new SQLiteCommand(con);
            cmd.Parameters.Add(new SQLiteParameter("@chat", message.Chat.Id));
            cmd.Parameters.Add(new SQLiteParameter("@locale", locale));
            
            cmd.CommandText = "UPDATE Chats SET Language = @locale WHERE ChatId = @chat";
            cmd.ExecuteNonQuery();
            
            con.Close();
        }
        
internal static string GetLanguage(Message message)
{
    using var con = new SQLiteConnection(MainDb());
    con.Open();
    var language = "en";

    if (!string.IsNullOrEmpty(message.Text) && message.Text.StartsWith("/lang"))
    {
        // Handle /lang command without specifying a language
        return language;
    }

    using var cmd = new SQLiteCommand(con);
    cmd.Parameters.Add(new SQLiteParameter("@chat", message.Chat.Id));

    cmd.CommandText = "SELECT Language FROM Chats WHERE ChatId = @chat";
    using var rdr = cmd.ExecuteReader();

    if (rdr.Read() && !rdr.IsDBNull(0))
    {
        var languageObj = rdr.GetValue(0);
        if (languageObj != null)
        {
            language = languageObj.ToString();
        }
    }

    con.Close();

    return language;
}


    }
}