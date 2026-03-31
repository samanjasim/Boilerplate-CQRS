import { useTranslation } from 'react-i18next';
import { PageHeader } from '@/components/common';

export default function BillingPlansPage() {
  const { t } = useTranslation();

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('billing.plans')}
        subtitle={t('billing.plansSubtitle')}
      />
    </div>
  );
}
