import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { useWorkflowDefinitions, useStartWorkflow } from '../api';

interface NewRequestDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function NewRequestDialog({ open, onOpenChange }: NewRequestDialogProps) {
  const { t } = useTranslation();
  const [selectedDefinitionId, setSelectedDefinitionId] = useState('');
  const [entityDisplayName, setEntityDisplayName] = useState('');

  const { data: definitions = [], isLoading: defsLoading } = useWorkflowDefinitions();
  const { mutate: startWorkflow, isPending } = useStartWorkflow();

  const activeDefinitions = definitions.filter((d) => d.isActive);

  const selectedDefinition = activeDefinitions.find((d) => d.id === selectedDefinitionId) ?? null;

  const canSubmit = !!selectedDefinition && entityDisplayName.trim().length > 0 && !isPending;

  const handleSubmit = () => {
    if (!selectedDefinition || !entityDisplayName.trim()) return;

    // useStartWorkflow already toasts success and invalidates instances/tasks;
    // the dialog only needs to close when the mutation succeeds.
    startWorkflow(
      {
        entityType: selectedDefinition.entityType,
        entityId: crypto.randomUUID(),
        definitionName: selectedDefinition.name,
        entityDisplayName: entityDisplayName.trim(),
      },
      { onSuccess: () => handleOpenChange(false) },
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
                    {def.displayName || def.name}
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
