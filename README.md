# TaskManager API

A REST API for managing projects, tasks and users, with JWT authentication and capability-based permissions.

Built with ASP.NET Core (.NET 10), Entity Framework Core and PostgreSQL 18.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- The EF Core command-line tool:

  ```bash
  dotnet tool install --global dotnet-ef
  ```

## Setup

Run these commands from the solution folder.

### 1. Start the database

Create your local settings file from the example:

```bash
cp .env.example .env
```

Fill in these values in `.env`. The other values in the file are not used yet.

```env
DB_PORT=5433
DB_NAME=taskmanager
DB_USER=taskmanager
DB_USER_PASSWORD=choose-a-password
```

Then start PostgreSQL:

```bash
docker compose up -d
```

### 2. Set the secrets

Secrets are kept in .NET user secrets and are never committed. Use the same database values you put in `.env`.

```bash
dotnet user-secrets --project TaskManager set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5433;Database=taskmanager;Username=taskmanager;Password=choose-a-password"
dotnet user-secrets --project TaskManager set "Jwt:Key" "a-random-key-of-at-least-32-characters"
dotnet user-secrets --project TaskManager set "Admin:Username" "admin"
dotnet user-secrets --project TaskManager set "Admin:Password" "choose-an-admin-password"
```

| Secret | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string |
| `Jwt:Key` | Key that signs the login tokens |
| `Admin:Username` | Username of the first admin |
| `Admin:Password` | Password of the first admin |

### 3. Create the database tables

```bash
dotnet ef database update --project TaskManager
```

### 4. Run the API

```bash
dotnet run --project TaskManager
```

The API runs at `http://localhost:5244`. To use HTTPS at `https://localhost:7144`, add `--launch-profile https`.

On first start, the app creates an admin user from `Admin:Username` and `Admin:Password`. The OpenAPI document is at `/openapi/v1.json`.

### Connecting a frontend

Browsers only allow calls from the address listed in `WithOrigins` in `TaskManager/Program.cs`. Set it to your frontend's address, for example `http://localhost:5173`.

## Using the API

All routes start with `/api/v1`. Log in through `POST /api/v1/auth/login`, then send the token on every other request:

```
Authorization: Bearer <token>
```

### Auth

| Method | Route | Access | Description |
|---|---|---|---|
| POST | `/auth/register/user` | Public | Create an account with the EMPLOYEE role |
| POST | `/auth/login` | Public | Get a login token |

### Users

| Method | Route | Access | Description |
|---|---|---|---|
| GET | `/users` | Admin | List users, with paging and sorting |
| GET | `/users/{uuid}` | Own profile or admin | Get a user |
| PUT | `/users/me` | Logged in | Update your own profile |
| PUT | `/users/me/password` | Logged in | Change your password |
| PUT | `/users/{uuid}` | Admin | Update a user and their role |
| DELETE | `/users/{uuid}` | Admin | Delete a user |

### Projects

| Method | Route | Access | Description |
|---|---|---|---|
| GET | `/projects` | Logged in | List projects, with paging and sorting |
| GET | `/projects/{uuid}` | Logged in | Get a project with its members and tasks |
| POST | `/projects` | Manager or admin | Create a project |
| PUT | `/projects/{uuid}` | Manager or admin | Update a project and replace its members |
| DELETE | `/projects/{uuid}` | Manager or admin | Delete a project and its tasks |
| POST | `/projects/{uuid}/join` | Manager or admin | Join a project |
| POST | `/projects/{uuid}/leave` | Manager or admin | Leave a project |

### Tasks

| Method | Route | Access | Description |
|---|---|---|---|
| GET | `/tasks/{uuid}` | Logged in | Get a task |
| POST | `/tasks` | Project member with manager or admin role | Create a task |
| PUT | `/tasks/{uuid}` | Task creator or admin | Update a task and replace its members |
| DELETE | `/tasks/{uuid}` | Task creator or admin | Delete a task |

List routes accept the query parameters `pageNumber`, `pageSize` and `sortBy`.

## Roles and rules

| Role id | Role | Can do |
|---|---|---|
| 1 | ADMIN | Everything, including managing users |
| 2 | MANAGER | Create, edit and delete projects and tasks |
| 3 | EMPLOYEE | View projects and tasks |

New accounts are employees. An admin changes a role with `PUT /users/{uuid}`.

- Everyone who is logged in can see all projects, their members and their tasks.
- Project creators are added as members automatically, and so are task creators.
- Every task member must also be a member of the project.
- Managers join and leave projects on their own. Employees are added and removed by managers.
- When someone leaves a project or is removed from it, they are removed from its tasks and the tasks they created are deleted.
- When a user is deleted, the projects and tasks they created pass to the admin who deleted them.
- The last admin can't be deleted or lose the ADMIN role.
- Project names are unique, and task names are unique within a project.
- Deletes are soft: rows are marked as deleted and hidden from every query.

## Tests

The tests call the real API inside the test process.

```bash
dotnet test TaskManager.Tests
```

That command uses an in-memory SQLite database and needs no setup.

To test on PostgreSQL with the real migrations and create `TaskManager.Tests/TestReport.pdf`, make sure Docker is running and use this Windows command:

```powershell
powershell -ExecutionPolicy Bypass -File TaskManager.Tests\run-tests.ps1 -Postgres
```

It starts its own temporary database on port 5434 and removes it afterwards, so your data isn't touched.

Latest result on PostgreSQL: 86 of 86 tests passing, 97.3% of lines and 94.6% of branches covered.

## Project structure

| Folder | Contents |
|---|---|
| `TaskManager/Controllers` | API endpoints |
| `TaskManager/Services` | Business rules and permission checks |
| `TaskManager/Repositories` | Database queries |
| `TaskManager/Models` | EF Core entities |
| `TaskManager/DTO` | Request and response objects |
| `TaskManager/Data` | `DbContext` and the first-admin seeder |
| `TaskManager/Migrations` | EF Core migrations |
| `TaskManager/Handlers` | Global exception handler |
| `TaskManager/Security` | Password hashing and JWT creation |
| `TaskManager.Tests` | Automated tests and the report script |
