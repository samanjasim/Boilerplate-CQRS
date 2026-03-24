import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { formatDateTime } from '@/utils/format';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { useLoginHistory } from '@/features/auth/api';

export function LoginHistoryList() {
  const { t } = useTranslation();
  const [pageNumber, setPageNumber] = useState(1);
  const pageSize = 10;

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

            {pagination && pagination.totalPages > 1 && (
              <div className="flex items-center justify-between mt-4">
                <p className="text-sm text-muted-foreground">
                  {t('common.showing', {
                    start: (pagination.pageNumber - 1) * pagination.pageSize + 1,
                    end: Math.min(pagination.pageNumber * pagination.pageSize, pagination.totalCount),
                    total: pagination.totalCount,
                  })}
                </p>
                <div className="flex gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={!pagination.hasPreviousPage}
                    onClick={() => setPageNumber((p) => p - 1)}
                  >
                    {t('common.previous')}
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={!pagination.hasNextPage}
                    onClick={() => setPageNumber((p) => p + 1)}
                  >
                    {t('common.next')}
                  </Button>
                </div>
              </div>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
