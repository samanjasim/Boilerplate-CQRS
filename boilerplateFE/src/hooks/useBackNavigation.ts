import { useEffect } from 'react';
import { useUIStore } from '@/stores';

/**
 * Sets a back navigation button in the header bar.
 * Automatically clears when the component unmounts (page navigates away).
 *
 * @example
 * useBackNavigation('/users', t('users.backToUsers'));
 */
/**
 * @deprecated Use the `breadcrumbs` prop on `<PageHeader>` instead.
 *   The header back-link UI was removed in the floating-glass refresh.
 *   The hook still works (it sets `useUIStore.backNavigation`) but no
 *   component reads that state any more. Will be removed in a follow-up.
 */
export function useBackNavigation(to: string, label: string) {
  const setBackNavigation = useUIStore((s) => s.setBackNavigation);

  useEffect(() => {
    setBackNavigation({ to, label });
    return () => setBackNavigation(null);
  }, [to, label, setBackNavigation]);
}
