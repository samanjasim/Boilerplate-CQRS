import { useFeatureFlag } from '@/hooks';
import { EntityTimeline } from './EntityTimeline';

interface EntityTimelineSlotProps {
  entityType: string;
  entityId: string;
  tenantId?: string;
}

export function EntityTimelineSlot(props: EntityTimelineSlotProps) {
  const { isEnabled, isLoading } = useFeatureFlag('comments.activity_enabled');

  if (isLoading) return null;
  if (!isEnabled) return null;

  return <EntityTimeline {...props} />;
}
