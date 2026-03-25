# Boilerplate CQRS

A production-ready full-stack boilerplate with **.NET 10 Backend** (Clean Architecture + CQRS) and **React/Vite Frontend** (TypeScript + shadcn/ui). Built to be renamed and used as a starter for any project.

## Tech Stack

### Backend
- **.NET 10** with Clean Architecture (Domain, Application, Infrastructure, API)
- **CQRS** with MediatR (Commands, Queries, Pipeline Behaviors)
- **PostgreSQL** with Entity Framework Core
- **Redis** for distributed caching
- **RabbitMQ** with MassTransit for messaging
- **JWT Authentication** with refresh tokens
- **Permission-based Authorization** with custom policy provider
- **FluentValidation** for request validation
- **Serilog** for structured logging
- **BCrypt** password hashing

### Frontend
- **React 19** + TypeScript + Vite
- **shadcn/ui** component library (Radix UI + Tailwind CSS 4)
- **TanStack React Query** for server state management
- **Zustand** for client state management
- **React Hook Form** + Zod validation
- **i18next** with English, Arabic, Kurdish (RTL support)
- **Axios** with auth/refresh/error interceptors

## Features

### Authentication & Authorization
- Login / Register / Logout
- Email verification with OTP (sent via SMTP)
- Forgot password / Reset password with OTP
- JWT access + refresh token rotation
- Permission-based route guards (BE + FE)
- Account lockout after failed login attempts

### User Management
- User CRUD with role assignment
- Status management: Activate, Suspend, Deactivate, Unlock
- Profile editing with email re-verification

### Role & Permission Management
- Role CRUD with permission matrix
- System role protection (cannot edit/delete)
- Permission grouping by module

### Audit Logs
- Automatic change tracking on all entities
- JSON diff of old/new values
- Filterable audit log viewer (entity type, action, date, user)

### Email & SMS
- **SMTP email service** with HTML templates (verification, password reset)
- **Twilio SMS service** (optional, toggle via config)
- **OTP service** with rate limiting and cache-based storage

### Infrastructure
- Docker Compose for local development (PostgreSQL, Redis, RabbitMQ, Mailpit)
- Health checks for all external services
- API versioning + Swagger/OpenAPI
- Rate limiting per endpoint
- CORS with explicit header whitelist
- Correlation ID middleware for request tracing
- Multi-tenancy with subdomain routing (`acme.starter.com`)
- Shared cookie auth for cross-subdomain SSO
- Dark/Light/System theme with RTL support

## Quick Start

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- PostgreSQL (local or Docker)
- Redis (local or Docker)

### Using Docker Compose (recommended for dependencies)

```bash
cd boilerplateBE
docker compose up -d
```

This starts: PostgreSQL (5432), Redis (6379), RabbitMQ (5672/15672), Mailpit (1025/8025)

### Backend

```bash
cd boilerplateBE

# Create initial migration
dotnet ef migrations add InitialCreate \
  --project src/Starter.Infrastructure \
  --startup-project src/Starter.Api

# Run (migrations + seeding happen automatically in Development)
dotnet run --project src/Starter.Api
```

API runs at **http://localhost:5000** | Swagger at **http://localhost:5000/swagger**

### Frontend

```bash
cd boilerplateFE
npm install
npm run dev
```

Frontend runs at **http://localhost:3000**

### Default Login

| Field | Value |
|-------|-------|
| Email | `superadmin@starter.com` |
| Password | `Admin@123456` |

### Mailpit (dev email viewer)

Open **http://localhost:8025** to see verification and password reset emails.

## Creating a New Project

Use the rename script to create a new project from this boilerplate:

### PowerShell (Windows)

```powershell
.\scripts\rename.ps1 -Name "MyApp"
.\scripts\rename.ps1 -Name "MyApp" -OutputDir "C:\Projects"
```

### Bash (Linux/macOS)

```bash
./scripts/rename.sh MyApp
./scripts/rename.sh MyApp /home/user/projects
```

This creates a new directory with:
```
MyApp/
  MyApp-BE/    # Backend with all namespaces renamed
  MyApp-FE/    # Frontend with all config/keys renamed
```

