export const LANGUAGES = [
  { code: 'en' as const, label: 'English' },
  { code: 'ar' as const, label: 'العربية' },
  { code: 'ku' as const, label: 'کوردی' },
] as const;

export type LanguageCode = (typeof LANGUAGES)[number]['code'];
