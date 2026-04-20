import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useQueryClient } from '@tanstack/react-query';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { toast } from 'sonner';
import { queryKeys } from '@/lib/query/keys';
import { useWorkflowDefinitions, useStartWorkflow } from '../api';
import type { WorkflowDefinitionSummary } from '@/types/workflow.types';

interface NewRequestDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function NewRequestDialog({ open, onOpenChange }: NewRequestDialogProps) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [selectedDefinitionId, setSelectedDefinitionId] = useState('');
  const [entityDisplayName, setEntityDisplayName] = useState('');

  const { data: definitionsData, isLoading: defsLoading } = useWorkflowDefinitions();
  const { mutate: startWorkflow, isPending } = useStartWorkflow();

  const allDefinitions: WorkflowDefinitionSummary[] = Array.isArray(definitionsData)
    ? definitionsData
    : definitionsData?.data ?? [];

  const activeDefinitions = allDefinitions.filter((d) => d.isActive);

  const selectedDefinition = activeDefinitions.find((d) => d.id === selectedDefinitionId) ?? null;

  const canSubmit = !!selectedDefinition && entityDisplayName.trim().length > 0 && !isPending;

  const handleSubmit = () => {
    if (!selectedDefinition || !entityDisplayName.trim()) return;

    startWorkflow(
      {
        entityType: selectedDefinition.entityType,
        entityId: crypto.randomUUID(),
        definitionName: selectedDefinition.name,
        entityDisplayName: entityDisplayName.trim(),
      },
      {
        onSuccess: () => {
          toast.success(t('workflow.newRequest.success'));
          queryClient.invalidateQueries({ queryKey: queryKeys.workflow.instances.all });
          queryClient.invalidateQueries({ queryKey: queryKeys.workflow.tasks.all });
          handleOpenChange(false);
        },
      },
    );
  };

  const handleOpenChange = (isOpen: boolean) => {
    if (!isOpen) {
      setSelectedDefinitionId('');
      setEntityDisplayName('');
    }
    onOpenChange(isOpen);
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t('workflow.newRequest.title')}</DialogTitle>
          <DialogDescription>{t('workflow.newRequest.description')}</DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          {/* Workflow Type */}
          <div className="space-y-1.5">
            <Label htmlFor="workflow-type">{t('workflow.newRequest.workflowType')}</Label>
            <Select
              value={selectedDefinitionId}
              onValueChange={setSelectedDefinitionId}
              disabled={defsLoading}
            >
              <SelectTrigger id="workflow-type">
                <SelectValue placeholder={t('workflow.newRequest.selectWorkflow')} />
              </SelectTrigger>
              <SelectContent>
                {activeDefinitions.map((def) => (
                  <SelectItem key={def.id} value={def.id}>
                    {def.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Request Name */}
          <div className="space-y-1.5">
            <Label htmlFor="request-name">{t('workflow.newRequest.requestName')}</Label>
            <Input
              id="request-name"
              value={entityDisplayName}
              onChange={(e) => setEntityDisplayName(e.target.value)}
              placeholder={t('workflow.newRequest.requestNamePlaceholder')}
            />
          </div>
        </div>

        <DialogFooter>
          <Button onClick={handleSubmit} disabled={!canSubmit}>
            {isPending ? t('common.saving') : t('workflow.newRequest.submit')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
