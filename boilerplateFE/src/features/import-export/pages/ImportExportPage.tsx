import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ArrowLeftRight } from 'lucide-react';
import { PageHeader } from '@/components/common';
import { Button } from '@/components/ui/button';
import { ExportsTab } from '../components/ExportsTab';
import { ImportsTab } from '../components/ImportsTab';

type Tab = 'exports' | 'imports';

export default function ImportExportPage() {
  const { t } = useTranslation();
  const [activeTab, setActiveTab] = useState<Tab>('exports');

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('importExport.title')}
        subtitle={t('importExport.subtitle')}
      />

      {/* Tab navigation */}
      <div className="flex gap-1 rounded-xl bg-muted p-1 w-fit">
        <Button
          variant="ghost"
          size="sm"
          className={`rounded-lg px-4 transition-all ${
            activeTab === 'exports'
              ? 'bg-background text-foreground shadow-sm'
              : 'text-muted-foreground hover:text-foreground'
          }`}
          onClick={() => setActiveTab('exports')}
        >
          <ArrowLeftRight className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
          {t('importExport.exports')}
        </Button>
        <Button
          variant="ghost"
          size="sm"
          className={`rounded-lg px-4 transition-all ${
            activeTab === 'imports'
              ? 'bg-background text-foreground shadow-sm'
              : 'text-muted-foreground hover:text-foreground'
          }`}
          onClick={() => setActiveTab('imports')}
        >
          <ArrowLeftRight className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
          {t('importExport.imports')}
        </Button>
      </div>

      {/* Tab content */}
      {activeTab === 'exports' && <ExportsTab />}
      {activeTab === 'imports' && <ImportsTab />}
    </div>
  );
}
