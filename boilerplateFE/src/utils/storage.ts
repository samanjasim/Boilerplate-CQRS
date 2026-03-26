const BASE_DOMAIN = import.meta.env.VITE_BASE_DOMAIN || '';
const IS_PROD = import.meta.env.PROD;
const TOKEN_PREFIX = 'starter'; // Will be renamed by rename script

const STORAGE_KEYS = {
  ACCESS_TOKEN: `${TOKEN_PREFIX}_access_token`,
  REFRESH_TOKEN: `${TOKEN_PREFIX}_refresh_token`,
} as const;

function getCookie(name: string): string | null {
  const match = document.cookie.match(new RegExp(`(?:^|; )${name}=([^;]*)`));
  return match ? decodeURIComponent(match[1]) : null;
}

function setCookie(name: string, value: string): void {
  let cookie = `${name}=${encodeURIComponent(value)}; path=/; SameSite=Lax`;
  if (IS_PROD) cookie += '; Secure';
  if (BASE_DOMAIN && BASE_DOMAIN !== 'localhost') {
    cookie += `; domain=.${BASE_DOMAIN}`;
  }
  document.cookie = cookie;
}

function deleteCookie(name: string): void {
  let cookie = `${name}=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Lax`;
  if (BASE_DOMAIN && BASE_DOMAIN !== 'localhost') {
    cookie += `; domain=.${BASE_DOMAIN}`;
  }
  document.cookie = cookie;
}

export const storage = {
  getAccessToken: (): string | null => {
    return getCookie(STORAGE_KEYS.ACCESS_TOKEN);
  },

  getRefreshToken: (): string | null => {
    return getCookie(STORAGE_KEYS.REFRESH_TOKEN);
  },

  setTokens: (accessToken: string, refreshToken: string): void => {
    setCookie(STORAGE_KEYS.ACCESS_TOKEN, accessToken);
    setCookie(STORAGE_KEYS.REFRESH_TOKEN, refreshToken);
  },

  clearTokens: (): void => {
    deleteCookie(STORAGE_KEYS.ACCESS_TOKEN);
    deleteCookie(STORAGE_KEYS.REFRESH_TOKEN);
  },

  get: <T>(key: string, defaultValue: T): T => {
    try {
      const item = localStorage.getItem(key);
      return item ? JSON.parse(item) : defaultValue;
    } catch {
      return defaultValue;
    }
  },

  set: <T>(key: string, value: T): void => {
    localStorage.setItem(key, JSON.stringify(value));
  },

  remove: (key: string): void => {
    localStorage.removeItem(key);
  },
};
