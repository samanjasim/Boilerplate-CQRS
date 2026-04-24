import { Component, type ReactNode } from 'react';
import i18n from '@/i18n';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { AlertTriangle, RefreshCw } from 'lucide-react';

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

interface State {
  hasError: boolean;
  error?: Error;
}

export class ErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false };
  }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  handleReset = () => {
    this.setState({ hasError: false, error: undefined });
  };

  render() {
    if (this.state.hasError) {
      if (this.props.fallback) {
        return this.props.fallback;
      }

      return (
        <div className="flex min-h-screen items-center justify-center bg-background p-4">
          <Card className="max-w-md">
            <CardContent className="flex flex-col items-center text-center">
              <div className="mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-red-100 dark:bg-red-500/10">
                <AlertTriangle className="h-8 w-8 text-red-500" />
              </div>
              <h2 className="mb-2 text-xl font-semibold text-foreground">
                {i18n.t('common.somethingWentWrong')}
              </h2>
              <p className="mb-6 text-sm text-muted-foreground">
                {i18n.t('common.unexpectedError')}
              </p>
              {import.meta.env.DEV && this.state.error && (
                <pre className="mb-6 max-h-32 w-full overflow-auto rounded-lg bg-muted p-3 text-start text-xs text-muted-foreground">
                  {this.state.error.message}
                </pre>
              )}
              <Button onClick={this.handleReset}>
                <RefreshCw className="h-4 w-4" />
                {i18n.t('common.tryAgain')}
              </Button>
            </CardContent>
          </Card>
        </div>
      );
    }

    return this.props.children;
  }
}
