import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Spinner } from '@/components/ui/spinner';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/common';
import { useBackNavigation, usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import { useWorkflowDefinition, useCloneDefinition, useUpdateDefinition } from '../api';
import { WorkflowAnalyticsTab } from '../components/analytics/WorkflowAnalyticsTab';

export default function WorkflowDefinitionDetailPage() {
  const { t } = useTranslation();
  const { id } = useParams<{ id: string }>();
  useBackNavigation('/workflows/definitions', t('workflow.definitions.title'));

  const { data: def, isLoading } = useWorkflowDefinition(id!);
  const { mutate: cloneDefinition, isPending: cloning } = useCloneDefinition();
  const { mutate: updateDefinition, isPending: updating } = useUpdateDefinition();

  const { hasPermission } = usePermissions();
  const [editName, setEditName] = useState('');
  const [isEditing, setIsEditing] = useState(false);

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
                        <p className="text-xs font-medium text-muted-foreground">{t('workflow.forms.required', 'Form Fields')}:</p>
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
                          <span className="font-medium">{t('workflow.sla.overdue', 'SLA')}:</span>
                          {state.sla.reminderAfterHours != null && (
                            <span className="ms-1">⏰ Reminder after {state.sla.reminderAfterHours}h</span>
                          )}
                          {state.sla.escalateAfterHours != null && (
                            <span className="ms-1">🔺 Escalate after {state.sla.escalateAfterHours}h</span>
                          )}
                        </p>
                      </div>
                    )}
                    {state.parallel && (
                      <div className="mt-2">
                        <p className="text-xs text-muted-foreground">
                          <span className="font-medium">{t('workflow.parallel.progress', 'Parallel')}:</span>
                          <span className="ms-1">{state.parallel.mode} — {state.parallel.assignees.length} assignee(s)</span>
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
      <PageHeader
        title={def.name}
        actions={
          <div className="flex items-center gap-2">
            {def.isTemplate ? (
              <Button onClick={() => cloneDefinition(id!)} disabled={cloning}>
                {t('workflow.detail.cloneToCustomize')}
              </Button>
            ) : (
              !isEditing && (
                <Button variant="outline" onClick={handleEdit}>
                  {t('workflow.definitions.edit')}
                </Button>
              )
            )}
          </div>
        }
      />

      {/* Header info */}
      <Card>
        <CardContent className="py-5">
          <div className="flex flex-wrap items-center gap-3">
            <Badge variant="secondary">{def.entityType}</Badge>
            <Badge variant={def.isTemplate ? 'outline' : 'default'}>
              {def.isTemplate
                ? t('workflow.definitions.systemTemplate')
                : t('workflow.definitions.customized')}
            </Badge>
            {def.isActive ? (
              <Badge variant="default">{t('workflow.status.active')}</Badge>
            ) : (
              <Badge variant="secondary">{t('common.inactive')}</Badge>
            )}
          </div>
        </CardContent>
      </Card>

      {showAnalyticsTab ? (
        <Tabs defaultValue="overview">
          <TabsList>
            <TabsTrigger value="overview">{t('workflow.detail.tabOverview', 'Overview')}</TabsTrigger>
            <TabsTrigger value="analytics">{t('workflow.detail.tabAnalytics', 'Analytics')}</TabsTrigger>
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
