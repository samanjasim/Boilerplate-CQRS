import { useEffect } from 'react';
import { useUIStore } from '@/stores';

/**
 * Sets a back navigation button in the header bar.
 * Automatically clears when the component unmounts (page navigates away).
 *
 * @example
 * useBackNavigation('/users', t('users.backToUsers'));
 */
export function useBackNavigation(to: string, label: string) {
  const setBackNavigation = useUIStore((s) => s.setBackNavigation);

  useEffect(() => {
    setBackNavigation({ to, label });
    return () => setBackNavigation(null);
  }, [to, label, setBackNavigation]);
}
