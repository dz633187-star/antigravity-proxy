# Antigravity Proxy (приложение)

Маленькое окно для Windows, которое:

- держит локальный прокси `127.0.0.1:10809` (Xray);
- позволяет менять внешний прокси (адрес, порт, логин, пароль);
- проверяет, куда реально выходит трафик (`ipinfo.io`);
- запускает Antigravity с правильной переменной окружения;

## Установка

```
powershell -ExecutionPolicy Bypass -File install-app.ps1
```

(от администратора). Скрипт скопирует файлы в `%LOCALAPPDATA%\AntigravityProxy`,
создаст ярлык на рабочем столе и в меню Пуск, выдаст пользователю права на
папку `%LOCALAPPDATA%\XrayNL`.

## Где лежат данные

- конфиг Xray: `%LOCALAPPDATA%\XrayNL\config.json`
- старый конфиг VPS/Reality (приложением не используется): `%LOCALAPPDATA%\XrayNL\config.json.vless-bak`
- журнал доступа: `%LOCALAPPDATA%\XrayNL\access.log`

В репозиторий эти файлы не попадают — там могут быть ваши пароли.
