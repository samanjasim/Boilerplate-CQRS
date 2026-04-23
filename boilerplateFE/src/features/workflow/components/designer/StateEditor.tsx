import { useTranslation } from 'react-i18next';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { X } from 'lucide-react';
import { useDesignerStore } from './hooks/useDesignerStore';
import type { WorkflowStateConfig } from '@/types/workflow.types';

const STATE_TYPES = ['Initial', 'HumanTask', 'SystemAction', 'Terminal'] as const;
const BUILTIN_STRATEGIES = ['SpecificUser', 'Role', 'EntityCreator'] as const;

interface Props {
  stateName: string;
  readOnly?: boolean;
}

export function StateEditor({ stateName, readOnly = false }: Props) {
  const { t } = useTranslation();
  const node = useDesignerStore(s => s.nodes.find(n => n.id === stateName));
  const update = useDesignerStore(s => s.updateStateByName);

  if (!node) return null;
  const state = node.data;

  const patch = (p: Partial<WorkflowStateConfig>) => update(state.name, p);

  return (
    <div className="space-y-4 p-4">
      {/* Identity */}
      <section className="space-y-2">
        <h4 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          {t('workflow.designer.state.identity')}
        </h4>
        <div className="space-y-1.5">
          <Label>{t('workflow.designer.state.name')}</Label>
          <Input
            value={state.name}
            onChange={e => patch({ name: e.target.value })}
            disabled={readOnly}
          />
          <p className="text-[11px] text-muted-foreground">{t('workflow.designer.state.nameSlugHelp')}</p>
        </div>
        <div className="space-y-1.5">
          <Label>{t('workflow.designer.state.displayName')}</Label>
          <Input
            value={state.displayName}
            onChange={e => patch({ displayName: e.target.value })}
            disabled={readOnly}
          />
        </div>
        <div className="space-y-1.5">
          <Label>{t('workflow.designer.state.type')}</Label>
          <select
            className="w-full rounded-xl border border-border bg-background px-3 py-2 text-sm"
            value={state.type}
            onChange={e => patch({ type: e.target.value })}
            disabled={readOnly}
          >
            {STATE_TYPES.map(tt => <option key={tt} value={tt}>{tt}</option>)}
          </select>
        </div>
      </section>

      {/* Actions (HumanTask only) */}
      {state.type === 'HumanTask' && (
        <section className="space-y-2">
          <h4 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            {t('workflow.designer.state.actions')}
          </h4>
          <ActionChipInput
            value={state.actions ?? []}
            onChange={actions => patch({ actions })}
            disabled={readOnly}
          />
        </section>
      )}

      {/* Assignee (HumanTask only) */}
      {state.type === 'HumanTask' && (
        <section className="space-y-2">
          <h4 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            {t('workflow.designer.state.assignee')}
          </h4>
          <div className="space-y-1.5">
            <Label>{t('workflow.designer.state.strategy')}</Label>
            <select
              className="w-full rounded-xl border border-border bg-background px-3 py-2 text-sm"
              value={state.assignee?.strategy ?? ''}
              onChange={e => patch({ assignee: { ...(state.assignee ?? { parameters: {} }), strategy: e.target.value } })}
              disabled={readOnly}
            >
              <option value="">—</option>
              {BUILTIN_STRATEGIES.map(s => <option key={s} value={s}>{s}</option>)}
              {state.assignee?.strategy && !BUILTIN_STRATEGIES.includes(state.assignee.strategy as typeof BUILTIN_STRATEGIES[number]) && (
                <option value={state.assignee.strategy}>{state.assignee.strategy}</option>
              )}
            </select>
          </div>
          {state.assignee?.strategy === 'Role' && (
            <div className="space-y-1.5">
              <Label>{t('workflow.designer.state.roleName')}</Label>
              <Input
                value={String(state.assignee.parameters?.roleName ?? '')}
                onChange={e => patch({ assignee: { ...state.assignee!, parameters: { ...(state.assignee!.parameters ?? {}), roleName: e.target.value } } })}
                disabled={readOnly}
              />
            </div>
          )}
          {state.assignee?.strategy === 'SpecificUser' && (
            <div className="space-y-1.5">
              <Label>{t('workflow.designer.state.userId')}</Label>
              <Input
                value={String(state.assignee.parameters?.userId ?? '')}
                onChange={e => patch({ assignee: { ...state.assignee!, parameters: { ...(state.assignee!.parameters ?? {}), userId: e.target.value } } })}
                disabled={readOnly}
              />
            </div>
          )}
        </section>
      )}

      {/* SLA */}
      {state.type === 'HumanTask' && (
        <section className="space-y-2">
          <h4 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            {t('workflow.designer.state.sla')}
          </h4>
          <div className="grid grid-cols-2 gap-2">
            <div className="space-y-1.5">
              <Label className="text-xs">{t('workflow.designer.state.reminderAfterHours')}</Label>
              <Input
                type="number"
                min={0}
                value={state.sla?.reminderAfterHours ?? ''}
                onChange={e => patch({ sla: { ...(state.sla ?? {}), reminderAfterHours: e.target.value === '' ? null : Number(e.target.value) } })}
                disabled={readOnly}
              />
            </div>
            <div className="space-y-1.5">
              <Label className="text-xs">{t('workflow.designer.state.escalateAfterHours')}</Label>
              <Input
                type="number"
                min={0}
                value={state.sla?.escalateAfterHours ?? ''}
                onChange={e => patch({ sla: { ...(state.sla ?? {}), escalateAfterHours: e.target.value === '' ? null : Number(e.target.value) } })}
                disabled={readOnly}
              />
            </div>
          </div>
        </section>
      )}

      {/* Advanced (JSON blocks) wired in Task 13 */}
    </div>
  );
}

function ActionChipInput({ value, onChange, disabled }: { value: string[]; onChange: (v: string[]) => void; disabled?: boolean }) {
  return (
    <div className="flex flex-wrap gap-1">
      {value.map((a, i) => (
        <span key={a} className="inline-flex items-center gap-1 rounded bg-muted px-2 py-0.5 text-xs">
          {a}
          {!disabled && (
            <button
              type="button"
              onClick={() => onChange(value.filter((_, j) => j !== i))}
              className="opacity-60 hover:opacity-100"
              aria-label={`Remove ${a}`}
            >
              <X className="h-3 w-3" />
            </button>
          )}
        </span>
      ))}
      {!disabled && (
        <input
          type="text"
          className="rounded border border-border bg-background px-2 py-0.5 text-xs min-w-[120px]"
          placeholder="Add action, press Enter"
          onKeyDown={e => {
            if (e.key === 'Enter') {
              e.preventDefault();
              const v = (e.target as HTMLInputElement).value.trim();
              if (v && !value.includes(v)) onChange([...value, v]);
              (e.target as HTMLInputElement).value = '';
            }
          }}
        />
      )}
    </div>
  );
}
