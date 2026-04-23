import { useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Spinner } from '@/components/ui/spinner';
import { useWorkflowAnalytics } from '@/features/workflow/api';
import { WindowSelector, type WindowValue } from './WindowSelector';
import { LowDataBanner } from './LowDataBanner';
import { HeadlineStrip } from './HeadlineStrip';
import { InstanceCountChart } from './InstanceCountChart';
import { BottleneckStatesChart } from './BottleneckStatesChart';
import { ActionRatesChart } from './ActionRatesChart';
import { StuckInstancesTable } from './StuckInstancesTable';
import { ApproverActivityTable } from './ApproverActivityTable';

interface Props {
  definitionId: string;
}

const DEFAULT_WINDOW: WindowValue = '30d';
const LOW_DATA_THRESHOLD = 5;

export function WorkflowAnalyticsTab({ definitionId }: Props) {
  const { t: _t } = useTranslation();
  const [params, setParams] = useSearchParams();
  const window = (params.get('window') as WindowValue) || DEFAULT_WINDOW;

  const { data, isLoading } = useWorkflowAnalytics(definitionId, window);

  const setWindow = (w: WindowValue) => {
    const next = new URLSearchParams(params);
    next.set('window', w);
    setParams(next, { replace: true });
  };

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  if (!data) return null;

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-end">
        <WindowSelector value={window} onChange={setWindow} />
      </div>

      {data.instancesInWindow < LOW_DATA_THRESHOLD && (
        <LowDataBanner count={data.instancesInWindow} />
      )}

      <HeadlineStrip headline={data.headline} />

      <InstanceCountChart series={data.instanceCountSeries} />

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <BottleneckStatesChart states={data.statesByBottleneck} />
        <ActionRatesChart rates={data.actionRates} />
      </div>

      <StuckInstancesTable rows={data.stuckInstances} />
      <ApproverActivityTable rows={data.approverActivity} />
    </div>
  );
}
