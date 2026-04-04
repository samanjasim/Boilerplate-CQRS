export function truncateUrl(url: string, maxLen = 40): string {
  if (url.length <= maxLen) return url;
  return `${url.slice(0, maxLen)}...`;
}

export function tryPrettyJson(value: string): string {
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}
