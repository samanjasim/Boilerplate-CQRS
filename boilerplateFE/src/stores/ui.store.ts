import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';

type Theme = 'dark' | 'light' | 'system';
type Language = 'en' | 'ar' | 'ku';

interface UIState {
  theme: Theme;
  language: Language;
  sidebarOpen: boolean;
  sidebarCollapsed: boolean;
  activeModal: string | null;
  modalData: unknown;
  activeTenantId: string | null;
}

interface UIActions {
  setTheme: (theme: Theme) => void;
  setLanguage: (language: Language) => void;
  toggleSidebar: () => void;
  setSidebarOpen: (open: boolean) => void;
  toggleSidebarCollapse: () => void;
  openModal: (modalId: string, data?: unknown) => void;
  closeModal: () => void;
  setActiveTenantId: (tenantId: string | null) => void;
}

type UIStore = UIState & UIActions;

const applyTheme = (theme: Theme) => {
  const root = document.documentElement;
  root.classList.remove('light', 'dark');
  if (theme === 'system') {
    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    root.classList.add(prefersDark ? 'dark' : 'light');
  } else {
    root.classList.add(theme);
  }
};

const applyLanguageDirection = (language: Language) => {
  const root = document.documentElement;
  const isRTL = language === 'ar' || language === 'ku';
  root.dir = isRTL ? 'rtl' : 'ltr';
  root.lang = language;
};

export const useUIStore = create<UIStore>()(
  persist(
    (set) => ({
      theme: 'light',
      language: 'en',
      sidebarOpen: true,
      sidebarCollapsed: false,
      activeModal: null,
      modalData: null,
      activeTenantId: null,

      setTheme: (theme) => {
        set({ theme });
        applyTheme(theme);
      },

      setLanguage: (language) => {
        set({ language });
        applyLanguageDirection(language);
      },

      toggleSidebar: () => set((state) => ({ sidebarOpen: !state.sidebarOpen })),

      setSidebarOpen: (sidebarOpen) => set({ sidebarOpen }),

      toggleSidebarCollapse: () =>
        set((state) => ({ sidebarCollapsed: !state.sidebarCollapsed })),

      openModal: (activeModal, modalData = null) => set({ activeModal, modalData }),

      closeModal: () => set({ activeModal: null, modalData: null }),

      setActiveTenantId: (activeTenantId) => set({ activeTenantId }),
    }),
    {
      name: 'starter-ui',
      storage: createJSONStorage(() => localStorage),
      partialize: (state) => ({
        theme: state.theme,
        language: state.language,
        sidebarCollapsed: state.sidebarCollapsed,
        activeTenantId: state.activeTenantId,
      }),
      onRehydrateStorage: () => (state) => {
        if (state) {
          applyTheme(state.theme);
          applyLanguageDirection(state.language);
        }
      },
    }
  )
);

export const selectTheme = (state: UIStore) => state.theme;
export const selectLanguage = (state: UIStore) => state.language;
export const selectSidebarOpen = (state: UIStore) => state.sidebarOpen;
export const selectSidebarCollapsed = (state: UIStore) => state.sidebarCollapsed;
export const selectActiveModal = (state: UIStore) => state.activeModal;
export const selectModalData = (state: UIStore) => state.modalData;
export const selectActiveTenantId = (state: UIStore) => state.activeTenantId;
