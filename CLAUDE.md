# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

ASP.NET Core Web API (.NET 10, `net10.0`) for a task/project manager, backed by PostgreSQL via EF Core (Npgsql). Single project `TaskManager/` in the `TaskManager.slnx` solution (new XML solution format). AutoMapper and Serilog are referenced but not yet wired up. No test project exists yet.

## Commands

```bash
dotnet build TaskManager.slnx
dotnet run --project TaskManager                      # http://localhost:5244
dotnet run --project TaskManager --launch-profile https  # https://localhost:7144
```

- OpenAPI document is served at `/openapi/v1.json` in Development only.
- `TaskManager/TaskManager.http` holds ad-hoc HTTP requests (VS/Rider HTTP client).
- EF migrations: `Microsoft.EntityFrameworkCore.Design` and the `dotnet-ef` tool are not installed yet; add them before running `dotnet ef migrations add <Name> --project TaskManager`.

## Architecture

Layered by folder within one project (namespace = `TaskManager.<Folder>`):

- `Models/` – EF entities. `User`, `Project`, `ProjectTask` inherit `BaseEntity` (audit timestamps + soft delete via `IsDeleted`/`DeletedAt`) and carry both an `int Id` (PK) and a `Guid Uuid` (public-facing identifier, unique-indexed). `Role` and `Capability` are lookup entities without `BaseEntity`.
- `Enums/` – `WorkStatus` and `ProjectTaskPriority`, explicitly numbered starting at 1.
- `Data/TaskMangerContext.cs` – the `DbContext` (note the class/file name is spelled `TaskManger`). Relationships, max lengths and named indexes/constraints are configured with the Fluent API in `OnModelCreating`, using explicit names (`FK_<Table>_<Ref>_<Col>`, `IX_...`, `UQ_...`).
- `Controllers/` – API controllers (`[ApiController]`, attribute routing).

Domain model:
- `User` → one `Role`; `Role` ↔ `Capability` is many-to-many through the `RolesCapabilities` join table (role-based permissions).
- `Project` and `ProjectTask` each have a creator (`UserId`/`User`) plus a many-to-many `Users` collection of assignees/members. `User` exposes both sides as `Projects`/`CreatedProjects` and `Tasks`/`CreatedTasks`.

## Current state

Work in progress: the `DbContext` Fluent configuration is still being written, and the database is intentionally not wired up yet (no `AddDbContext` in `Program.cs`, no connection string, no migrations). There are no controllers yet.

Because `User` has two navigations to each of `Project` and `ProjectTask` (creator vs. members), every such relationship must be mapped explicitly in `OnModelCreating`. Follow the pattern used for `Project`: `HasOne(x => x.User).WithMany(u => u.Created...)` plus `HasMany(x => x.Users).WithMany(u => u....)`.
