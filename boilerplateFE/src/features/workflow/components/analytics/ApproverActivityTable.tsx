import { useTranslation } from 'react-i18next';
import { Card, CardContent } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/common';
import type { ApproverActivity } from '@/types/workflow.types';
import { Users } from 'lucide-react';

interface Props {
  rows: ApproverActivity[];
}

export function ApproverActivityTable({ rows }: Props) {
  const { t } = useTranslation();

  if (rows.length === 0) {
    return (
      <Card>
        <CardContent className="py-5">
          <h3 className="mb-3 text-sm font-semibold text-foreground">{t('workflow.analytics.approverActivity.title')}</h3>
          <EmptyState icon={Users} title={t('workflow.analytics.approverActivity.empty')} />
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardContent className="py-5">
        <h3 className="mb-3 text-sm font-semibold text-foreground">{t('workflow.analytics.approverActivity.title')}</h3>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('workflow.analytics.approverActivity.user')}</TableHead>
              <TableHead>{t('workflow.analytics.approverActivity.approvals')}</TableHead>
              <TableHead>{t('workflow.analytics.approverActivity.rejections')}</TableHead>
              <TableHead>{t('workflow.analytics.approverActivity.returns')}</TableHead>
              <TableHead>{t('workflow.analytics.approverActivity.avgResponse')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((r) => (
              <TableRow key={r.userId}>
                <TableCell>{r.userDisplayName ?? r.userId}</TableCell>
                <TableCell>{r.approvals}</TableCell>
                <TableCell>{r.rejections}</TableCell>
                <TableCell>{r.returns}</TableCell>
                <TableCell>{r.avgResponseTimeHours !== null ? r.avgResponseTimeHours.toFixed(1) : '—'}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}
