import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { GitBranch } from 'lucide-react';
import { Link } from 'react-router-dom';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { PageHeader, EmptyState, Pagination } from '@/components/common';
import { getPersistedPageSize } from '@/components/common/pagination-utils';
import { STATUS_BADGE_VARIANT } from '@/constants/status';
import { useWorkflowDefinitions, useCloneDefinition } from '../api';

export default function WorkflowDefinitionsPage() {
  const { t } = useTranslation();
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize);
  const { data: allDefinitions = [], isLoading } = useWorkflowDefinitions();
  const { mutate: cloneDefinition, isPending: cloning } = useCloneDefinition();

  // Client-side pagination since the definitions endpoint returns all records
  const totalCount = allDefinitions.length;
  const totalPages = Math.ceil(totalCount / pageSize) || 1;
  const startIndex = (page - 1) * pageSize;
  const definitions = allDefinitions.slice(startIndex, startIndex + pageSize);
  const pagination = totalCount > 0
    ? {
        pageNumber: page,
        pageSize,
        totalCount,
        totalPages,
        hasNextPage: page < totalPages,
        hasPreviousPage: page > 1,
      }
    : null;

  return (
    <div className="space-y-6">
      <PageHeader title={t('workflow.definitions.title')} />

      {isLoading ? (
        <div className="flex justify-center py-12">
          <Spinner size="lg" />
        </div>
      ) : definitions.length === 0 ? (
        <EmptyState
          icon={GitBranch}
          title={t('workflow.definitions.title')}
          description={t('workflow.definitions.emptyDesc')}
        />
      ) : (
        <>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('workflow.definitions.name')}</TableHead>
                <TableHead>{t('workflow.definitions.entityType')}</TableHead>
                <TableHead>{t('workflow.definitions.steps')}</TableHead>
                <TableHead>{t('workflow.definitions.source')}</TableHead>
                <TableHead>{t('workflow.definitions.status')}</TableHead>
                <TableHead>{t('workflow.inbox.actions')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {definitions.map((def) => (
                <TableRow key={def.id}>
                  <TableCell className="font-medium text-foreground">
                    <Link
                      to={`/workflows/definitions/${def.id}`}
                      className="hover:underline"
                    >
                      {def.displayName || def.name}
                    </Link>
                  </TableCell>
                  <TableCell>
                    <Badge variant="secondary">{def.entityType}</Badge>
                  </TableCell>
                  <TableCell className="text-muted-foreground">{def.stepCount}</TableCell>
                  <TableCell>
                    <Badge variant={def.isTemplate ? 'outline' : 'default'}>
                      {def.isTemplate
                        ? t('workflow.definitions.systemTemplate')
                        : t('workflow.definitions.customized')}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <Badge variant={STATUS_BADGE_VARIANT[def.isActive ? 'Active' : 'Inactive'] ?? 'outline'}>
                      {def.isActive
                        ? t('workflow.definitions.statusValue.active')
                        : t('workflow.definitions.statusValue.inactive')}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center gap-2">
                      {def.isTemplate ? (
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => cloneDefinition(def.id)}
                          disabled={cloning}
                        >
                          {t('workflow.definitions.clone')}
                        </Button>
                      ) : (
                        <Button size="sm" variant="outline" asChild>
                          <Link to={`/workflows/definitions/${def.id}`}>
                            {t('workflow.definitions.edit')}
                          </Link>
                        </Button>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>

          {pagination && (
            <Pagination
              pagination={pagination}
              onPageChange={setPage}
              onPageSizeChange={(size) => { setPageSize(size); setPage(1); }}
            />
          )}
        </>
      )}
    </div>
  );
}
