const BASE_DOMAIN = import.meta.env.VITE_BASE_DOMAIN || '';

export function getTenantSlug(): string | null {
  // Dev fallback: ?tenant=acme query param
  if (import.meta.env.DEV) {
    const params = new URLSearchParams(window.location.search);
    const tenant = params.get('tenant');
    if (tenant) return tenant.toLowerCase();
  }

  if (!BASE_DOMAIN) return null;

  const hostname = window.location.hostname;
  if (hostname === BASE_DOMAIN) return null;

  const suffix = `.${BASE_DOMAIN}`;
  if (!hostname.endsWith(suffix)) return null;

  const subdomain = hostname.slice(0, hostname.length - suffix.length);
  const reserved = ['www', 'api', 'admin', 'app', 'mail', 'smtp', 'ftp'];
  if (!subdomain || reserved.includes(subdomain.toLowerCase())) return null;
  if (subdomain.includes('.')) return null;

  return subdomain.toLowerCase();
}

export function isSubdomainAccess(): boolean {
  return getTenantSlug() !== null;
}

export function getMainDomainUrl(): string {
  if (import.meta.env.DEV) {
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
