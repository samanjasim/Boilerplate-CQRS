const BASE_DOMAIN = import.meta.env.VITE_BASE_DOMAIN || '';

// Use ?tenant= query param fallback only when BASE_DOMAIN is "localhost"
// (real subdomains like acme.localhost work in Chrome but cookies don't share).
// When BASE_DOMAIN is a real domain (e.g., starter.local), use actual subdomains.
const USE_QUERY_PARAM_FALLBACK = BASE_DOMAIN === 'localhost';

// DNS label: 1-63 chars, alphanumeric + hyphens, no leading/trailing hyphen
const SLUG_PATTERN = /^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?$/;
const RESERVED = new Set(['www', 'api', 'admin', 'app', 'mail', 'smtp', 'ftp']);

function isValidSlug(value: string): boolean {
  return SLUG_PATTERN.test(value) && !RESERVED.has(value);
}

export function getTenantSlug(): string | null {
  // Fallback: ?tenant=acme query param (only when BASE_DOMAIN=localhost)
  if (USE_QUERY_PARAM_FALLBACK) {
    const params = new URLSearchParams(window.location.search);
    const tenant = params.get('tenant')?.toLowerCase();
    if (tenant && isValidSlug(tenant)) return tenant;
  }

  if (!BASE_DOMAIN) return null;

  const hostname = window.location.hostname;
  if (hostname === BASE_DOMAIN) return null;

  const suffix = `.${BASE_DOMAIN}`;
  if (!hostname.endsWith(suffix)) return null;

  const subdomain = hostname.slice(0, hostname.length - suffix.length).toLowerCase();
  if (subdomain.includes('.')) return null;
  if (!isValidSlug(subdomain)) return null;

  return subdomain;
}

export function isSubdomainAccess(): boolean {
  return getTenantSlug() !== null;
}

export function getMainDomainUrl(): string {
  if (!BASE_DOMAIN) return window.location.origin;
  const port = window.location.port ? `:${window.location.port}` : '';
  return `${window.location.protocol}//${BASE_DOMAIN}${port}`;
}

export function getTenantUrl(slug: string, path = '/'): string {
  if (USE_QUERY_PARAM_FALLBACK) {
    const base = getMainDomainUrl();
    const url = new URL(path, base);
    url.searchParams.set('tenant', slug);
    return url.toString();
  }
  if (!BASE_DOMAIN) return window.location.origin + path;
  const port = window.location.port ? `:${window.location.port}` : '';
  return `${window.location.protocol}//${slug}.${BASE_DOMAIN}${port}${path}`;
}
