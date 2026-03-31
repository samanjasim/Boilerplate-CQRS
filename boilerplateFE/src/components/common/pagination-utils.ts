const STORAGE_KEY = 'app-page-size';
const DEFAULT_PAGE_SIZE = 20;

/** Read persisted page size from localStorage */
export function getPersistedPageSize(): number {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored) return Number(stored);
  } catch { /* ignore */ }
  return DEFAULT_PAGE_SIZE;
}

/** Persist page size to localStorage */
export function persistPageSize(size: number) {
  try {
    localStorage.setItem(STORAGE_KEY, String(size));
  } catch { /* ignore */ }
}
