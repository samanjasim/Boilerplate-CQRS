# Observability Setup Guide

OpenTelemetry-based observability for the Starter API — traces, metrics, and logs across Dev, Staging, and Production.

## Architecture Overview

```
App (.NET 10)                    Infrastructure
+-----------------------+       +------------------+
| ASP.NET Core          |       |                  |
| EF Core               | OTLP  | Jaeger v2        |
| MediatR               |------>| (OTel Collector) |
| HttpClient            | HTTP   |   Port 4318      |
| Serilog               | Proto  |                  |
+-----------------------+ buf   +--------+---------+
                                         |
                                  spanmetrics
                                  connector
                                         |
                                +--------v---------+
                                | Prometheus        |
                                | scrapes :8889     |
                                | (span metrics)    |
                                | scrapes :8888     |
                                | (collector stats) |
                                +--------+---------+
                                         |
                                +--------v---------+
                                | Jaeger Monitor    |
                                | tab queries       |
                                | Prometheus        |
                                +------------------+
```

## What Each UI Shows

### Jaeger UI (http://localhost:16686)

| Tab | What It Shows | How To Use |
|-----|--------------|------------|
| **Search** | Individual request traces with full span waterfall | Select `starter-api` from Service dropdown, click "Find Traces". Click any trace to see the request breakdown: ASP.NET → MediatR → EF Core SQL |
| **Monitor** | RED metrics dashboard (Rate, Errors, Duration) | Select your service from the Service dropdown. Shows P50/P75/P95 latency, error rate %, and request rate per operation. **Takes ~30s after first traffic to show data.** |
| **Compare** | Side-by-side trace comparison | Select two traces from Search results, then click Compare |
| **System Architecture** | Service dependency graph | Shows which services call which — useful with microservices |

### Prometheus UI (http://localhost:9090)

| Page | What It Shows | How To Use |
|------|--------------|------------|
| **Query** | Raw PromQL metric exploration | Type a query like `traces_span_metrics_calls_total` and click Execute. Switch between Table and Graph tabs. |
| **Status > Targets** | Scrape target health | Verify both `aggregated-trace-metrics` and `jaeger-collector-metrics` show as UP |

### Pre-computed Metrics (Recording Rules)

These metrics are automatically computed every 15 seconds by Prometheus recording rules (`prometheus-rules.yml`). They are always ready — no manual queries needed.

Go to http://localhost:9090, paste any metric name below, click **Execute**, switch to **Graph** tab.

#### Request Rate

| Metric | What It Shows |
|--------|--------------|
| `api:request_rate:5m` | Requests/sec per service + operation (5-min window) |
| `api:request_rate_total:5m` | Requests/sec per service, all operations combined |

**Example:** `api:request_rate:5m{service_name="starter-api"}`

#### Error Rate

| Metric | What It Shows |
|--------|--------------|
| `api:error_rate:5m` | Error percentage per service + operation (0.0 to 1.0) |
| `api:error_rate_total:5m` | Error percentage per service, all operations combined |

**Example:** `api:error_rate:5m{service_name="starter-api"} * 100` (multiply by 100 for %)

#### Latency Percentiles

| Metric | What It Shows |
|--------|--------------|
| `api:latency_p50:5m` | Median latency in ms per operation |
| `api:latency_p95:5m` | P95 latency in ms per operation |
| `api:latency_p99:5m` | P99 latency in ms per operation |

**Example:** `api:latency_p95:5m{service_name="starter-api"}`

#### Counts (Last Hour)

| Metric | What It Shows |
|--------|--------------|
| `api:request_count:1h` | Total requests per operation in the last hour |
| `api:error_count:1h` | Total errors per operation in the last hour |

**Example:** `api:request_count:1h{service_name="starter-api"}`

### Raw PromQL Queries (Advanced)

For custom analysis beyond the pre-computed metrics:

