import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import en from './locales/en/translation.json';
import ar from './locales/ar/translation.json';
import ku from './locales/ku/translation.json';

const savedLanguage = (() => {
  try {
    const stored = localStorage.getItem('starter-ui');
    if (stored) {
      const parsed = JSON.parse(stored);
      return parsed.state?.language || 'en';
    }
  } catch {
    // ignore
  }
  return 'en';
})();

i18n.use(initReactI18next).init({
  resources: {
    en: { translation: en },
    ar: { translation: ar },
    ku: { translation: ku },
  },
  lng: savedLanguage,
  fallbackLng: 'en',
  interpolation: {
    escapeValue: false,
  },
});

export default i18n;
