import { Navigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Spinner } from '@/components/ui/spinner';
import { PageHeader } from '@/components/common';
import { ROUTES } from '@/config';
import { formatDateTime } from '@/utils/format';
import { useAuditLog } from '../api';
import { AuditMetadataCard } from '../components/AuditMetadataCard';
import { JsonView } from '../components/JsonView';

type StatusVariant = 'failed' | 'info' | 'pending' | 'healthy';

function statusForAction(action: string): StatusVariant {
  const normalized = action.toLowerCase();
  if (normalized.includes('delete') || normalized.includes('revoke') || normalized.includes('suspend')) {
    return 'failed';
  }
  if (normalized.includes('create')) return 'healthy';
  if (normalized.includes('login') || normalized.includes('logout')) return 'info';
  return 'info';
}

export default function AuditLogDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { t } = useTranslation();

  const { data: log, isLoading, isError, error } = useAuditLog(id);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-16">
        <Spinner />
      </div>
    );
  }

  if (isError || !log) {
    const status = (error as { response?: { status?: number } } | null)?.response?.status;
    if (status === 404) return <Navigate to="/404" replace />;

    return (
      <Card>
        <CardContent className="py-12 text-center text-muted-foreground">
          {t('auditLogs.detail.notFound')}
        </CardContent>
      </Card>
    );
  }

  const variant = statusForAction(log.action);
  const heading = `${log.entityType} ${log.action.toLowerCase()}`;

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('auditLogs.detail.title')}
        breadcrumbs={[
          { label: t('auditLogs.title'), to: ROUTES.AUDIT_LOGS.LIST },
          { label: log.action },
        ]}
      />

      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h1 className="gradient-text text-2xl font-semibold">{heading}</h1>
          <p className="mt-1 font-mono text-sm text-muted-foreground">{log.entityId}</p>
          <p className="mt-2 text-xs text-muted-foreground">{formatDateTime(log.performedAt)}</p>
        </div>
        <Badge variant={variant}>{log.action}</Badge>
      </div>

      <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
        <Card variant="glass">
          <CardContent className="pt-6">
            <h2 className="mb-4 text-sm font-medium">{t('auditLogs.detail.eventPayload')}</h2>
            <JsonView payload={log.changes} />
          </CardContent>
        </Card>

        <AuditMetadataCard log={log} />
      </div>
    </div>
  );
}
