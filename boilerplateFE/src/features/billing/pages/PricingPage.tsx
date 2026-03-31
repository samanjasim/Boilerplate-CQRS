import { useTranslation } from 'react-i18next';
import { PageHeader } from '@/components/common';

export default function PricingPage() {
  const { t } = useTranslation();

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('billing.pricingTitle')}
        subtitle={t('billing.pricingSubtitle')}
      />
    </div>
  );
}