```promql
# Request rate per operation (custom window)
rate(traces_span_metrics_calls_total{service_name="starter-api"}[10m])

# P95 latency per operation (custom window)
histogram_quantile(0.95, rate(traces_span_metrics_duration_milliseconds_bucket{service_name="starter-api"}[10m]))

# Error rate as percentage
rate(traces_span_metrics_calls_total{service_name="starter-api", status_code="STATUS_CODE_ERROR"}[5m])
/ rate(traces_span_metrics_calls_total{service_name="starter-api"}[5m]) * 100

# Top 5 slowest operations
topk(5, api:latency_p95:5m{service_name="starter-api"})

# Operations with error rate > 5%
api:error_rate:5m{service_name="starter-api"} > 0.05
```

### Log Files

Logs are written to `src/Starter.Api/logs/` with daily rolling files (7-day retention). Every log line includes a `TraceId` that correlates directly to Jaeger traces:

```
2026-03-28 04:04:30.387 [INF] Request completed: POST /api/v1/Auth/login | Status: 200 | Duration: 1323ms | TraceId: fc49054fd74d6d88a36b5d2ee2eca878
```

Copy the TraceId into Jaeger's "Lookup by Trace ID" search box (top right) to jump directly to that trace.

---

## Environment Configuration

### Development (localhost)

Already configured. Run:

```bash
cd boilerplateBE
docker compose up -d jaeger prometheus
cd src/Starter.Api && dotnet run --launch-profile http
```

**URLs:**
- App: http://localhost:5000/swagger
- Jaeger: http://localhost:16686
- Prometheus: http://localhost:9090

**appsettings.Development.json:**
```json
{
  "OpenTelemetry": {
    "Enabled": true
  }
}
```

**appsettings.json (base config):**
```json
{
  "OpenTelemetry": {
    "Enabled": false,
    "ServiceName": "starter-api",
    "OtlpEndpoint": "http://127.0.0.1:4318"
  }
}
```

**Files required alongside docker-compose.yml:**
- `jaeger-config.yaml` — Jaeger v2 OTel Collector config
- `prometheus.yml` — Prometheus scrape config

### Staging

Staging mirrors production architecture but with relaxed retention.

**docker-compose.staging.yml** (add to your staging host):

```yaml
services:
  jaeger:
    image: jaegertracing/jaeger:latest
    networks:
      backend:
        aliases: [spm_metrics_source]
    volumes:
      - ./jaeger-config.yaml:/etc/jaeger/config.yaml
    command: ["--config", "/etc/jaeger/config.yaml"]
    ports:
      - "16686:16686"
      - "4318:4318"
      - "8888:8888"
      - "8889:8889"
    restart: unless-stopped

  prometheus:
    image: prom/prometheus:v3.10.0
    networks:
      - backend
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus-data:/prometheus
    ports:
      - "9090:9090"
    restart: unless-stopped

networks:
  backend:

volumes:
  prometheus-data:
```

**jaeger-config.yaml** — same as dev, but change log level:

```yaml
# Change this line from dev config:
    logs:
      level: warn    # was: info
```

**prometheus.yml** — change scrape interval for staging:

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: aggregated-trace-metrics
    static_configs:
      - targets: ['jaeger:8889']
  - job_name: jaeger-collector-metrics
    static_configs:
      - targets: ['jaeger:8888']
```

**appsettings.Staging.json:**

```json
{
  "OpenTelemetry": {
    "Enabled": true,
    "OtlpEndpoint": "http://jaeger:4318"
  }
}
```

> Note: When the app runs inside Docker alongside Jaeger, use `http://jaeger:4318` (Docker DNS). When it runs on the host, use `http://127.0.0.1:4318`.

### Production

Production requires durable storage (Elasticsearch or Cassandra for Jaeger, persistent volume for Prometheus) and should be secured behind a reverse proxy.

**Key differences from dev/staging:**

| Aspect | Dev | Staging | Production |
|--------|-----|---------|------------|
| Jaeger storage | In-memory (100K traces) | In-memory (100K traces) | Elasticsearch / Cassandra |
| Prometheus retention | Default (15 days) | Default (15 days) | 30-90 days |
| Jaeger log level | info | warn | warn |
| Spanmetrics flush | 15s | 15s | 60s |
| Prometheus scrape interval | 15s | 15s | 15s |
| Access | Open | VPN/internal | VPN + auth |

**appsettings.Production.json:**

```json
{
  "OpenTelemetry": {
    "Enabled": true,
    "OtlpEndpoint": "http://otel-collector:4318"
  }
}
```

