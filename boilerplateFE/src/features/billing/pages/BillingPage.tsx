import { useTranslation } from 'react-i18next';
import { PageHeader } from '@/components/common';

export default function BillingPage() {
  const { t } = useTranslation();

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('billing.title')}
        subtitle={t('billing.subtitle')}
      />
    </div>
  );
}