All references to "Starter"/"starter" are replaced with your project name, including:
- C# namespaces, project names, solution file
- Database name, connection strings
- JWT issuer/audience
- Docker container names
- localStorage keys, package name
- Email templates, branding text

## Project Structure

### Backend

```
boilerplateBE/
├── src/
│   ├── Starter.Domain/           # Entities, Value Objects, Events, Enums
│   ├── Starter.Application/      # CQRS Commands/Queries, Interfaces, Behaviors
│   ├── Starter.Infrastructure/   # EF Core, Services (Email, OTP, SMS, Cache)
│   ├── Starter.Infrastructure.Identity/  # Auth, JWT, Password services
│   ├── Starter.Api/              # Controllers, Middleware, Configuration
│   └── Starter.Shared/           # Result pattern, DTOs, Constants
├── docker-compose.yml
└── Dockerfile
```

### Frontend

```
boilerplateFE/
├── src/
│   ├── app/           # App entry, providers
│   ├── components/    # ui/ (shadcn), common/, layout/, guards/
│   ├── config/        # API endpoints, routes, query config
│   ├── constants/     # Permissions, languages
│   ├── features/      # auth, dashboard, users, roles, audit-logs
│   ├── hooks/         # usePermissions, useClickOutside, useDebounce
│   ├── i18n/          # Translations (en, ar, ku)
│   ├── lib/           # Axios client, React Query, validation schemas
│   ├── routes/        # Router with permission guards
│   ├── stores/        # Zustand (auth, ui)
│   ├── styles/        # Tailwind CSS + theme variables
│   ├── types/         # TypeScript interfaces
│   └── utils/         # Storage helpers
└── package.json
```

## Configuration

### Backend (`appsettings.Development.json`)

| Setting | Default | Description |
|---------|---------|-------------|
| `ConnectionStrings:DefaultConnection` | `localhost:5432/starterdb` | PostgreSQL |
| `ConnectionStrings:Redis` | `localhost:6379` | Redis cache |
| `JwtSettings:Secret` | Dev key | JWT signing key (override in production!) |
| `SmtpSettings:Host/Port` | `localhost:1025` | Mailpit SMTP |
| `TwilioSettings:Enabled` | `false` | Enable SMS via Twilio |
| `RabbitMQ:Enabled` | `false` | Enable RabbitMQ (uses in-memory in dev) |

### Frontend (`.env`)

| Variable | Default | Description |
|----------|---------|-------------|
| `VITE_API_BASE_URL` | `http://localhost:5000/api/v1` | Backend API URL |
| `VITE_APP_NAME` | `Starter` | App name shown in UI |

## Theme Customization

Edit `boilerplateFE/src/styles/index.css` to change the color scheme:
- `--color-primary-*` — Primary color scale (default: blue)
- `--color-accent-*` — Accent color scale (default: emerald)
- Dark mode overrides in `.dark { }` block

## Subdomain Tenant Routing

Each tenant gets a branded URL like `acme.starter.com`. The frontend detects the subdomain and scopes all data to that tenant. The API stays on a single domain.

### How It Works

1. User visits `starter.com` and logs in (same login page for everyone)
2. After login, tenant users are automatically redirected to `acme.starter.com/dashboard`
3. Platform admins stay on `starter.com/dashboard`
4. Auth tokens are stored in shared cookies (`domain=.starter.com`) so they work across all subdomains

### Production Setup

1. **DNS**: Add a wildcard record: `*.starter.com → your-server-IP`
2. **SSL**: Get a wildcard certificate for `*.starter.com` (Let's Encrypt, Cloudflare, etc.)
3. **Reverse proxy**: Configure Nginx/Caddy to route all subdomains to the same frontend
4. **Backend config**: Set `AppSettings:BaseDomain` to your domain (e.g., `starter.com`)
5. **Frontend build**: Set `VITE_BASE_DOMAIN=starter.com` when building

### Development

Subdomain routing works locally:
- `acme.localhost:3000` works in Chrome/Firefox natively
- `localhost:3000?tenant=acme` works as a fallback in any browser

## License

MIT
