import { useTranslation } from 'react-i18next';
import { AlertCircle } from 'lucide-react';

interface Props {
  count: number;
}

export function LowDataBanner({ count }: Props) {
  const { t } = useTranslation();
  return (
    <div className="flex items-start gap-2 rounded-xl border border-border bg-muted/50 px-4 py-3 text-sm text-muted-foreground">
      <AlertCircle className="mt-0.5 h-4 w-4 flex-shrink-0" />
      <p>{t('workflow.analytics.lowDataBanner', { count })}</p>
    </div>
  );
}
