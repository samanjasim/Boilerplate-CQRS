import { useTranslation } from 'react-i18next';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { JsonBlockField } from './JsonBlockField';
import { useDesignerStore } from './hooks/useDesignerStore';
import type { ConditionConfig } from '@/types/workflow.types';

interface Props {
  edgeId: string;
  readOnly?: boolean;
}

export function TransitionEditor({ edgeId, readOnly = false }: Props) {
  const { t } = useTranslation();
  const edge = useDesignerStore(s => s.edges.find(e => e.id === edgeId));
  const update = useDesignerStore(s => s.updateTransitionById);

  if (!edge) return null;

  return (
    <div className="space-y-4 p-4">
      <div className="space-y-1.5">
        <Label>{t('workflow.designer.transition.trigger')}</Label>
        <Input
          value={edge.data?.trigger ?? ''}
          onChange={e => update(edge.id, { trigger: e.target.value })}
          disabled={readOnly}
        />
      </div>
      <div className="grid grid-cols-2 gap-2">
        <div className="space-y-1.5">
          <Label className="text-xs">{t('workflow.designer.transition.from')}</Label>
          <Input value={edge.source} readOnly />
        </div>
        <div className="space-y-1.5">
          <Label className="text-xs">{t('workflow.designer.transition.to')}</Label>
          <Input value={edge.target} readOnly />
        </div>
      </div>
      <div className="space-y-1.5">
        <Label>{t('workflow.designer.transition.type')}</Label>
        <select
          className="w-full rounded-xl border border-border bg-background px-3 py-2 text-sm"
          value={edge.data?.type ?? 'Manual'}
          onChange={e => update(edge.id, { type: e.target.value })}
          disabled={readOnly}
        >
          <option value="Manual">{t('workflow.designer.transition.typeManual')}</option>
          <option value="Auto">{t('workflow.designer.transition.typeAuto')}</option>
        </select>
      </div>
      <JsonBlockField
        label={t('workflow.designer.transition.condition')}
        value={edge.data?.condition ?? null}
        onChange={v => update(edge.id, { condition: v as ConditionConfig | null })}
        placeholder='{ "field": "amount", "operator": ">", "value": 1000 }'
        disabled={readOnly}
      />
    </div>
  );
}
