import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Spinner } from '@/components/ui/spinner';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Layers, Workflow as WorkflowIcon } from 'lucide-react';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import { STATUS_BADGE_VARIANT } from '@/constants/status';
import { ROUTES } from '@/config';
import { JsonView } from '@/features/audit-logs/components/JsonView';
import { useWorkflowDefinition, useCloneDefinition, useUpdateDefinition } from '../api';
import { WorkflowAnalyticsTab } from '../components/analytics/WorkflowAnalyticsTab';
import { DesignerCanvas } from '../components/designer/DesignerCanvas';
import { useDesignerStore } from '../components/designer/hooks/useDesignerStore';
import { WorkflowStatusHeader } from '../components/WorkflowStatusHeader';
import type { WorkflowStateConfig, WorkflowTransitionConfig } from '@/types/workflow.types';

function DefinitionCanvasPreview({
  states,
  transitions,
}: {
  states: WorkflowStateConfig[];
  transitions: WorkflowTransitionConfig[];
}) {
  const load = useDesignerStore((state) => state.load);

  useEffect(() => {
    load(states, transitions);
  }, [load, states, transitions]);

  return <DesignerCanvas readOnly />;
}

export default function WorkflowDefinitionDetailPage() {
  const { t } = useTranslation();
  const { id } = useParams<{ id: string }>();

  const { data: def, isLoading } = useWorkflowDefinition(id!);
  const { mutate: cloneDefinition, isPending: cloning } = useCloneDefinition();
  const { mutate: updateDefinition, isPending: updating } = useUpdateDefinition();

  const navigate = useNavigate();
  const { hasPermission } = usePermissions();
  const [editName, setEditName] = useState('');
  const [isEditing, setIsEditing] = useState(false);
  const [showRawJson, setShowRawJson] = useState(false);

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  if (!def) return null;

  const canViewAnalytics = hasPermission(PERMISSIONS.Workflows.ViewAnalytics);
  const showAnalyticsTab = canViewAnalytics && !def.isTemplate;
  const definitionTitle = def.name;
  const definitionStepCount = def.states?.length ?? 0;

  const handleEdit = () => {
    setEditName(def.name);
    setIsEditing(true);
  };

  const handleSave = () => {
    updateDefinition(
      { id: id!, data: { displayName: editName } },
      { onSuccess: () => setIsEditing(false) },
    );
  };

  const overviewContent = (
    <>
      {/* Editable fields for custom definitions */}
      {isEditing && !def.isTemplate && (
        <Card>
          <CardContent className="py-5 space-y-4">
            <div className="space-y-2">
              <Label>{t('workflow.definitions.name')}</Label>
              <Input value={editName} onChange={(e) => setEditName(e.target.value)} />
            </div>
            <div className="flex items-center gap-2">
              <Button onClick={handleSave} disabled={updating}>
                {updating ? t('common.saving') : t('common.save')}
              </Button>
              <Button variant="outline" onClick={() => setIsEditing(false)}>
                {t('common.cancel')}
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      <section className="space-y-3">
        <Card variant="glass">
          <CardContent className="p-0">
            <div className="h-[420px] max-h-[60vh]">
              <DefinitionCanvasPreview
                states={def.states ?? []}
                transitions={def.transitions ?? []}
              />
            </div>
          </CardContent>
        </Card>

        <div className="space-y-3">
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={() => setShowRawJson((value) => !value)}
          >
            {t('workflow.detail.viewRawJson')}
          </Button>
          {showRawJson && (
            <Card>
              <CardContent className="py-4">
                <JsonView
                  payload={JSON.stringify({
                    states: def.states ?? [],
                    transitions: def.transitions ?? [],
                  })}
                />
              </CardContent>
            </Card>
          )}
        </div>
      </section>

      {/* States list */}
      <section className="space-y-3">
        <h2 className="text-base font-semibold text-foreground">{t('workflow.detail.stateList')}</h2>
        <div className="space-y-3">
          {def.states?.map((state, index) => (
            <Card key={state.name}>
              <CardContent className="py-4">
                <div className="flex items-start justify-between gap-4">
                  <div className="space-y-1.5">
                    <div className="flex items-center gap-2">
                      <span className="text-xs font-medium text-muted-foreground">
                        {index + 1}.
                      </span>
                      <h3 className="text-sm font-semibold text-foreground">
                        {state.displayName || state.name}
                      </h3>
                      <Badge variant="outline" className="text-xs">{state.type}</Badge>
                    </div>
                    {state.assignee && (
                      <p className="text-xs text-muted-foreground">
                        {t('workflow.detail.assignee')}: {state.assignee.strategy}
                        {state.assignee.parameters && Object.keys(state.assignee.parameters).length > 0 && (
                          <> ({Object.entries(state.assignee.parameters).map(([k, v]) => `${k}: ${v}`).join(', ')})</>
                        )}
                      </p>
                    )}
                    {state.actions && state.actions.length > 0 && (
                      <div className="flex items-center gap-1.5 mt-1">
                        {state.actions.map((action) => (
                          <Badge key={action} variant="secondary" className="text-xs">
                            {action}
                          </Badge>
                        ))}
                      </div>
                    )}
                    {state.formFields && state.formFields.length > 0 && (
                      <div className="mt-2 space-y-1">
                        <p className="text-xs font-medium text-muted-foreground">{t('workflow.forms.fields')}:</p>
                        <div className="flex flex-wrap gap-1.5">
                          {state.formFields.map((f) => (
                            <Badge key={f.name} variant="outline" className="text-xs">
                              {f.label} ({f.type}){f.required ? ' *' : ''}
                            </Badge>
                          ))}
                        </div>
                      </div>
                    )}
                    {state.sla && (
                      <div className="mt-2">
                        <p className="text-xs text-muted-foreground">
                          <span className="font-medium">{t('workflow.sla.title')}:</span>
                          {state.sla.reminderAfterHours != null && (
                            <span className="ms-1">
                              {t('workflow.sla.reminderAfterHours', { hours: state.sla.reminderAfterHours })}
                            </span>
                          )}
                          {state.sla.escalateAfterHours != null && (
                            <span className="ms-1">
                              {t('workflow.sla.escalateAfterHours', { hours: state.sla.escalateAfterHours })}
                            </span>
                          )}
                        </p>
                      </div>
                    )}
                    {state.parallel && (
                      <div className="mt-2">
                        <p className="text-xs text-muted-foreground">
                          <span className="font-medium">{t('workflow.parallel.title')}:</span>
                          <span className="ms-1">
                            {t('workflow.parallel.assigneeCount', {
                              mode: state.parallel.mode,
                              count: state.parallel.assignees.length,
                            })}
                          </span>
                        </p>
                      </div>
                    )}
                  </div>
                  {(state.onEnter || state.onExit) && (
                    <div className="text-xs text-muted-foreground">
                      <span className="font-medium">{t('workflow.detail.hooks')}:</span>
                      {state.onEnter && <span className="ms-1">onEnter({state.onEnter.length})</span>}
                      {state.onExit && <span className="ms-1">onExit({state.onExit.length})</span>}
                    </div>
                  )}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      </section>
    </>
  );

  return (
    <div className="space-y-6">
      <WorkflowStatusHeader
        title={definitionTitle}
        status={def.isActive
          ? t('workflow.definitions.statusValue.active')
          : t('workflow.definitions.statusValue.inactive')}
        statusVariant={STATUS_BADGE_VARIANT[def.isActive ? 'Active' : 'Inactive'] ?? 'outline'}
        chips={[
          { icon: <Layers className="h-3 w-3" />, label: def.entityType, tinted: true },
          { label: `${t('workflow.definitions.steps')}: ${definitionStepCount}` },
          {
            label: def.isTemplate
              ? t('workflow.definitions.systemTemplate')
              : t('workflow.definitions.customized'),
          },
        ]}
        actions={
          <div className="flex items-center gap-2">
            {def.isTemplate ? (
              <>
                <Button
                  variant="outline"
                  onClick={() => navigate(ROUTES.WORKFLOWS.getDefinitionDesigner(id!))}
                >
                  <WorkflowIcon className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
                  {t('workflow.designer.openDesigner')}
                </Button>
                <Button onClick={() => cloneDefinition(id!)} disabled={cloning}>
                  {t('workflow.detail.cloneToCustomize')}
                </Button>
              </>
            ) : (
              <>
                {!isEditing && hasPermission(PERMISSIONS.Workflows.ManageDefinitions) && (
                  <Button
                    variant="default"
                    onClick={() => navigate(ROUTES.WORKFLOWS.getDefinitionDesigner(id!))}
                  >
                    <WorkflowIcon className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
                    {t('workflow.designer.openDesigner')}
                  </Button>
                )}
                {!isEditing && (
                  <Button variant="outline" onClick={handleEdit}>
                    {t('workflow.definitions.edit')}
                  </Button>
                )}
              </>
            )}
          </div>
        }
      />

      {showAnalyticsTab ? (
        <Tabs defaultValue="overview">
          <TabsList>
            <TabsTrigger value="overview">{t('workflow.detail.tabOverview')}</TabsTrigger>
            <TabsTrigger value="analytics">{t('workflow.detail.tabAnalytics')}</TabsTrigger>
          </TabsList>
          <TabsContent value="overview" className="space-y-4 mt-4">
            {overviewContent}
          </TabsContent>
          <TabsContent value="analytics" className="mt-4">
            <WorkflowAnalyticsTab definitionId={id!} />
          </TabsContent>
        </Tabs>
      ) : (
        <div className="space-y-4">
          {overviewContent}
        </div>
      )}
    </div>
  );
}
