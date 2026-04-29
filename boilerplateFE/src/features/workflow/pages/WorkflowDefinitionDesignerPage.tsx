import { useEffect, useRef, useCallback } from 'react';
import { useParams, useNavigate, useBlocker } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Spinner } from '@/components/ui/spinner';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { PageHeader, ConfirmDialog } from '@/components/common';
import { ROUTES } from '@/config';
import { useWorkflowDefinition, useUpdateDefinition, useCloneDefinition } from '../api';
import { DesignerCanvas } from '../components/designer/DesignerCanvas';
import { SidePanel } from '../components/designer/SidePanel';
import { DesignerToolbar } from '../components/designer/DesignerToolbar';
import { useDesignerStore } from '../components/designer/hooks/useDesignerStore';
import { useAutoLayout } from '../components/designer/hooks/useAutoLayout';

export default function WorkflowDefinitionDesignerPage() {
  const { t } = useTranslation();
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const { data: def, isLoading } = useWorkflowDefinition(id!);
  const { mutate: updateDefinition, isPending: saving } = useUpdateDefinition();
  const { mutate: cloneDefinition, isPending: cloning } = useCloneDefinition();

  const autoLayout = useAutoLayout();
  const load = useDesignerStore(s => s.load);
  const toDefinition = useDesignerStore(s => s.toDefinition);
  const markClean = useDesignerStore(s => s.markClean);
  const isDirty = useDesignerStore(s => s.isDirty);
  const setNodesFromLayout = useDesignerStore(s => s.setNodesFromLayout);
  const addState = useDesignerStore(s => s.addState);

  const loaded = useRef(false);

  // Load definition into the store once
  useEffect(() => {
    if (!def || loaded.current) return;
    load(def.states ?? [], def.transitions ?? []);

    // Auto-layout if no positions exist (display-only; does NOT mark dirty)
    const hasPositions = (def.states ?? []).some(s => s.uiPosition);
    if (!hasPositions) {
      const positioned = autoLayout(useDesignerStore.getState().nodes, useDesignerStore.getState().edges);
      setNodesFromLayout(positioned);
      markClean(); // first-open auto-layout is not a user edit
    }

    loaded.current = true;
  }, [def, load, autoLayout, setNodesFromLayout, markClean]);

  // Navigate-away guard
  const blocker = useBlocker(({ currentLocation, nextLocation }) =>
    isDirty && currentLocation.pathname !== nextLocation.pathname,
  );

  // Before unload (browser close/refresh)
  useEffect(() => {
    const handler = (e: BeforeUnloadEvent) => {
      if (isDirty) { e.preventDefault(); e.returnValue = ''; }
    };
    window.addEventListener('beforeunload', handler);
    return () => window.removeEventListener('beforeunload', handler);
  }, [isDirty]);

  const handleSave = useCallback(() => {
    if (!id) return;
    const { states, transitions } = toDefinition();
    updateDefinition(
      {
        id,
        data: {
          statesJson: JSON.stringify(states),
          transitionsJson: JSON.stringify(transitions),
        },
      },
      { onSuccess: () => markClean() },
    );
  }, [id, updateDefinition, toDefinition, markClean]);

  const handleAutoLayout = useCallback(() => {
    const positioned = autoLayout(useDesignerStore.getState().nodes, useDesignerStore.getState().edges);
    setNodesFromLayout(positioned);
    // Explicit user action — mark dirty
    useDesignerStore.setState({ isDirty: true });
  }, [autoLayout, setNodesFromLayout]);

  const handleAddState = useCallback(() => {
    const existing = useDesignerStore.getState().nodes;
    let i = existing.length + 1;
    let name = `State${i}`;
    while (existing.some(n => n.id === name)) { i += 1; name = `State${i}`; }
    addState(
      { name, displayName: `State ${i}`, type: 'HumanTask' },
      { x: 80 + (i % 5) * 50, y: 80 + i * 30 },
    );
  }, [addState]);

  const handleClone = useCallback(() => {
    if (!id) return;
    cloneDefinition(id, {
      onSuccess: (cloneId) => navigate(ROUTES.WORKFLOWS.getDefinitionDesigner(String(cloneId))),
    });
  }, [id, cloneDefinition, navigate]);

  if (isLoading) {
    return <div className="flex justify-center py-12"><Spinner size="lg" /></div>;
  }
  if (!def) return null;

  const readOnly = def.isTemplate;

  return (
    <div className="flex flex-col h-[calc(100vh-5rem)]">
      <PageHeader
        title={`${def.name} — ${t('workflow.designer.title')}`}
        breadcrumbs={[
          { to: '/workflows/definitions', label: t('workflow.definitions.title') },
          { to: ROUTES.WORKFLOWS.getDefinitionDetail(id!), label: def?.name ?? t('common.loading') },
          { label: t('workflow.definitions.designer.title', 'Designer') },
        ]}
      />
      {readOnly && (
        <Card variant="glass" className="m-4">
          <CardContent className="py-4 flex items-center justify-between gap-4">
            <div>
              <h3 className="text-sm font-semibold">{t('workflow.designer.template.readOnlyTitle')}</h3>
              <p className="text-xs text-muted-foreground">{t('workflow.designer.template.readOnlyBody')}</p>
            </div>
            <Button onClick={handleClone} disabled={cloning}>
              {t('workflow.designer.template.cloneToEdit')}
            </Button>
          </CardContent>
        </Card>
      )}
      <DesignerToolbar
        onSave={handleSave}
        onAutoLayout={handleAutoLayout}
        onAddState={handleAddState}
        saving={saving}
        readOnly={readOnly}
      />
      <div className="flex flex-1 min-h-0">
        <div className="flex-1 min-w-0">
          <DesignerCanvas readOnly={readOnly} />
        </div>
        <SidePanel readOnly={readOnly} />
      </div>

      <ConfirmDialog
        isOpen={blocker.state === 'blocked'}
        onClose={() => blocker.state === 'blocked' && blocker.reset()}
        title={t('workflow.designer.unsavedWarningTitle')}
        description={t('workflow.designer.unsavedWarningBody')}
        onConfirm={() => blocker.state === 'blocked' && blocker.proceed()}
        confirmLabel={t('workflow.designer.discard')}
        variant="danger"
      />
    </div>
  );
}
