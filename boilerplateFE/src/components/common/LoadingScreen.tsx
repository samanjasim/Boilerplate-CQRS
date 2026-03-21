import i18n from '@/i18n';
import { Spinner } from '@/components/ui/spinner';

export function LoadingScreen() {
  return (
    <div className="flex h-screen w-screen items-center justify-center bg-background">
      <div className="flex flex-col items-center gap-4">
        <Spinner size="lg" />
        <p className="text-sm text-muted-foreground">{i18n.t('common.loading')}</p>
      </div>
    </div>
  );
}
