# TgPcAgent

Windows tray app that polls a Telegram bot and lets one paired owner chat query PC status, processes, screenshots, app launching, and selected power actions.

## What It Does

- `/ping` — pings `1.1.1.1` and `8.8.8.8` with roundtrip time in `ms`
- `/status` — machine name, OS, uptime, RAM, local IPv4 addresses, fixed drives, CPU load, CPU temp, GPU temp
- `/processes` — top 10 processes by memory
- `/screenshot` — sends a fresh desktop screenshot
- `/apps` — shows configured apps and a short scanned app list
- `/scanapps` — rescans Start Menu shortcuts
- `/open alias` — launches a configured app or scanned Start Menu shortcut
- `/lock` and `/sleep`
- `/shutdown` and `/restart` with double confirmation

## Security Model

- Bot token is stored locally and encrypted with Windows DPAPI.
- The bot accepts commands only from one paired Telegram chat.
- Pairing requires a local one-time code shown from the tray app.
- Unauthorized chats are ignored after pairing.
- `shutdown` and `restart` require two Telegram confirmations with expiry.
- The app is visible in the Windows tray and shows a local balloon when a screenshot is requested.

## First Run

1. Build or run the app:

```powershell
dotnet run --project .\TgPcAgent.App\TgPcAgent.App.csproj
```

2. Open the tray icon.
3. Open `Настройки`.
4. Paste your Telegram bot token and save.
5. Click `Показать код привязки`.
6. Send `/pair 123456` to your bot in Telegram.
7. Run `/help` or `/status`.

## Custom Apps

In settings, custom apps use this format:

```text
steam|C:\Program Files (x86)\Steam\steam.exe
chrome|C:\Program Files\Google\Chrome\Application\chrome.exe|--profile-directory=Default
```

Format is:

```text
alias|path|optional arguments
```

## Notes

- Scanned apps come from Windows Start Menu shortcuts.
- Config is saved in `%LocalAppData%\TgPcAgent\config.json`.
- Logs are saved in `%LocalAppData%\TgPcAgent\logs`.
