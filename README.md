# 🛡️ TgPcAgent

**Удалённое управление ПК через Telegram с невероятной скоростью благодаря Cloudflare Tunnels и современному SPA Mini App.**

[![Latest Release](https://img.shields.io/github/v/release/ExtraTools/TgPcAgent?include_prereleases&label=Releases&color=blue)](https://github.com/ExtraTools/TgPcAgent/releases)
[![Bot Link](https://img.shields.io/badge/Telegram-Bot-blue?logo=telegram)](https://t.me/WaitDino_bot)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)

---

## ✨ Ключевые возможности

* 📱 **Telegram Mini App:** Полноценный SPA-интерфейс прямо внутри Telegram. Переключение вкладок происходит мгновенно без перезагрузки страниц.
* ⚡ **Direct Tunnel Mode:** Агент автоматически поднимает безопасный прямой туннель к вашему ПК через инфраструктуру Cloudflare. Задержка выполнения команд **~250мс** вместо поллинга.
* 📸 **Управление и Мониторинг:** Реал-тайм графики нагрузки, быстрые скриншоты, завершение процессов и запуск приложений в один клик.
* 🔌 **Питание и Сеанс:** Выключение, перезагрузка, сон и блокировка ПК с телефона.
* 📜 **Кастомные скрипты:** Мощный движок для создания своих кнопок, автоматизаций и фоновых задач. Файловый списковый менеджер.
* 🔐 **Приватность:** Авторизация через одноразовый код `Pairing Code`. Для облачного API и туннеля используются уникальные Secure Tokens. Защита от чужих сообщений на уровне драйвера команд.
* 📦 **Auto-Updater:** Агент работает в фоне, сам проверяет релизы на GitHub и обновляется.

---

## 🛠 Установка и Подключение

1. Зайдите в раздел **[Releases](https://github.com/ExtraTools/TgPcAgent/releases/latest)** и скачайте `TgPcAgent-Setup.exe`.
2. Запустите установщик.
3. Откройте панель в системном трее Windows (иконка щита) и скопируйте **код привязки**.
4. Зайдите в официального бота **[@WaitDino_bot](https://t.me/WaitDino_bot)** и отправьте: `/pair ВАШ_КОД`.
5. Откройте **Mini App** через кнопку слева от поля ввода.

---

## 📥 Версии установщика

Сборка полностью автоматизирована и сильно ужата для скорости скачивания.

| Версия | Размер | Описание |
|---|---|---|
| **Full**<br>`TgPcAgent-Setup.exe` | ~35 МБ | В этот файл уже **встроен .NET 10**. Работает сразу "из коробки" на любой современной Windows. Идеально для большинства. |
| **Lite**<br>`TgPcAgent-Setup-Lite.exe` | ~3.7 МБ | Ультра-компактная версия. Только бинарники агента. Требует чтобы на ПК был установлен [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0). |

*Cloudflared*, необходимый для прямого туннеля, скачивается агентом самостоятельно при первом старте и весит всего несколько мегабайт.

---

## 🏗 Архитектура (v0.2.1+)

В версии 0.2 мы перевели платформу на комбинацию **Direct Tunnels + Vercel Cloud API**. Код написан на **C# 14 / .NET 10** для максимальной производительности (Escape Analysis, JIT optimizations).

```text
Ваш телефон (Telegram Mini App) 
       ↓ (X-Telegram-Init-Data)
[Vercel Serverless / Cloud Relay API] 
       ↓ (Получает HTTP-туннель)
[Cloudflare Edge Network] ───────→ Прямой TLS Туннель
                                           ↓
                                Ваш ПК (TgPcAgent + Cloudflared)
```

Локальные данные, логи настройки и бинарники туннеля хранятся приватно в:
`%LocalAppData%\TgPcAgent\`
