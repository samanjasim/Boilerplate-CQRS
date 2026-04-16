import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { Send, AlertTriangle } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { ROUTES } from '@/config';
import { useCommunicationDashboard } from '../api';

export function CommunicationDashboardWidget() {
  const { t } = useTranslation();
  const { data } = useCommunicationDashboard();

  const dashboard = data?.data;
  const sentToday = dashboard?.messagesSentToday ?? 0;
  const successRate = dashboard?.deliverySuccessRate ?? 0;
  const failed = dashboard?.failedDeliveries ?? 0;

  return (
    <Link to={ROUTES.COMMUNICATION.DELIVERY_LOG}>
      <Card className="hover-lift">
        <CardContent className="py-6">
          <div className="flex items-center gap-4">
            <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-blue-500/10 text-blue-600">
              <Send className="h-6 w-6" />
            </div>
            <div className="flex-1 min-w-0">
              <p className="text-sm text-muted-foreground">
                {t('communication.dashboard.title')}
              </p>
              <div className="flex items-baseline gap-3">
                <p className="text-2xl font-bold text-foreground">{sentToday}</p>
                <span className="text-xs text-muted-foreground">
                  {t('communication.dashboard.messagesSentToday')}
                </span>
              </div>
              <div className="flex items-center gap-3 mt-1">
                <span className="text-xs text-muted-foreground">
                  {t('communication.dashboard.deliveryRate')}: {successRate}%
                </span>
                {failed > 0 && (
                  <span className="flex items-center gap-1 text-xs text-destructive">
                    <AlertTriangle className="h-3 w-3" />
                    {failed} {t('communication.dashboard.failedDeliveries')}
                  </span>
                )}
              </div>
            </div>
          </div>
        </CardContent>
      </Card>
    </Link>
  );
}
