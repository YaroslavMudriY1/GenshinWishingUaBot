# GenshinWishingBot

A Telegram chat bot that simulates Genshin Impact wishes. Built with C#, .NET 8.0 and SQLite as RDBMS.

***Current version 5.6' (ua/en)***

### Patch Note (Major Update 1.5)

"Rise and Rank, Traveler!"
- `/rank` – Ever wondered how much of a Genshin gacha gremlin you’ve become? Now you can flex your level and see your EXP bar! (No, you can’t pay to win. Yet.)
- `/me` – Because sometimes you just want to admire yourself. Shows your stats, not your IRL luck.
- `/get_info` and `/get_inv` (in respond) – Stalk your friends’ stats and inventories (with consent, of course – reply to their message!).
- `/daily` – Claim your daily 3✨ Starglitter! Like a login bonus, but you don’t have to open Hoyoverse’s launcher.
- `/gift` [amount] (in respond) – Feeling generous? Send Starglitter to your friends! Feeling evil? Remind them you have more. 
- **Rank System** – Every wish now gives you EXP. The more you wish, the higher you climb. Gacha addiction, now with levels!
- *Easter Eggs* – New hidden jokes and surprises. Try weird stuff. Paimon dares you to ask (`/ask_paimon`).
- *Minor Fixes* – We squashed bugs, not Paimon. Probably.

### Previous changes (Major Update 1.3 and 1.4, compared to original functionality)
- New commands! Now you can `/trade` and `/sell` your items! For that commands you need to know item ID (by `/get_id`) If you don't know how use that commands - don't worry! Just type command (without attributes) in chat and bot will answer!
- Bot can react to key words like "how to *wish*?" if sended in reply to bot messages. Check other commands by "`?`".
- *Added jokes!* Just send "joke"/"rofl"/"meme" in reply to bot! 
- Added **a lot of weapons**, missing characters, assets, in short filling gaps to the last patch (5.3*). <ins>Reworked strings (added new details), now English and Ukrainian versions matches.</ins>
- Enhanced **skin system**, now it randomized. For characters with alternate skins (*Amber, Jean, Mona, Rosaria*) alternate skin chance is **50%** (event skin more priority). For event skins (lantern rite, summer) chance is **30%**. For 5✧ skins (*Diluc*) chance is **20%**.
- Added **50/50 system**, for event 5✧ weapons and characters. If hits 5✧, randomizer (50/50) checks if it event one. If you lose, it'll be saved in DB, and next time you get guaranteed event 5✧. Check "Event 5✧" in `/inv`.
- Used **in-game wish rate** (different for 5✧ weapon and characters), but *pity was substract to 30* for 5✧ and 7 for 4✧. >(It's just a telegram bot for fun, right?)
- **Added Starglitter✨**, cashback for wishes. If you have 10+ ✨, just type `/wish` to extra wish, ignoring timer. To check your ✨ use `/balance`. From different wish results, gives different amount of Starglitter (check wish message): 
*1th 5✧ - 10✨, 1th 4✧ - 3✨, dublicate 5✧ - 10✨, 4✧ - 2✨, 7th+ char|6th+ weapon (>C6|R5) 5✧ - 25✨, 4✧ (>C6|R5) - 5✨*.
- **Updated timer!** Now it works correctly in wish messages. Last wish time saves in DB. When bot launches or after 02:00 o'clock (UTC+2|+3) timer resets for everyone. *Curently bot setted for every 2-hour wishes.*
- **Expanded inventory!** Now you can see your pity, all-time wishes and starglitter balance in `/inv`.
- **Added 10 wishes command!** You can spend 100✨ to quickly make ten wishes. Results be in one message.

*'current Genshin Impact patch - 5.6. Last bot update - 04.06.2025

### If you want to try bot, check `@WishGenshinUaBot` in telegram. (works only in groups)

## Usage

Add this bot to your group chat and start using it right away.

### User commands:
- /wish or /w - make a wish and get a randomized result, can wish once a two hour per chat, resets at 12PM UTC+0.
- /wish10 or /w10 - make a ten wish, using 100 starglitter ✨. 
- /inventory or /inv - get your inventory in the chat.
- /balance or /b - get your starglitter✨ balance.
- /daily - claim your daily 3✨ Starglitter.
- /me - get your stats and level.
- /trade [item_id] [amount] - trade items with other users. Use `/get_id` to get item ID.
- /sell [item_id] [amount] - sell items to the bot and receive Starglitter. Use `/get_id` to get item ID.
- /settings - get your settings (currently only auto-wish available).

### Admins commands:
- /lang [code] (chat admins only) - change the locale for a specific chat, e.g. `/lang en`. Avaliable: `en, ua`.
- /addStar [amount] (bot owner only) - reply to any user's message with this command to add some amount to user's starglitter balance.
- /resetUser (bot owner only) - reply to any user's message with this command to reset his wish timer.
- /resetChat (bot owner only) - send this to the chat to reset everyone's wish timers.

## Installation

Requirements: .NET SDK 8.0

1. Clone the repository.
2. Build the solution to restore dependencies.
3. Change the `appsettings.json` token, bot username and admin id accordingly.
4. Make sure you use the main database (located in Debug\Release folder*). Sample.db located in main git folder. 
Path to place database might look like - GenshinWishingUaBot\src\TelegramUI\bin\Debug\net8.0\main.db
5. Run the project.

## License

The source code is licensed under Mozilla Public License 2.0.

Genshin Impact content and materials are a copyright of miHoYo Co., Ltd. No copyright infringement intended.

Original code belongs to "FrenzyYum". Feel free to mode and upgrade.
https://github.com/FrenzyYum/GenshinWishingBot

### Version 1.5, made by YMY with GPT-4o, Claude 4 and GitHub Copilot. 
