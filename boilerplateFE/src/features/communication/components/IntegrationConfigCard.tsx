import { useTranslation } from 'react-i18next';
import { Pencil, Trash2, Send, AlertCircle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import { STATUS_BADGE_VARIANT } from '@/constants/status';
import { ProviderLogo } from './ProviderLogo';
import { deriveLastTestedState } from '../utils/lastTested';
import type { IntegrationConfigDto } from '@/types/communication.types';

interface IntegrationConfigCardProps {
  config: IntegrationConfigDto;
  canManage: boolean;
  onTest: () => void;
  onEdit: () => void;
  onDelete: () => void;
  isTestPending: boolean;
}

export function IntegrationConfigCard({
  config,
  canManage,
  onTest,
  onEdit,
  onDelete,
  isTestPending,
}: IntegrationConfigCardProps) {
  const { t } = useTranslation();
  const lastTested = deriveLastTestedState(config.lastTestedAt);
  const isErrored = config.status === 'Error';

  return (
    <Card variant="elevated">
      <CardHeader className="pb-3">
        <div className="flex items-start gap-3">
          <ProviderLogo provider={config.integrationType} size="md" />
          <div className="flex-1 min-w-0">
            <CardTitle className="text-base">
              <span className="truncate">{config.displayName}</span>
            </CardTitle>
            <p className="text-sm text-muted-foreground">
              {t(`communication.integrations.types.${config.integrationType}`)}
            </p>
          </div>
          <Badge
            variant={STATUS_BADGE_VARIANT[config.status] ?? 'secondary'}
            className="shrink-0"
          >
            {isErrored && <AlertCircle className="h-3 w-3 ltr:mr-1 rtl:ml-1" />}
            {config.status}
          </Badge>
        </div>
      </CardHeader>
      <CardContent>
        <div className="space-y-3">
          <div className={cn('inline-flex items-center rounded-md px-2 py-0.5 text-xs', lastTested.chipClass)}>
            {lastTested.label
              ? t(`communication.channels.lastTested.${toneToKey(lastTested.tone)}`, { relative: lastTested.label })
              : t('communication.channels.lastTested.never')}
          </div>

          {canManage && (
            <div className="flex gap-1 pt-1">
              <Button
                variant="ghost"
                size="sm"
                title={t('communication.integrations.testButton')}
                onClick={onTest}
                disabled={isTestPending}
              >
                <Send className="h-4 w-4" />
              </Button>
              <Button variant="ghost" size="sm" onClick={onEdit}>
                <Pencil className="h-4 w-4" />
              </Button>
              <Button variant="ghost" size="sm" onClick={onDelete}>
                <Trash2 className="h-4 w-4 text-destructive" />
              </Button>
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

function toneToKey(tone: ReturnType<typeof deriveLastTestedState>['tone']): string {
  switch (tone) {
    case 'fresh': return 'justNow';
    case 'today': return 'today';
    case 'week': return 'thisWeek';
    case 'older': return 'older';
    case 'never': return 'never';
  }
}
