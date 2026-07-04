# Chat – real-time chat application

A simple real-time chat app built with **C# / .NET 8**:

- **ChatApi** – ASP.NET Core REST API + Entity Framework Core + MySQL
- **ChatClient** – WPF desktop client (MVVM)

Everything runs locally. The client stays "real-time" by polling the API for new
messages, presence and typing status on short intervals, using incremental
(`afterId`) requests so the API load stays low.

---

## Features

- Registration & login (PBKDF2-hashed passwords, bearer-token sessions)
- Editable **profile** (display name + change password)
- Multiple chat rooms (create new rooms on the fly)
- Message history with **load-older-on-scroll** and **date dividers**
- **Edit your own messages** (propagated to other clients)
- **Attachments** – send images (shown inline) and files (open in browser)
- **Emoji** quick bar, **new-message sound**, **offline/reconnect** indicator
- **Smart auto-scroll** with a "new messages" pill when scrolled up
- Online users per room + "is typing…" indicator
- **Direct messages** (1:1) with **delivered / read** receipts and unread badges
- Clean dark MVVM WPF UI
- xUnit test project for the API (`tests/ChatApi.Tests`)

> The API upgrades the database schema automatically on startup (adds new
> columns / the direct-messages table), so you never need to drop the database
> between versions.

---

## Architecture

```
WPF client  ──HTTP/JSON──►  ASP.NET Core REST API  ──EF Core──►  MySQL
   (polls every 1–3s)            (ChatApi)                       (chatapp)
```

Real-time is approximated with three timers in the client:

| Timer        | Interval | Purpose                                  |
|--------------|----------|------------------------------------------|
| Messages     | 1 s      | `GET /messages?afterId=` – only new rows |
| Presence     | 2 s      | who is online / typing                   |
| Heartbeat    | 3 s      | report "I'm alive" + current room        |

Typing notifications are throttled to at most one request every 2 seconds.

---

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A running **MySQL** (or MariaDB) server, default `localhost:3306`
- Windows (the WPF client is Windows-only)

---

## Setup

### 1. Database

The API **creates the database and tables automatically** on first run, so you
only need a reachable MySQL server. Update the connection string in
`src/ChatApi/appsettings.json` if your credentials differ:

```json
"ConnectionStrings": {
  "Default": "server=localhost;port=3306;database=chatapp;user=root;password=root"
}
```

(If you prefer to create the schema by hand, run `db/schema.sql`.)

### 2. Run the API

```bash
cd src/ChatApi
dotnet run
```

The API listens on **http://localhost:5099**. Swagger UI is available at
`http://localhost:5099/swagger` for trying the endpoints.

### 3. Run the WPF client

In a second terminal (or from Visual Studio):

```bash
cd src/ChatClient
dotnet run
```

Register a user, then log in. To test real-time chat, launch a **second**
instance of the client and log in as a different user.

> The client expects the API at `http://localhost:5099` (see
> `App.ApiBaseUrl` in `src/ChatClient/App.xaml.cs`).

---

## API reference

All endpoints except register/login require an
`Authorization: Bearer {token}` header (token returned by register/login).

| Method | Route                                   | Body / Query              | Description                |
|--------|-----------------------------------------|---------------------------|----------------------------|
| POST   | `/api/auth/register`                    | `{username, password}`    | Create account, get token  |
| POST   | `/api/auth/login`                       | `{username, password}`    | Log in, get token          |
| POST   | `/api/auth/logout`                      | –                         | Invalidate token           |
| PUT    | `/api/auth/profile`                     | `{displayName,newPassword}` | Update profile           |
| GET    | `/api/rooms`                            | –                         | List rooms                 |
| POST   | `/api/rooms`                            | `{name}`                  | Create room                |
| GET    | `/api/rooms/{id}/messages`              | `?afterId=0&beforeId=0`   | Messages (incremental/history) |
| POST   | `/api/rooms/{id}/messages`              | `{content}`               | Send a message             |
| PUT    | `/api/rooms/{id}/messages/{msgId}`      | `{content}`               | Edit own message           |
| POST   | `/api/rooms/{id}/attachments`           | multipart `file`          | Upload an attachment       |
| POST   | `/api/presence/heartbeat`               | `{roomId}`                | Report alive + room        |
| POST   | `/api/presence/typing`                  | `{roomId}`                | Report typing              |
| GET    | `/api/rooms/{id}/presence`              | –                         | Online users + typing      |
| GET    | `/api/dm/overview`                      | –                         | Users + unread DM counts   |
| GET    | `/api/dm/{userId}/messages`             | `?afterId=0`              | DM conversation            |
| POST   | `/api/dm/{userId}`                      | `{content}`               | Send a direct message      |
| POST   | `/api/dm/{userId}/read`                 | –                         | Mark DMs from user as read |

---

## Project layout

```
ChatApp.sln
db/
  schema.sql                 # reference SQL (API auto-creates tables)
src/
  ChatApi/                   # ASP.NET Core REST API
    Controllers/             # Auth, Rooms, Messages, Presence
    Data/AppDbContext.cs     # EF Core context
    Models/                  # User, Room, Message
    Dtos/Dtos.cs             # request/response records
    Security/PasswordHasher.cs
    Program.cs
    appsettings.json
  ChatClient/                # WPF desktop client (MVVM)
    Models/ApiModels.cs
    Services/ChatApiClient.cs
    ViewModels/              # ChatViewModel, RelayCommand, ...
    Views/                   # LoginWindow, MainWindow
    App.xaml(.cs)
```

---

## Tests

```bash
dotnet test
```

Covers password hashing and the auth controller (register / login / duplicate /
validation) using EF Core's in-memory provider.

---

## Publish to GitHub

From the project root (`H:\Projects\chat`):

```bash
git init
git add .
git commit -m "Initial commit: WPF chat app + ASP.NET REST API + MySQL"
git branch -M main
git remote add origin https://github.com/<your-username>/<repo>.git
git push -u origin main
```

Create the empty **public** repo on GitHub first, then run the commands above.

---

## Notes & possible improvements

- Tokens are stored in the `Users` table; for production use JWT with expiry.
- Polling could be swapped for **SignalR/WebSockets** for true push without
  changing the data model.
- No message editing/deletion or file attachments (kept intentionally simple).
