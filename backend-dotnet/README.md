# SiteChat ASP.NET Core Backend

This directory contains a new C# ASP.NET Core port of the existing `backend\` FastAPI stack. The Python backend remains in place; this stack is intended to run side-by-side while preserving the existing API contract.

## Run

```powershell
cd backend-dotnet
dotnet run --project src\SiteChat.Backend.Api\SiteChat.Backend.Api.csproj
```

Configuration is read from `appsettings.json`, environment-specific appsettings files, and environment variables. For production, set a strong `SiteChat__Jwt__Secret`, `SiteChat__MongoDb__Url`, `SiteChat__Rag__OpenRouterApiKey`, explicit CORS origins, trusted hosts, and trusted proxy IPs.

Chat completions and page embeddings are now routed through OpenRouter's OpenAI-compatible API. Configure `SiteChat__Rag__LlmModel` and `SiteChat__Rag__EmbeddingModel` to choose the chat and embedding models that fit your deployment.

## Security hardening included

- Admin, crawl, analytics, chat history/session, schedule, and QA endpoints require authentication/authorization.
- Crawler URLs are validated against SSRF targets before outbound requests.
- `X-Forwarded-For` is trusted only when the immediate peer is a configured trusted proxy.
- Password complexity is enforced when configured.
- Production startup is blocked when the JWT secret is weak or default.
- Swagger is disabled in production.
- Security headers, request-size limits, suspicious user-agent blocking, and safe production exception responses are enabled.

## Current parity notes

The ASP.NET Core stack preserves the existing route surface and MongoDB document shapes, but several provider-heavy features are intentionally isolated behind interfaces for incremental completion: FAISS/LangChain vector retrieval, document parsing beyond upload metadata, handoff streaming persistence, and trigger analytics aggregation. Data-backed endpoints for auth, sites, conversations, crawl jobs/pages, admin health/stats/clear-all, analytics overview, and static frontend hosting are implemented.
