import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Card, CardContent } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/common';
import type { StuckInstance } from '@/types/workflow.types';
import { Timer } from 'lucide-react';
import { formatDate } from '@/utils/format';

interface Props {
  rows: StuckInstance[];
}

export function StuckInstancesTable({ rows }: Props) {
  const { t } = useTranslation();
  const navigate = useNavigate();

  if (rows.length === 0) {
    return (
      <Card>
        <CardContent className="py-5">
          <h3 className="mb-3 text-sm font-semibold text-foreground">{t('workflow.analytics.stuck.title')}</h3>
          <EmptyState icon={Timer} title={t('workflow.analytics.stuck.empty')} />
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="space-y-3">
      <h3 className="text-sm font-semibold text-foreground">{t('workflow.analytics.stuck.title')}</h3>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>{t('workflow.analytics.stuck.currentState')}</TableHead>
            <TableHead>{t('workflow.analytics.stuck.startedAt')}</TableHead>
            <TableHead>{t('workflow.analytics.stuck.daysSince')}</TableHead>
            <TableHead>{t('workflow.analytics.stuck.assignee')}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.map((r) => (
            <TableRow
              key={r.instanceId}
              className="cursor-pointer"
              onClick={() => navigate(`/workflows/instances/${r.instanceId}`)}
            >
              <TableCell>
                <div className="flex flex-col">
                  <span className="font-medium">{r.entityDisplayName ?? r.instanceId.slice(0, 8)}</span>
                  <span className="text-xs text-muted-foreground">{r.currentState}</span>
                </div>
              </TableCell>
              <TableCell>{formatDate(r.startedAt)}</TableCell>
              <TableCell>
                {`${r.daysSinceStarted}${t('workflow.analytics.daysShort')}`}
              </TableCell>
              <TableCell>{r.currentAssigneeDisplayName ?? '—'}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
