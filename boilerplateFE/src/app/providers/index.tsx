import { Suspense, type ReactNode } from 'react';
import { Toaster } from 'sonner';
import { QueryProvider } from './QueryProvider';
import { LoadingScreen } from '@/components/common';
import { ErrorBoundary } from '@/components/common';

interface AppProvidersProps {
  children: ReactNode;
}

export function AppProviders({ children }: AppProvidersProps) {
  return (
    <ErrorBoundary>
      <QueryProvider>
        <Suspense fallback={<LoadingScreen />}>
          {children}
        </Suspense>
        <Toaster
          position="top-right"
          toastOptions={{
            style: {
              background: 'var(--color-surface)',
              border: '1px solid var(--color-border)',
              color: 'var(--color-text-primary)',
            },
          }}
        />
      </QueryProvider>
    </ErrorBoundary>
  );
}
