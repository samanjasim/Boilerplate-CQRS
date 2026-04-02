import { useTranslation } from 'react-i18next';
import { Webhook } from 'lucide-react';
import { PageHeader, EmptyState } from '@/components/common';

export default function WebhooksPage() {
  const { t } = useTranslation();

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('webhooks.title')}
        subtitle={t('webhooks.subtitle')}
      />

      <EmptyState
        icon={Webhook}
        title={t('webhooks.noEndpoints')}
        description={t('webhooks.noEndpointsDesc')}
      />
    </div>
  );
}
