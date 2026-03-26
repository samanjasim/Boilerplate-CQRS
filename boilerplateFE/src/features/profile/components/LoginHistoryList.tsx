import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { formatDateTime } from '@/utils/format';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Pagination, getPersistedPageSize } from '@/components/common';
import { useLoginHistory } from '@/features/auth/api';

export function LoginHistoryList() {
  const { t } = useTranslation();
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(() => Math.min(getPersistedPageSize(), 10));

  const { data, isLoading } = useLoginHistory({ pageNumber, pageSize });

  const items = data?.data ?? [];
  const pagination = data?.pagination;

  return (
    <Card>
      <CardContent className="py-6">
        <h3 className="text-lg font-semibold text-foreground mb-4">
          {t('loginHistory.title')}
        </h3>

        {isLoading && (
          <p className="text-sm text-muted-foreground">{t('common.loading')}</p>
        )}

        {!isLoading && items.length === 0 && (
          <p className="text-sm text-muted-foreground">{t('loginHistory.noHistory')}</p>
        )}

        {items.length > 0 && (
          <>
            <div className="rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t('auditLogs.performedAt')}</TableHead>
                    <TableHead>{t('auditLogs.ipAddress')}</TableHead>
                    <TableHead>{t('sessions.device')}</TableHead>
                    <TableHead>{t('common.status')}</TableHead>
                    <TableHead>{t('loginHistory.reason')}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {items.map((entry) => (
                    <TableRow key={entry.id}>
                      <TableCell className="text-sm">
                        {formatDateTime(entry.createdAt)}
                      </TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {entry.ipAddress ?? '-'}
                      </TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {entry.deviceInfo ?? '-'}
                      </TableCell>
                      <TableCell>
                        {entry.success ? (
                          <Badge variant="default">{t('loginHistory.success')}</Badge>
                        ) : (
                          <Badge variant="destructive">{t('loginHistory.failed')}</Badge>
                        )}
                      </TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {entry.failureReason ?? '-'}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>

            {pagination && (
              <Pagination
                pagination={pagination}
                onPageChange={setPageNumber}
                onPageSizeChange={(size) => { setPageSize(size); setPageNumber(1); }}
              />
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
