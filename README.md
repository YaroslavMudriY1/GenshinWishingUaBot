# GenshinWishingBot

A Telegram group chat bot simulating Genshin Impact gacha wishes.  
Built with C# · .NET 8 · SQLite · Telegram.Bot 16

**Version 1.6 · Content: patch 5.6 · Languages: 🇺🇦 UA / 🇬🇧 EN**
> Try it live: `@WishGenshinUaBot` (group chats only)

---

## Features

- 🎲 Wish simulation with in-game pull rates, pity system (30 / 7) and 50/50 mechanic
- ✨ Starglitter economy — earn from pulls, spend to bypass the timer
- 🗂️ Per-user per-chat inventory with full rarity breakdown
- 📈 Rank system — earn EXP every pull, level up for Starglitter rewards
- 🔁 Item trading between players
- 💬 Contextual FAQ and Easter eggs when replying to the bot

---
## Commands

### Wishing

| Command | Description |
|---|---|
| `/wish` · `/w` | Single wish. Once per 2 hours. Auto-bypasses timer if you have 10+ ✨ and Auto-wish is on. |
| `/wish10` · `/w10` | 10 wishes at once. Costs 100 ✨. |

### Inventory & Profile

| Command | Description |
|---|---|
| `/inv` · `/inventory` | Your items by rarity + stats summary. |
| `/me` · `/info` | Full profile: rank, EXP bar, pity, inventory totals. |
| `/stats` *(or reply to view someone else's)* | Compact stats overview. |
| `/rank` · `/level` · `/lvl` | Current level, rank title, EXP bar. |
| `/balance` · `/b` | Current Starglitter ✨ balance. |
| `/daily` | Claim 3 ✨ daily reward (resets at 02:00 server time). |

### Items

| Command | Description |
|---|---|
| `/get_id <name>` | Look up an item's ID. Supports partial names. |
| `/sell [amount] <id>` | Sell items for Starglitter (5★→25✨, 4★→10✨, 3★→1✨). |

### Social *(use in reply to another user's message)*

| Command | Description |
|---|---|
| `/trade [qty] <item> [qty] <item>` | Offer a trade. Accepts item names or IDs. Expires in 15 min. |
| `/gift <amount>` | Send Starglitter to another user. |
| `/peek` · `/get_inv` | View another user's inventory. |
| `/get_info` | View another user's full profile. |

### Extras

| Command | Description |
|---|---|
| `/ask_paimon` | Get a wish prediction from Paimon. |
| `/settings` | Toggle Auto-wish. Admins also see chat language controls. |
| `/help` | Full command list. |

### Admin / Owner

| Command | Who | Description |
|---|---|---|
| `/lang en` · `/lang ua` | Chat admin | Change chat language. |
| `/addStar <amount>` *(reply)* | Bot owner | Add Starglitter to a user. |
| `/resetUser` *(reply)* | Bot owner | Reset wish timer for one user. |
| `/resetChat` | Bot owner | Reset everyone's wish timers in the chat. |

---

## Gacha Mechanics

### Pull rates
| Rarity | Characters | Weapons |
|---|---|---|
| 5★ | 1.6% | 1.8% |
| 4★ | 13.0% | 14.5% |
| 3★ | 85.4% | 83.7% |

### Pity
- **4★** guaranteed on the 7th wish without one
- **5★** guaranteed on the 30th wish without one

### 50/50
On a 5★ pull there is a 50% chance of getting the event item. Losing the 50/50 guarantees the next 5★ will be the event item. Status visible in `/inv`.

### Starglitter cashback

| Scenario | Reward |
|---|---|
| First copy of a 5★ | 10 ✨ |
| Duplicate 5★ (≤ C6 / R5) | 10 ✨ |
| Over C6 / R5 for 5★ | 25 ✨ |
| First copy of a 4★ | 3 ✨ |
| Duplicate 4★ (≤ C6 / R5) | 2 ✨ |
| Over C6 / R5 for 4★ | 5 ✨ |

---

## Rank System

Every pull earns EXP (3★ → 1 · 4★ → 3 · 5★ → 10). Levelling up gives Starglitter rewards.

| Level | Title | Level-up reward |
|---|---|---|
| 1–4 | 🆕 Newcomer | 10 + (lvl/10 × 2) ✨ |
| 5–9 | 👤 Beginner | same |
| 10–14 | 🌱 Novice | same |
| 15–24 | 🎯 Hunter | same |
| 25–39 | ⚔️ Adventurer | same |
| 40–59 | 🛡️ Knight | same |
| 60–79 | 🗡️ Adeptus | same |
| 80–99 | ⚡ Archon | same |
| 100+ | 🌟 Celestial Master | same |

Milestone bonuses: every 5th level +50×(lvl/5) ✨ · every 10th level +100×(lvl/10) ✨

---

## FAQ

**How often can I wish?**  
Once every 2 hours per chat. All timers reset globally at 02:00 server time.

**How does Auto-wish work?**  
If Auto-wish is on (`/settings`) and your balance is ≥10 ✨, typing `/wish` while on cooldown will spend 10 ✨ and skip the timer automatically.

**How do I trade?**  
Reply to the other player's message and type `/trade [qty] <your item> [qty] <their item>`. Item names work directly — IDs are optional. The recipient has 15 minutes to accept or decline.  
Example: `/trade 2 Diluc 1 Furina`

**Items show as IDs in inventory instead of names.**  
The item ID doesn't match the current items list. Contact the server admin.

**What does "Guaranteed Event 5★: yes" mean?**  
You lost your last 50/50, so your next 5★ is guaranteed to be the event character/weapon.

---

## Installation

Requirements: **.NET SDK 8.0**
```bash
git clone https://github.com/YaroslavMudriY1/GenshinWishingUaBot
```

1. Edit `src/TelegramUI/Startup/appsettings.json`:
```json
{
  "Telegram": {
    "Token": "YOUR_BOT_TOKEN",
    "BotUsername": "@YourBotUsername",
    "AdminId": "YOUR_TELEGRAM_USER_ID"
  },
  "ConnectionStrings": {
    "MainDb": "Data Source=main.db"
  }
}
```
2. Copy `sample.db` to the output directory and rename to `main.db`.  
   Default path: `src/TelegramUI/bin/Debug/net8.0/main.db`
3. Build and run the project.
4. Add the bot to a group chat. **Private chats are not supported** (except `/help`).

---

## Project Structure
```
src/TelegramUI/
├── Commands/         # Business logic (Wish, Inventory, Trade, RankSystem…)
├── Scheduler/        # Daily reset timers
├── Startup/          # Config, appsettings
├── Strings/
│   ├── General/      # UI strings (en.json, ua.json)
│   ├── Items/        # Characters & weapons (en.json, ua.json)
│   ├── Misc/         # Jokes, puns, Paimon responses
│   └── Ranks/        # Rank titles & messages
└── Telegram/         # Bot command router (TelegramCommands.cs)
```

---

## License

Source code: [Mozilla Public License 2.0](LICENSE)  
Genshin Impact assets © HoYoverse. No copyright infringement intended.

Original project: [FrenzyYum/GenshinWishingBot](https://github.com/FrenzyYum/GenshinWishingBot)  
Extended by YMY with assistance from GPT-5.3, Claude 4.6 and GitHub Copilot.