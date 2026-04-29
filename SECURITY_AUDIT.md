# Security Audit Report — `anas-zenedge/sitechat`

**Audit Date:** 2026-04-29  
**Scope:** Full codebase — backend API, frontend dashboard, embeddable widget, configuration files, deployment artifacts

---

## Executive Summary

This codebase is a FastAPI + MongoDB RAG chatbot platform with a JavaScript embeddable widget and a single-page admin dashboard. There are **several high-severity issues that are blockers for production deployment**, most critically: real database credentials committed to the example configuration file, and multiple unprotected destructive administrative endpoints exposed to anonymous callers.

---

## Severity Legend

| Level | Meaning |
|---|---|
| 🔴 Critical | Immediate credential/data compromise; must fix before any deployment |
| 🟠 High | Unauthenticated access to destructive or sensitive endpoints |
| 🟡 Medium | Exploitable under realistic conditions; fix before production |
| 🟢 Low | Defense-in-depth; fix when practical |

---

## 🔴 Critical Findings

### CRIT-1 — Real MongoDB Credentials Committed to Repository

| Field | Detail |
|---|---|
| **Severity** | Critical |
| **File** | `backend/.env.example` (connection string line) |
| **Impact** | Full database compromise — credential theft, exfiltration, deletion |

**Description:**  
An active MongoDB Atlas connection string containing a username and password is committed verbatim into the example environment file and is therefore in the public git history. Anyone who has ever cloned this repository has these credentials. Even if they are rotated, any deployer copying `.env.example` directly will connect to the original author's database.

**Remediation:**
1. Immediately rotate/revoke the MongoDB Atlas credentials.
2. Replace the real connection string with a placeholder, e.g. `MONGODB_URL=mongodb+srv://<user>:<password>@<host>/?appName=SiteChat`.
3. Audit git history — treat these credentials as permanently compromised regardless of rotation.

---

## 🟠 High Findings

### HIGH-1 — All `/api/admin` Endpoints Completely Unauthenticated

| Field | Detail |
|---|---|
| **Severity** | High |
| **File** | `backend/app/routes/admin.py` |
| **Impact** | Any anonymous caller can destroy all platform data |

**Description:**  
Zero `Depends(require_auth)` or `Depends(require_admin)` decorators exist in this file. Every admin route is fully public:

- `DELETE /api/admin/clear-all` — deletes **all** conversations, pages, crawl jobs, and vectors
- `POST /api/admin/clear-cache` — clears caches (with potential path traversal via `LLM_CACHE_DIR`)
- `GET /api/admin/config` — reveals LLM model, chunk config, and rate limits
- `GET /api/admin/stats` and `GET /api/admin/health` — expose infrastructure state

**Remediation:**  
Add `admin: dict = Depends(require_admin)` to every route in `admin.py`. The destructive routes (`clear-all`, `clear-cache`) must require the `admin` role.

---

### HIGH-2 — All `/api/crawl` Endpoints Unauthenticated + SSRF Risk

| Field | Detail |
|---|---|
| **Severity** | High |
| **Files** | `backend/app/routes/crawl.py`, `backend/app/services/crawler.py` |
| **Impact** | Unauthenticated SSRF, index wipe, resource exhaustion |

**Description:**  
No authentication dependency exists in any route in this file. The `POST /api/crawl` endpoint accepts an arbitrary `url` parameter and instructs the server to fetch and index it without validating the destination host or URL scheme.

**SSRF vector:** `POST /api/crawl` with `url=http://169.254.169.254/latest/meta-data/` (AWS IMDSv1) or any internal service will cause the backend to fetch that URL. No IP range or scheme validation is performed in `CrawlerService` (`backend/app/services/crawler.py`).

Other unprotected routes: `POST /api/crawl/reindex`, `DELETE /api/crawl/pages/{url}`, `GET /api/crawl/pages`, `GET /api/crawl/status/{job_id}`, `GET /api/crawl/latest`.

