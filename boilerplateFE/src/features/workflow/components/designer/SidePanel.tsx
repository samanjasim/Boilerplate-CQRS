import { useTranslation } from 'react-i18next';
import { StateEditor } from './StateEditor';
import { TransitionEditor } from './TransitionEditor';
import { useDesignerStore } from './hooks/useDesignerStore';

interface Props {
  readOnly?: boolean;
}

export function SidePanel({ readOnly = false }: Props) {
  const { t } = useTranslation();
  const selection = useDesignerStore(s => s.selection);

  return (
    <aside className="w-[360px] shrink-0 border-l border-border bg-card overflow-auto">
      {selection.kind === 'state' && <StateEditor stateName={selection.name} readOnly={readOnly} />}
      {selection.kind === 'transition' && <TransitionEditor edgeId={selection.id} readOnly={readOnly} />}
      {selection.kind === 'empty' && (
        <div className="p-6 text-sm text-muted-foreground">
          {t('workflow.designer.emptySelection')}
        </div>
      )}
    </aside>
  );
}
