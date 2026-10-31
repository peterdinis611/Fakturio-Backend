# Fakturio backend

> **WIP** — na aplikácii sa stále pracuje. API, schémy aj správanie sa môžu meniť.

ASP.NET Core API (JWT, SQLite) pre fakturáciu — páruje sa s Next.js frontendom.

## Spustenie

```bash
dotnet run
```

Predvolene počúva na [http://localhost:5148](http://localhost:5148). Swagger je dostupný v Development.

## Stack

- .NET 10 / ASP.NET Core
- EF Core + SQLite
- JWT auth, FluentValidation, Serilog
- QuestPDF (PDF), HybridCache
