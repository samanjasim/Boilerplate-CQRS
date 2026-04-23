import { useTranslation } from 'react-i18next';
import { Save, LayoutGrid, Plus, AlertTriangle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useDesignerStore } from './hooks/useDesignerStore';

interface Props {
  onSave: () => void;
  onAutoLayout: () => void;
  onAddState: () => void;
  saving: boolean;
  readOnly?: boolean;
}

export function DesignerToolbar({ onSave, onAutoLayout, onAddState, saving, readOnly = false }: Props) {
  const { t } = useTranslation();
  const isDirty = useDesignerStore(s => s.isDirty);
  const errors = useDesignerStore(s => s.issues.length);

  const saveDisabled = readOnly || !isDirty || errors > 0 || saving;

  return (
    <div className="flex items-center justify-between gap-2 border-b border-border bg-card px-4 py-2">
      <div className="flex items-center gap-2">
        {!readOnly && (
          <Button size="sm" variant="outline" onClick={onAddState}>
            <Plus className="h-4 w-4 ltr:mr-1.5 rtl:ml-1.5" />
            {t('workflow.designer.state.addState', 'Add State')}
          </Button>
        )}
        {!readOnly && (
          <Button size="sm" variant="outline" onClick={onAutoLayout}>
            <LayoutGrid className="h-4 w-4 ltr:mr-1.5 rtl:ml-1.5" />
            {t('workflow.designer.autoLayout')}
          </Button>
        )}
      </div>
      <div className="flex items-center gap-2">
        {errors > 0 && (
          <span className="inline-flex items-center gap-1 text-xs text-destructive">
            <AlertTriangle className="h-3.5 w-3.5" />
            {errors}
          </span>
        )}
        {!readOnly && (
          <Button size="sm" onClick={onSave} disabled={saveDisabled}>
            <Save className="h-4 w-4 ltr:mr-1.5 rtl:ml-1.5" />
            {saving ? t('workflow.designer.saving') : t('workflow.designer.save')}
          </Button>
        )}
      </div>
    </div>
  );
}
