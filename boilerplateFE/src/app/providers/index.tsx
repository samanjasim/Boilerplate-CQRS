import { Suspense, type ReactNode } from 'react';
import { Toaster } from '@/components/ui/sonner';
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
        <Toaster />
      </QueryProvider>
    </ErrorBoundary>
  );
}
