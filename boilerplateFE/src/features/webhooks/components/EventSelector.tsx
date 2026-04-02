import { useTranslation } from 'react-i18next';
import { Label } from '@/components/ui/label';
import { useWebhookEventTypes } from '../api';
import type { WebhookEventType } from '@/types';

interface EventSelectorProps {
  selectedEvents: string[];
  onChange: (events: string[]) => void;
}

function groupEventTypes(eventTypes: WebhookEventType[]): Record<string, WebhookEventType[]> {
  return eventTypes.reduce<Record<string, WebhookEventType[]>>((acc, et) => {
    if (!acc[et.resource]) acc[et.resource] = [];
    acc[et.resource].push(et);
    return acc;
  }, {});
}

export function EventSelector({ selectedEvents, onChange }: EventSelectorProps) {
  const { t } = useTranslation();
  const { data: eventTypes = [] } = useWebhookEventTypes();

  const grouped = groupEventTypes(eventTypes);

  const toggleEvent = (eventType: string) => {
    onChange(
      selectedEvents.includes(eventType)
        ? selectedEvents.filter((e) => e !== eventType)
        : [...selectedEvents, eventType],
    );
  };

  const toggleGroup = (resource: string) => {
    const groupEvents = (grouped[resource] ?? []).map((e) => e.type);
    const allSelected = groupEvents.every((e) => selectedEvents.includes(e));
    onChange(
      allSelected
        ? selectedEvents.filter((e) => !groupEvents.includes(e))
        : [...new Set([...selectedEvents, ...groupEvents])],
    );
  };

  return (
    <div className="space-y-2">
      <Label>{t('webhooks.selectEvents')}</Label>
      {eventTypes.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t('common.loading')}</p>
      ) : (
        <div className="space-y-3 rounded-xl border border-border p-4 max-h-56 overflow-y-auto">
          {Object.entries(grouped).map(([resource, types]) => {
            const groupEvents = types.map((t) => t.type);
            const allSelected = groupEvents.every((e) => selectedEvents.includes(e));
            const someSelected = groupEvents.some((e) => selectedEvents.includes(e));

            return (
              <div key={resource} className="space-y-1.5">
                {/* Group heading */}
                <label className="flex items-center gap-2 cursor-pointer select-none">
                  <input
                    type="checkbox"
                    checked={allSelected}
                    ref={(el) => {
                      if (el) el.indeterminate = someSelected && !allSelected;
                    }}
                    onChange={() => toggleGroup(resource)}
                    className="accent-primary h-3.5 w-3.5"
                  />
                  <span className="text-xs font-semibold text-foreground uppercase tracking-wide">
                    {resource}
                  </span>
                </label>
                {/* Individual events */}
                <div className="ml-5 space-y-1">
                  {types.map((et) => (
                    <label
                      key={et.type}
                      className="flex items-center gap-2 cursor-pointer select-none"
                    >
                      <input
                        type="checkbox"
                        checked={selectedEvents.includes(et.type)}
                        onChange={() => toggleEvent(et.type)}
                        className="accent-primary h-3.5 w-3.5"
                      />
                      <span className="text-sm text-foreground">{et.type}</span>
                      {et.description && (
                        <span className="text-xs text-muted-foreground">— {et.description}</span>
                      )}
                    </label>
                  ))}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
