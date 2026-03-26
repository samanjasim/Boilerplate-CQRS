const BASE_DOMAIN = import.meta.env.VITE_BASE_DOMAIN || '';

// DNS label: 1-63 chars, alphanumeric + hyphens, no leading/trailing hyphen
const SLUG_PATTERN = /^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?$/;
const RESERVED = new Set(['www', 'api', 'admin', 'app', 'mail', 'smtp', 'ftp']);

function isValidSlug(value: string): boolean {
  return SLUG_PATTERN.test(value) && !RESERVED.has(value);
}

export function getTenantSlug(): string | null {
  // Dev fallback: ?tenant=acme query param
  if (import.meta.env.DEV) {
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
  if (import.meta.env.DEV) {
    // "acme.localhost:4000" → "localhost:4000", "localhost:4000" → "localhost:4000"
    return `${window.location.protocol}//${window.location.host.split('.').slice(-1)[0]}`;
  }
  if (!BASE_DOMAIN) return window.location.origin;
  return `${window.location.protocol}//${BASE_DOMAIN}`;
}

export function getTenantUrl(slug: string, path = '/'): string {
  if (import.meta.env.DEV) {
    const base = getMainDomainUrl();
    const url = new URL(path, base);
    url.searchParams.set('tenant', slug);
    return url.toString();
  }
  if (!BASE_DOMAIN) return window.location.origin + path;
  return `${window.location.protocol}//${slug}.${BASE_DOMAIN}${path}`;
}
