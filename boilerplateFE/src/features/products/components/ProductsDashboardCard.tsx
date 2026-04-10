import { Package } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';
import { ROUTES } from '@/config';
import { useProducts } from '../api';

export function ProductsDashboardCard() {
  const { t } = useTranslation();
  const { data } = useProducts({ pageSize: 1, status: 'Active' });
  const totalActive = data?.pagination?.totalCount ?? 0;

  return (
    <Link to={ROUTES.PRODUCTS.LIST}>
      <Card className="hover-lift">
        <CardContent className="py-6">
          <div className="flex items-center gap-4">
            <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-orange-500/10 text-orange-600">
              <Package className="h-6 w-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">{t('dashboard.activeProducts', 'Active Products')}</p>
              <p className="text-2xl font-bold text-foreground">{totalActive}</p>
            </div>
          </div>
        </CardContent>
      </Card>
    </Link>
  );
}
