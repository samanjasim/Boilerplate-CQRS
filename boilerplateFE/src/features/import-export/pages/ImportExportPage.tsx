import { useTranslation } from 'react-i18next';
import { ArrowLeftRight } from 'lucide-react';
import { PageHeader } from '@/components/common';
import { EmptyState } from '@/components/common';

export default function ImportExportPage() {
  const { t } = useTranslation();

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('importExport.title')}
        subtitle={t('importExport.subtitle')}
      />
      <EmptyState
        icon={ArrowLeftRight}
        title={t('importExport.noImports')}
        description={t('common.comingSoonDesc')}
      />
    </div>
  );
}