**jaeger-config.yaml for production with Elasticsearch:**

```yaml
service:
  extensions: [jaeger_storage, jaeger_query]
  pipelines:
    traces:
      receivers: [otlp]
      processors: [batch]
      exporters: [jaeger_storage_exporter, spanmetrics]
    metrics/spanmetrics:
      receivers: [spanmetrics]
      exporters: [prometheus]
  telemetry:
    resource:
      service.name: jaeger
    metrics:
      level: detailed
      readers:
        - pull:
            exporter:
              prometheus:
                host: 0.0.0.0
                port: 8888
    logs:
      level: warn

extensions:
  jaeger_query:
    storage:
      traces: es_store
      metrics: prom_store
    http:
      endpoint: 0.0.0.0:16686
  jaeger_storage:
    backends:
      es_store:
        elasticsearch:
          server_urls: http://elasticsearch:9200
          index_prefix: jaeger
          num_shards: 3
          num_replicas: 1
    metric_backends:
      prom_store:
        prometheus:
          endpoint: http://prometheus:9090
          normalize_calls: true
          normalize_duration: true

connectors:
  spanmetrics:
    metrics_flush_interval: 60s

receivers:
  otlp:
    protocols:
      grpc:
      http:
        endpoint: "0.0.0.0:4318"

processors:
  batch:

exporters:
  jaeger_storage_exporter:
    trace_storage: es_store
  prometheus:
    endpoint: "0.0.0.0:8889"
```

> Replace `http://elasticsearch:9200` with your Elasticsearch cluster URL. Alternatively, use `cassandra` backend — see [Jaeger storage docs](https://www.jaegertracing.io/docs/latest/deployment/).

---

## Troubleshooting

### Monitor tab shows "No data"

1. **Check service dropdown** — Make sure you selected your service (e.g., `starter-api`), not `jaeger`
2. **Wait 30 seconds** — Spanmetrics flush every 15s, Prometheus scrapes every 15s
3. **Generate traffic** — The Monitor tab needs active request data
4. **Check Prometheus targets** — Go to http://localhost:9090 → Status → Targets. Both jobs should show "UP"
5. **Check Prometheus metrics** — Query `traces_span_metrics_calls_total` — if empty, Jaeger isn't producing spanmetrics

### Traces not appearing in Jaeger Search

1. **Check OpenTelemetry is enabled** — Verify `OpenTelemetry:Enabled` is `true` in your active appsettings
2. **Check OTLP endpoint** — Use `http://127.0.0.1:4318` for localhost (NOT `localhost` — IPv6 causes issues on Windows Docker)
3. **Check Jaeger container** — `docker logs starter-jaeger` should show "Everything is ready"
4. **Check app logs** — Look for `TraceId:` in log output — if present, traces are being created but may not be exported

### Prometheus is empty

1. **Go to Status > Targets** — Verify scrape jobs are healthy
2. **Use the Query tab** — Type `up` and click Execute to see all targets
3. **Try a specific metric** — `traces_span_metrics_calls_total` in the query box, click Execute, switch to Graph tab

### IPv6 connection issues (Windows)

.NET resolves `localhost` to `::1` (IPv6) first. Docker Desktop's IPv6 port forwarding is unreliable on Windows. Always use `127.0.0.1` in OTLP endpoint configuration instead of `localhost`.

---

## Files Reference

```
boilerplateBE/
├── docker-compose.yml          # Jaeger + Prometheus containers
├── jaeger-config.yaml          # Jaeger v2 OTel Collector config (SPM)
├── prometheus.yml              # Prometheus scrape config
├── prometheus-rules.yml        # Recording rules (auto-computed RED metrics)
└── src/Starter.Api/
    ├── Program.cs                          # Serilog OTLP sink (conditional on OTel enabled)
    ├── Configurations/
    │   └── OpenTelemetryConfiguration.cs   # OTel SDK setup (tracing + metrics)
    ├── appsettings.json                    # Base config (OTel disabled)
    ├── appsettings.Development.json        # Dev (OTel enabled, 127.0.0.1:4318)
    └── appsettings.Production.json         # Prod (OTel enabled, otel-collector:4318)
```
