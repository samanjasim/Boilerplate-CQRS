import { useCallback, useMemo } from 'react';
import { formatDistanceToNow, type Locale } from 'date-fns';
import { ar, enUS } from 'date-fns/locale';
import { useTranslation } from 'react-i18next';

const LOCALES: Record<string, Locale> = {
  ar,
  ku: ar,
  en: enUS,
};

function resolveLocale(lng: string): Locale {
  const key = lng.toLowerCase().split('-')[0] ?? '';
  return LOCALES[key] ?? enUS;
}

function formatTimeAgo(
  date: Date | string | number | null | undefined,
  locale: Locale,
  addSuffix: boolean,
): string {
  if (!date) return '';
  const d = date instanceof Date ? date : new Date(date);
  if (Number.isNaN(d.getTime())) return '';
  return formatDistanceToNow(d, { addSuffix, locale });
}

export function useTimeAgo(
  date: Date | string | number | null | undefined,
  options?: { addSuffix?: boolean },
): string {
  const { i18n } = useTranslation();
  return useMemo(
    () => formatTimeAgo(date, resolveLocale(i18n.language), options?.addSuffix ?? true),
    [date, i18n.language, options?.addSuffix],
  );
}

export function useTimeAgoFormatter(options?: { addSuffix?: boolean }) {
  const { i18n } = useTranslation();
  const addSuffix = options?.addSuffix ?? true;
  return useCallback(
    (date: Date | string | number | null | undefined) =>
      formatTimeAgo(date, resolveLocale(i18n.language), addSuffix),
    [i18n.language, addSuffix],
  );
}