**Remediation:**
1. Add `user: dict = Depends(require_auth)` (or `require_admin`) to every route in `crawl.py`.
2. In `CrawlerService.crawl()`, validate that `start_url` uses only `http`/`https` and that the resolved IP is not in a private/loopback/link-local range (10.x, 172.16–31.x, 192.168.x, 127.x, 169.254.x, ::1) before making any outbound request.

---

### HIGH-3 — All `/api/analytics` Endpoints Completely Unauthenticated

| Field | Detail |
|---|---|
| **Severity** | High |
| **File** | `backend/app/routes/analytics.py` |
| **Impact** | Full leakage of visitor conversation content and session IDs |

**Description:**  
No `Depends(require_auth)` exists anywhere in this file. All endpoints are publicly accessible:

- `GET /api/analytics/overview` — total conversations, messages, feedback counts
- `GET /api/analytics/popular-questions` — raw user message content (top queries)
- `GET /api/analytics/recent-conversations` — session IDs and first user messages
- `GET /api/analytics/conversations` — trend data
- `GET /api/analytics/sources-used` — source documents cited
- `GET /api/analytics/conversations-by-site` — per-site statistics

**Remediation:**  
Add `user: dict = Depends(require_auth)` to all routes. Scope non-admin users to only see their own sites' data.

---

### HIGH-4 — Schedule and QA Routes Use Optional `get_current_user` (No Auth Enforcement)

| Field | Detail |
|---|---|
| **Severity** | High |
| **Files** | `backend/app/routes/schedule.py`, `backend/app/routes/qa.py` |
| **Impact** | Unauthenticated crawl triggering, schedule modification, training data manipulation |

**Description:**  
These routes use `Depends(get_current_user)` instead of `Depends(require_auth)`. The `get_current_user` function returns `None` for unauthenticated requests and **does not raise a 401**. The `current_user` parameter is never checked for `None` inside any handler, meaning all routes in these files are effectively open to anonymous callers.

Affected routes include:
- `PUT /api/sites/{site_id}/crawl-schedule`
- `POST /api/sites/{site_id}/crawl-now`
- All Q&A training data CRUD endpoints under `/api/sites/{site_id}/qa`

**Remediation:**  
Replace `get_current_user` with `require_auth` (or `require_admin_or_user`) and add per-site ownership checks to confirm the authenticated user owns the site being modified.

---

### HIGH-5 — Rate Limit IP Spoofing via `X-Forwarded-For`

| Field | Detail |
|---|---|
| **Severity** | High |
| **File** | `backend/app/core/security.py`, lines 202–216 |
| **Impact** | Bypass of all `slowapi` rate limits — enables brute-force login, chat flooding |

**Description:**  
The `get_client_ip` helper blindly trusts the `X-Forwarded-For` header provided by the client:

```python
forwarded_for = request.headers.get("x-forwarded-for")
if forwarded_for:
    return forwarded_for.split(",")[0].strip()
```

Without a trusted reverse proxy that strips/overwrites this header, any attacker can send `X-Forwarded-For: 1.2.3.4` to masquerade as any IP, bypassing all rate limits (login: 10/min, chat: 20/min, handoff: 5/min, lead capture: 5/min).

**Remediation:**  
Trust `X-Forwarded-For` only when the connection originates from a known trusted proxy IP. Use Starlette's `ProxyHeadersMiddleware` with `trusted_hosts` set to the proxy's address, or fall back to `request.client.host` when no trusted proxy is detected.

---

## 🟡 Medium Findings

### MED-1 — XSS: LLM Responses Rendered as Unsanitized HTML in Dashboard Chat

| Field | Detail |
|---|---|
| **Severity** | Medium |
| **File** | `frontend/js/app.js`, `addMessage()` function |
| **Impact** | Stored XSS in admin dashboard if LLM or crawled content contains HTML/script |

**Description:**  
The LLM response content is set directly as `innerHTML` without HTML escaping:

```js
let html = content;           // raw LLM answer
msg.innerHTML = html;         // rendered as HTML
```

