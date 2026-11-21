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

## Caching

HybridCache (L1 memory + optional Redis L2) tags entries **per company**:

- `reports:{companyId}` — report overview / aging / VAT / …
- `company:{companyId}` — company settings / summary / bank

Invoice, customer, and company writes invalidate only that tenant’s tags. Admin `POST /api/cache/invalidate` still flushes all known company tags plus legacy global `reports` / `company` tags.

| Setting | Default | Notes |
| --- | --- | --- |
| `Cache:Enabled` | `true` | Master switch |
| `Cache:ReportsSeconds` | `60` | Distributed TTL; L1 uses `min(ttl, 30s)` |
| `Cache:CompanySeconds` | `300` | Distributed TTL; L1 uses `min(ttl, 60s)` |
| `Cache:IntegrationsSeconds` | `3600` | Reserved for integration lookups |

## Rate limiting

Sliding-window policies (process-local; multi-instance fairness needs a later Redis limiter):

| Policy | Endpoints | Default | Partition |
| --- | --- | --- | --- |
| `auth` | login / register | 10 / 60s | IP |
| `api` | authenticated `/api/*` (via `ApiControllerBase`) | 120 / 60s | `user:{sub}` or IP |
| `public` | public invoice links | 60 / 60s | IP |
| `integrations` | registry / integrations | 30 / 60s | user or IP |
| `password` | `POST …/password-check` | 5 / 60s | IP |

Health (`/api/health`) is excluded. Configure via `RateLimiting:ApiPermitLimit`, `ApiWindowSeconds`, `PasswordPermitLimit`, etc. Allowed responses may include `X-RateLimit-Limit`; 429 responses include `Retry-After`, `code=rate_limited`, and `X-RateLimit-Remaining: 0`.
