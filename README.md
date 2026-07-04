# Chat – real-time chat application

A real-time chat application built with **C# / .NET 8**:

- **ChatApi** – ASP.NET Core REST API + Entity Framework Core + MySQL
- **ChatClient** – WPF desktop client (MVVM)

Everything runs locally. The client stays "real-time" by polling the API on short
intervals using incremental (`afterId`) requests, so new messages, presence and
typing status arrive quickly while keeping the API load low.

---

## Features

- Registration & login (PBKDF2-hashed passwords, bearer-token sessions)
- Editable **profile** – display name + change password
- Multiple **chat rooms**, created on the fly
- Message **history** with load-older-on-scroll and **date dividers**
- **Edit your own messages** (changes propagate to other clients)
- **Attachments** – images are shown inline, other files open in the browser
- **Emoji** quick bar, **new-message sound**, and an **offline/reconnect** indicator
- **Smart auto-scroll** with a "new messages" pill when scrolled up
- **Online users** per room and an **"is typing…"** indicator
- **Direct messages** (1:1) with **delivered / read** receipts and unread badges
- Clean dark **MVVM** WPF UI
- **Unit tests** for the API (`tests/ChatApi.Tests`)

> The API upgrades the database schema automatically on startup (adds any new
> columns and the direct-messages table), so the database never needs to be
> dropped between versions.

---

## Architecture

```
WPF client  ──HTTP/JSON──►  ASP.NET Core REST API  ──EF Core──►  MySQL
  (MVVM, polling)               (ChatApi)                        (chatapp)
```

The client uses several timers to feel real-time without overloading the API:

| Timer      | Interval | Purpose                                             |
|------------|----------|-----------------------------------------------------|
| Messages   | 1 s      | `GET /messages?afterId=` – fetch only new rows      |
| Presence   | 2 s      | who is online / typing + direct-message unread counts |
| Heartbeat  | 3 s      | report "I'm alive" + current room                   |
| Reconcile  | 3 s      | re-check the latest page so edits by others show up |

Typing notifications are throttled to at most one request every 2 seconds.
Direct-message conversations poll once per second while their window is open.

---

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A running **MySQL** (or MariaDB) server, default `localhost:3306`
- Windows (the WPF client is Windows-only)

---

## Getting started

### 1. Database

The API **creates the database and tables automatically** on first run, so you
only need a reachable MySQL server. Adjust the connection string in
`src/ChatApi/appsettings.json` if your credentials differ:

```json
"ConnectionStrings": {
  "Default": "server=localhost;port=3306;database=chatapp;user=root;password=root"
}
```

A `docker-compose.yml` is included if you prefer to run MySQL in a container
(`docker compose up -d`). The reference schema is in `db/schema.sql`.

### 2. Run the API

```bash
cd src/ChatApi
dotnet run
```

The API listens on **http://localhost:5099**. Swagger UI (for trying the
endpoints) is at `http://localhost:5099/swagger`.

### 3. Run the WPF client

In a second terminal (or from Visual Studio):

```bash
cd src/ChatClient
dotnet run
```

Register a user and log in. To test real-time chat, launch a **second** client
instance and log in as a different user. The client expects the API at
`http://localhost:5099` (see `App.ApiBaseUrl` in `src/ChatClient/App.xaml.cs`).

---

## API reference

All endpoints except register/login require an
`Authorization: Bearer {token}` header (the token is returned by register/login).

| Method | Route                                | Body / Query                | Description                    |
|--------|--------------------------------------|-----------------------------|--------------------------------|
| POST   | `/api/auth/register`                 | `{username, password}`      | Create account, get token      |
| POST   | `/api/auth/login`                    | `{username, password}`      | Log in, get token              |
| POST   | `/api/auth/logout`                   | –                           | Invalidate token               |
| PUT    | `/api/auth/profile`                  | `{displayName, newPassword}`| Update profile                 |
| GET    | `/api/rooms`                         | –                           | List rooms                     |
| POST   | `/api/rooms`                         | `{name}`                    | Create room                    |
| GET    | `/api/rooms/{id}/messages`           | `?afterId=0&beforeId=0`     | Messages (incremental/history) |
| POST   | `/api/rooms/{id}/messages`           | `{content}`                 | Send a message                 |
| PUT    | `/api/rooms/{id}/messages/{msgId}`   | `{content}`                 | Edit own message               |
| POST   | `/api/rooms/{id}/attachments`        | multipart `file`            | Upload an attachment           |
| POST   | `/api/presence/heartbeat`            | `{roomId}`                  | Report alive + current room    |
| POST   | `/api/presence/typing`               | `{roomId}`                  | Report typing                  |
| GET    | `/api/rooms/{id}/presence`           | –                           | Online users + typing          |
| GET    | `/api/dm/overview`                   | –                           | Users + unread DM counts       |
| GET    | `/api/dm/{userId}/messages`          | `?afterId=0`                | DM conversation                |
| POST   | `/api/dm/{userId}`                   | `{content}`                 | Send a direct message          |
| POST   | `/api/dm/{userId}/read`              | –                           | Mark DMs from user as read     |

---

## Project layout

```
ChatApp.sln
docker-compose.yml           # optional MySQL container
db/
  schema.sql                 # reference SQL (API auto-creates tables)
src/
  ChatApi/                   # ASP.NET Core REST API
    Controllers/             # Auth, Rooms, Messages, Attachments, Presence, DirectMessages
    Data/AppDbContext.cs     # EF Core context
    Models/                  # User, Room, Message, DirectMessage
    Dtos/Dtos.cs             # request/response records
    Security/PasswordHasher.cs
    Program.cs               # startup, DB create/upgrade, static files
    appsettings.json
  ChatClient/                # WPF desktop client (MVVM)
    Models/ApiModels.cs
    Services/ChatApiClient.cs
    ViewModels/              # ChatViewModel, DirectChatViewModel, item + base types
    Views/                   # Login, Main, Profile, DirectChat windows
    App.xaml(.cs)
tests/
  ChatApi.Tests/             # xUnit tests (password hashing, auth controller)
```

---

## Tests

```bash
dotnet test
```

Covers password hashing and the auth controller (register / login / duplicate /
validation) using EF Core's in-memory provider.

---

## Notes & possible improvements

- Sessions use a token stored on the user row; a production app would use JWT
  with expiry and refresh.
- Polling could be replaced with **SignalR / WebSockets** for true server push
  without changing the data model.
- Attachments are stored on the server's local disk under `wwwroot/uploads`.
```