Source titles and URLs are also interpolated without escaping:

```js
sources.map(s => `<a href="${s.url}" ...>${s.title || s.url}</a>`)
```

If crawled pages or LLM output contain HTML/script tags (either from indexed content or prompt injection), they execute in the admin dashboard context.

**Remediation:**  
Use `textContent` for LLM response content. For Markdown rendering, use a dedicated library such as `marked.js` combined with `DOMPurify` for sanitization. Escape source URL and title through `escapeHtml()` before interpolation.

---

### MED-2 — XSS: Widget Markdown Renderer Injects HTML Without Sanitization

| Field | Detail |
|---|---|
| **Severity** | Medium |
| **File** | `frontend/src/widget/chatbot.js`, `markdownToHtml()` function and `appendMessage()` |
| **Impact** | Stored XSS on every website the widget is embedded in |

**Description:**  
The widget's `markdownToHtml` function applies regex replacements that inject content directly into HTML without escaping it first:

```js
G.replace(/\*\*(.*?)\*\*/g, "<strong>$1</strong>")
 .replace(/`([^`]+)`/g, "<code>$1</code>")
```

Input such as `**<img src=x onerror=alert(1)>**` produces `<strong><img src=x onerror=alert(1)></strong>`. The resulting HTML is then set via `N["innerHTML"] = P`. Source URLs and titles from the API are also interpolated directly without escaping.

**Remediation:**  
HTML-escape all dynamic values before inserting into template strings. Alternatively, sanitize the final HTML output with `DOMPurify` before setting `innerHTML`.

---

### MED-3 — XSS: `customBrandingText` Injected via `innerHTML` in Widget

| Field | Detail |
|---|---|
| **Severity** | Medium |
| **File** | `frontend/src/widget/chatbot.js`, `updateBrandingFooter()` |
| **Impact** | Admin-controlled stored XSS on all embedding customer websites |

**Description:**  
The `customBrandingText` value fetched from the public site config API is set via `innerHTML`:

```js
H["innerHTML"] = config.customBrandingText
```

Any admin (or compromised admin account) who sets `custom_branding_text` in the site config can inject arbitrary HTML or JavaScript into every page on which the widget is embedded.

**Remediation:**  
Use `textContent` or sanitize with `DOMPurify` before setting `innerHTML`.

---

### MED-4 — Password Complexity Requirement Not Enforced

| Field | Detail |
|---|---|
| **Severity** | Medium |
| **Files** | `backend/app/config.py` (`REQUIRE_PASSWORD_COMPLEXITY`), `backend/app/core/security.py` `validate_password()` |
| **Impact** | Weak passwords accepted for all user roles |

**Description:**  
`REQUIRE_PASSWORD_COMPLEXITY=True` is documented, but the `validate_password` function only enforces minimum length:

```python
def validate_password(password: str) -> tuple[bool, str]:
    if len(password) < settings.MIN_PASSWORD_LENGTH:
        return False, f"Password must be at least {settings.MIN_PASSWORD_LENGTH} characters"
    return True, ""
```

Eight-character all-lowercase passwords are accepted.

**Remediation:**  
When `REQUIRE_PASSWORD_COMPLEXITY=True`, enforce at least one uppercase letter, one lowercase letter, one digit, and one special character.

---

### MED-5 — Weak Default JWT Secret

| Field | Detail |
|---|---|
| **Severity** | Medium |
| **File** | `backend/app/config.py` |
| **Impact** | Admin JWT forgery if default secret is not changed |

**Description:**  
`JWT_SECRET` defaults to `"CHANGE-THIS-SECRET-IN-PRODUCTION"`. While a warning is logged at startup, the application starts and functions normally with this known secret. Anyone who knows the default can mint valid admin tokens.

**Remediation:**  
The application should refuse to start in `ENVIRONMENT=production` mode when `is_jwt_secret_secure` returns `False`.

---

### MED-6 — JWT Token Stored in `localStorage`

| Field | Detail |
|---|---|
| **Severity** | Medium |
| **File** | `frontend/js/app.js` |
| **Impact** | Token exfiltration via any XSS vulnerability on the dashboard page |

**Description:**  
The bearer token is persisted in `localStorage`:

```js
localStorage.setItem('token', data.access_token);
localStorage.setItem('user', JSON.stringify(currentUser));
```

Any XSS on the dashboard page allows a script to read and exfiltrate the admin token.

**Remediation:**  
For high-security deployments, prefer `HttpOnly` cookies managed by the server. If `localStorage` is retained, prioritize eliminating all XSS vectors (MED-1, MED-2, MED-3) first.

---

### MED-7 — Unauthenticated `GET`/`DELETE /api/chat/history/{session_id}`

| Field | Detail |
|---|---|
| **Severity** | Medium |
| **File** | `backend/app/routes/chat.py`, lines 125–171 |
| **Impact** | Conversation content disclosure or deletion for any guessable session ID |

**Description:**  
Any caller with a known `session_id` can read or clear the full conversation history for that session. Session IDs are 13-character base36 strings generated from `Math.random()`.

**Remediation:**  
Require authentication, or gate access on a session-bound secret token issued at session creation.

---

### MED-8 — Unauthenticated `GET /api/chat/sessions` Exposes All Session IDs

| Field | Detail |
|---|---|
| **Severity** | Medium |
| **File** | `backend/app/routes/chat.py`, lines 174–198 |
| **Impact** | Session enumeration enabling MED-7 exploitation at scale |

**Description:**  
`GET /api/chat/sessions?limit=N` returns session IDs for all conversations without any authentication.

**Remediation:**  
Require authentication.

---

### MED-9 — Regex Injection in Vector Store Delete

| Field | Detail |
|---|---|
| **Severity** | Medium |
| **File** | `backend/app/routes/sites.py`, line 97 |
| **Impact** | Over-deletion of vector data from other sites when a site URL contains regex metacharacters |

**Description:**  
```python
await vector_store.delete_by_metadata({"source_url": {"$regex": f"^{url}"}})
```

The site `url` is inserted directly into a MongoDB `$regex` query without escaping. A URL such as `http://a.com` matches `http://axcom` because `.` is a regex wildcard.

**Remediation:**  
```python
import re
await vector_store.delete_by_metadata({"source_url": {"$regex": f"^{re.escape(url)}"}})
```

---

### MED-10 — OpenAPI Docs Publicly Exposed in All Environments

| Field | Detail |
|---|---|
| **Severity** | Medium |
| **File** | `backend/app/main.py` |
| **Impact** | Full endpoint map provided to unauthenticated attackers |

**Description:**  
`/api/docs` and `/api/redoc` are always enabled. There is no conditional to disable them in production mode.

**Remediation:**  
Set `docs_url=None, redoc_url=None` when `settings.is_production` is `True`, or restrict access to an internal IP allowlist.

---

## 🟢 Low Findings

### LOW-1 — Python Exception Messages Leaked in 500 Responses

**File:** Multiple route files  
**Details:** `raise HTTPException(status_code=500, detail=str(e))` exposes internal file paths, variable values, and stack details to clients.  
**Remediation:** Log full exceptions server-side; return a generic `"Internal server error"` string to the client in production.

---

### LOW-2 — Admin Email Exposed in Public HTML

**File:** `backend/app/public_html.py`, line 19  
**Details:** `settings.ADMIN_EMAIL` is injected into public-facing `landing.html` and `login.html` via the `__ADMIN_EMAIL__` placeholder, leaking the admin account email.  
**Remediation:** Remove the email substitution from public pages, or display it only after authentication.

---

### LOW-3 — Missing `rel="noopener noreferrer"` on External Links

**Files:** `frontend/js/app.js`, `frontend/src/widget/chatbot.js`  
**Details:** Links with `target="_blank"` are inserted without `rel="noopener noreferrer"`, allowing opened pages to access the opener's context via `window.opener`.  
**Remediation:** Add `rel="noopener noreferrer"` to all dynamically created `target="_blank"` anchors.

---

### LOW-4 — Unquoted `Content-Disposition` Filename

**File:** `backend/app/routes/leads.py`, line 168  
**Details:** `f"attachment; filename={filename}"` — the filename is not RFC 6266-quoted.  
**Remediation:** Use `f'attachment; filename="{filename}"'`.

---

### LOW-5 — Unpinned Dependency Versions (Supply Chain Risk)

**File:** `backend/requirements.txt`  
**Details:** All dependencies use unbounded `>=` version constraints with no lock file. Future `pip install` could pull compromised or breaking versions.  
**Remediation:** Generate a pinned `requirements.lock` via `pip freeze` and run `pip-audit` or enable Dependabot to monitor for vulnerabilities.

---

### LOW-6 — Site IDs Generated with Truncated MD5

**File:** `backend/app/routes/embed.py`, lines 44–46  
**Details:** `hashlib.md5(url.encode()).hexdigest()[:12]` — the 12-character truncation increases collision probability across many sites.  
**Remediation:** Use `secrets.token_urlsafe(8)` or a UUID-based ID instead.

---

## Unauthenticated Endpoints Summary

| Endpoint | Highest Impact | Should Require Auth? |
|---|---|---|
| `POST /api/crawl` | SSRF, resource abuse | ✅ Yes |
| `POST /api/crawl/reindex` | Resource abuse | ✅ Yes |
| `DELETE /api/crawl/pages/{url}` | Data deletion | ✅ Yes |
| `GET /api/crawl/pages` | Data exposure | ✅ Yes |
| `DELETE /api/admin/clear-all` | **Destroy all platform data** | ✅ Yes (admin) |
| `POST /api/admin/clear-cache` | Cache clearing | ✅ Yes (admin) |
| `GET /api/admin/config` | Config disclosure | ✅ Yes |
| `GET /api/admin/health` | Infrastructure disclosure | ✅ Yes |
| `GET /api/admin/stats` | Statistics disclosure | ✅ Yes |
| `GET /api/analytics/*` (all 6 routes) | Conversation content leakage | ✅ Yes |
| `GET /api/chat/history/{session_id}` | Conversation content exposure | ✅ Yes |
| `DELETE /api/chat/history/{session_id}` | Conversation deletion | ✅ Yes |
| `GET /api/chat/sessions` | Session ID enumeration | ✅ Yes |
| `GET /api/sites/{id}/crawl-schedule` | Config exposure | ✅ Yes |
| `PUT /api/sites/{id}/crawl-schedule` | Config modification | ✅ Yes |
| `POST /api/sites/{id}/crawl-now` | Crawl trigger | ✅ Yes |
| `POST/GET/PUT/DELETE /api/sites/{id}/qa` | Training data modification | ✅ Yes |

---

## Final Recommendation

**⛔ This repository is NOT safe to deploy as-is.** The following issues must be fixed before any internet-facing deployment:

1. **🔴 Rotate the MongoDB credentials immediately** (CRIT-1) — they are permanently in the public git history.
2. **🟠 Add authentication to all admin, crawl, and analytics routes** (HIGH-1, HIGH-2, HIGH-3) — any anonymous internet user can currently destroy all data or use the server for SSRF.
3. **🟠 Replace `get_current_user` with `require_auth`** in `schedule.py` and `qa.py` (HIGH-4).
4. **🟠 Harden IP trust logic for rate limiting** (HIGH-5) before exposing to the internet.

After those four blockers are resolved, this codebase can be made reasonably secure by working through the medium-severity findings. The overall architecture is sound — JWT-based authentication, bcrypt password hashing, CORS configuration, `TrustedHostMiddleware`, honeypot fields on public forms, SRI hash support for the widget script, and startup security-configuration warnings are all positive signals. The missing authentication guards on destructive endpoints are the primary blockers for production deployment.
