# ProfixmedWeb

## Админка (мероприятия и новости)

Скрытая панель для редактирования главной страницы: `https://<сайт>/<Admin:Path>` (по умолчанию `/manage`).
Пока не задан `Admin:PasswordHash`, панель выключена (адрес отдаёт 404).

Настройка на сервере — в `/var/www/profixmed/shared/appsettings.Production.json`:

```json
{
  "Admin": {
    "Path": "придумайте-свой-путь",
    "Username": "admin",
    "PasswordHash": "pbkdf2-sha256$..."
  }
}
```

Хеш пароля (пароль от 10 символов):

```bash
dotnet /var/www/profixmed/current/ProfixmedWeb.dll hash-password
```

Данные панели (`content.json`, загруженные файлы, ключи сессий) лежат в `App_Data/`.
При деплое это ссылка на `/var/www/profixmed/shared/content`, так что деплой их не затирает.
При первом запуске `content.json` создаётся из содержимого, которое раньше было в `Index.cshtml`.

Если перед сайтом стоит nginx, для загрузки PDF нужен `client_max_body_size 50m;` (по умолчанию 1 МБ).
