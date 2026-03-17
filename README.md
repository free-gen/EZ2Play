# EZ2Play Launcher

[![GitHub release](https://img.shields.io/github/v/release/free-gen/EZ2Play?style=for-the-badge)](https://github.com/free-gen/EZ2Play/releases/latest)
[![Platform](https://img.shields.io/badge/.Net-0066ff?style=for-the-badge&logo=data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSIxNSIgaGVpZ2h0PSIxNSIgdmlld0JveD0iMCAwIDE1IDE1Ij48cGF0aCBmaWxsPSIjZmZmIiBkPSJNMTQuODE0LjExMUEuNS41IDAgMCAxIDE1IC41VjdIN1YxLjU5NkwxNC4zOTUuMDFhLjUuNSAwIDAgMSAuNDIuMU02IDEuODFMLjM5NSAzLjAxMUEuNS41IDAgMCAwIDAgMy41VjdoNnpNMCA4djQuNWEuNS41IDAgMCAwIC40My40OTVsNS41Ny43OTZWOHptNyA1LjkzNGw3LjQzIDEuMDYxQS41LjUgMCAwIDAgMTUgMTQuNVY4SDd6Ii8+PC9zdmc+&labelColor=gray)](https://dotnet.microsoft.com)
[![Framevork](https://img.shields.io/badge/WPF--UI-0066ff?style=for-the-badge&logo=data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSI0OCIgaGVpZ2h0PSI0OCIgdmlld0JveD0iMCAwIDQ4IDQ4Ij48cGF0aCBmaWxsPSIjZmZmIiBkPSJNMjUuMTcgNC4xNjNhMS41IDEuNSAwIDAgMC0xLjM0IDBsLTEyIDZhMS41IDEuNSAwIDAgMC0uODMgMS4zNDJ2MjMuNDU2YzAgLjUxNi4yNjUuOTk2LjcwMiAxLjI3bDEyIDcuNTRBMS41IDEuNSAwIDAgMCAyNiA0Mi41VjMwLjQzMmwxMS4xNy01LjU4NWExLjUgMS41IDAgMCAwIDAtMi42ODRsLTkuMzE2LTQuNjU4bDkuMzE3LTQuNjU4YTEuNSAxLjUgMCAwIDAgMC0yLjY4NHoiLz48L3N2Zz4=&labelColor=gray)](https://wpfui.lepo.co/)

EZ2Play - это минималистичный игровой лаунчер для тех, кто ценит простоту и отсутствие лишних настроек. Его логика строится на использовании стандартных ярлыков Windows, которые уже содержат всё необходимое: путь к игре, параметры запуска и иконку .ico в качестве обложки. Лаунчер позволяет управлять играми с геймпада и быстро переключать изображение между монитором и телевизором, делая игру на большом экране максимально удобной.

<img width="2560" height="1440" alt="Снимок экрана 2026-03-11 184446" src="https://github.com/user-attachments/assets/a90f531b-9c2a-4d43-a670-f4220ba9316e" />

## Возможности

- **Обработчик кнопки Home/Guide** - возврат в лаунчер из любой игры по нажатию одной кнопки
- **Быстрое переключение экрана** - опция доступна при наличии двух и более экранов
- **Мультиязычность** - поддержка русского и английского языков
- **Никаких настроек** - достаточно поместить ярлыки в папку `shortcuts`
- **WPF-UI** - полная поддержка стилей Windows 11 

## Использование

1. Скачайте и запустите `EZ2Play.exe`
2. Поместите ярлыки игр (.lnk и .url) в папку `shortcuts` (создаётся автоматически)
3. Опционально: для настройки ярлыков вы можете воспользоваться `EZParser`
4. Лаунчер готов к работе

## Кастомизация

EZ2Play поддерживает пользовательские звуки и логотип запуска.
Для этого используется файл `ui.pack` в папке с лаунчером.

#### Создание ui.pack

1. Подготовьте нужные файлы (см. состав ниже).
2. Упакуйте их в архив `ui.zip` (без подпапок, файлы должны лежать в корне).
3. Переименуйте архив `ui.zip` в `ui.pack`.

#### Состав пакета

- `logo.png` — логотип заставки
- `select.mp3` — звук перемещения
- `action.mp3` — звук запуска
- `abort.mp3` — звук возврата
- `ambient.mp3` — фоновая музыка

> **Примечание:** если файл `ui.pack` отсутствует или в нём нет какого-либо ресурса, используется встроенное значение по умолчанию.

## Параметры командной строки

```bash
EZ2Play.exe [параметры]
```

| Параметр       | Описание |
|----------------|----------|
| `--nosplash`   | Запуск без экрана-заставки |
| `--hotswap`    | Автоматически переключает дисплей при старте и возвращает исходный при закрытии |
| `--nomusic`    | Отключает фоновую музыку |

### Примеры

```bash
# Запуск без заставки
EZ2Play.exe --nosplash

# Запуск с автоматическим переключением дисплея
EZ2Play.exe --hotswap

# Запуск без фоновой музыки
EZ2Play.exe --nomusic

# Комбинация
EZ2Play.exe --hotswap --nosplash --nomusic
```

# Вспомогательное приложение EZParser

<img width="960" height="780" alt="Снимок экрана 2026-03-06 085818" src="https://github.com/user-attachments/assets/6bb61e34-8d59-4008-9139-8f79f169f0e8" />
<img width="960" height="780" alt="Снимок экрана 2026-03-06 085705" src="https://github.com/user-attachments/assets/775261f3-be1f-48ed-a0fc-689e0cb69c1c" />

Позволяет быстро находить обложки для игр и сохранять их в формате иконок, используя встроенный конвертер `.png → .ico`. Поддерживает **PsStore** и **SteamGridDB**.

> Для SteamGridDB требуется API ключ

## Системные требования

- Windows 10 / 11
- .NET Framework 4.8

## Сборка из исходного кода

Для сборки проекта используйте .NET SDK:
```bash
dotnet build --configuration Release
```

## Поддержать проект

**Можно криптой или рублем:**

[![OZON](https://img.shields.io/badge/RUB-OZON_БАНК-0066ff?style=for-the-badge)](https://finance.ozon.ru/apps/sbp/ozonbankpay/019993bb-a466-72de-bc2c-e7ee85abc8a6)
[![USDT](https://img.shields.io/badge/USDT-TRC_20-009933?style=for-the-badge)](https://tronscan.org/#/address/TZD9FhF1ZusMCN2XfSQrb2jpRBk7YTCzUy)

## Лицензия

Проект распространяется под лицензией **MIT**. Подробности в файле [LICENSE](LICENSE).
