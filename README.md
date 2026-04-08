# TgPcAgent

Удалённое управление ПК через Telegram-бота с облачной архитектурой.

## Возможности

- Снимки экрана (JPEG)
- Мониторинг системы (CPU, GPU, RAM)
- Управление процессами
- Запуск приложений
- Управление питанием (выключение, перезагрузка, блокировка, сон)
- Автоматические задачи (авто-пинг, авто-скриншоты)
- Мультипользовательская архитектура через облачный relay
- Автоматические обновления через GitHub Releases

## Установка

1. Скачайте последний `TgPcAgent-Setup.exe` из [Releases](https://github.com/ExtraTools/TgPcAgent/releases)
2. Запустите установщик
3. В трее появится иконка агента — нажмите «Показать код привязки»
4. Отправьте `/pair КОД` боту [@WaitDino_bot](https://t.me/WaitDino_bot)

## Версии

- **Full** (`TgPcAgent-Setup.exe`) — самодостаточный, ~50 МБ, .NET 8 встроен
- **Lite** (`TgPcAgent-Setup-Lite.exe`) — лёгкий, ~4 МБ, требует .NET 8 Runtime

## Архитектура

```
Telegram → Webhook → Vercel Cloud → Command Queue → Agent (polling) → ПК
```
