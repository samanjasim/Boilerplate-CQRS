import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { AlertCircle } from 'lucide-react';
import { Spinner } from '@/components/ui/spinner';
import { EmptyState, type EmptyStateProps } from './EmptyState';

interface ListPageStateProps {
  /** Set true while no data has loaded yet. */
  isInitialLoading: boolean;
  /** Set true when the query errored. */
  isError: boolean;
  /** Set true when the query settled with zero rows. */
  isEmpty: boolean;
  /** Props passed to `<EmptyState>` when `isEmpty`. */
  emptyState: EmptyStateProps;
  /** Optional override of the error fallback. Defaults to a generic AlertCircle EmptyState. */
  errorState?: EmptyStateProps;
  /** Render the populated content (typically a Table). */
  children: ReactNode;
}

/**
 * Renders the standard list-page state ladder: error → loading → empty → content.
 * Pair with `useListPage` to keep page bodies focused on layout, not state branching.
 */
export function ListPageState({
  isInitialLoading,
  isError,
  isEmpty,
  emptyState,
  errorState,
  children,
}: ListPageStateProps) {
  const { t } = useTranslation();

  if (isError) {
    return (
      <EmptyState
        icon={errorState?.icon ?? AlertCircle}
        title={errorState?.title ?? t('common.errorOccurred')}
        description={errorState?.description ?? t('common.tryAgain')}
        action={errorState?.action}
        className={errorState?.className}
      />
    );
  }

  if (isInitialLoading) {
    return (
      <div className="flex justify-center py-16">
        <Spinner size="lg" />
      </div>
    );
  }

  if (isEmpty) {
    return <EmptyState {...emptyState} />;
  }

  return <>{children}</>;
}
