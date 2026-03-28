import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ToggleRight, Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { PageHeader, EmptyState, getPersistedPageSize } from '@/components/common';
import { useFeatureFlags } from '../api';
import { FeatureFlagsList } from '../components/FeatureFlagsList';
import { CreateFeatureFlagDialog } from '../components/CreateFeatureFlagDialog';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';

export default function FeatureFlagsPage() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize);
  const [createOpen, setCreateOpen] = useState(false);

  const { data, isLoading, isError } = useFeatureFlags({ pageNumber, pageSize });

  if (isError) {
    return (
      <div className="space-y-6">
        <PageHeader title={t('featureFlags.title')} />
        <EmptyState icon={ToggleRight} title={t('common.errorOccurred')} description={t('common.tryAgain')} />
      </div>
    );
  }

  if (isLoading && !data) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('featureFlags.title')}
        subtitle={t('featureFlags.description')}
        actions={
          hasPermission(PERMISSIONS.FeatureFlags.Create) ? (
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="mr-2 h-4 w-4" />
              {t('featureFlags.create')}
            </Button>
          ) : undefined
        }
      />

      <FeatureFlagsList
        flags={data?.data ?? []}
        pagination={data?.pagination}
        onPageChange={setPageNumber}
        onPageSizeChange={(size) => { setPageSize(size); setPageNumber(1); }}
      />

      <CreateFeatureFlagDialog open={createOpen} onOpenChange={setCreateOpen} />
    </div>
  );
}
