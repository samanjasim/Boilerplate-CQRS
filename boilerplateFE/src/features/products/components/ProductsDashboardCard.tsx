import { Package } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { StatCard } from '@/components/common';
import { ROUTES } from '@/config';
import { useProducts } from '../api';

export function ProductsDashboardCard() {
  const { t } = useTranslation();
  const { data } = useProducts({ pageSize: 1, status: 'Active' });
  const totalActive = data?.pagination?.totalCount ?? 0;

  return (
    <Link to={ROUTES.PRODUCTS.LIST} className="block">
      <StatCard
        icon={Package}
        label={t('dashboard.activeProducts', 'Active Products')}
        value={totalActive}
        tone="copper"
      />
    </Link>
  );
}
