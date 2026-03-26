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
              borderRadius: '12px',
              boxShadow: '0 4px 12px rgba(0, 0, 0, 0.08), 0 0 0 1px rgba(0, 0, 0, 0.03)',
              border: 'none',
              padding: '12px 16px',
            },
          }}
        />
      </QueryProvider>
    </ErrorBoundary>
  );
}
