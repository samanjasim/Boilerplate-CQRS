import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { useExecuteTask } from '../api';
import { DynamicFormRenderer } from './DynamicFormRenderer';
import type { FormFieldDefinition } from '@/types/workflow.types';

interface ApprovalDialogProps {
  taskId: string;
  definitionName: string;
  entityType: string;
  entityId: string;
  actions: string[];
  formFields?: FormFieldDefinition[] | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

const ACTION_LABELS: Record<string, string> = {
  Approve: 'workflow.inbox.approve',
  Reject: 'workflow.inbox.reject',
  ReturnForRevision: 'workflow.inbox.return',
};

const ACTION_VARIANTS: Record<string, 'default' | 'outline' | 'ghost'> = {
  Approve: 'default',
  Reject: 'outline',
  ReturnForRevision: 'ghost',
};

export function ApprovalDialog({
  taskId,
  definitionName,
  entityType,
  entityId,
  actions,
  formFields,
  open,
  onOpenChange,
}: ApprovalDialogProps) {
  const { t } = useTranslation();
  const [comment, setComment] = useState('');
  const [formData, setFormData] = useState<Record<string, unknown>>({});
  const [formErrors, setFormErrors] = useState<Record<string, string>>({});
  const { mutate: executeTask, isPending } = useExecuteTask();

  const hasFormFields = formFields && formFields.length > 0;

  const handleFormChange = (name: string, value: unknown) => {
    setFormData((prev) => ({ ...prev, [name]: value }));
    // Clear error on change
    if (formErrors[name]) {
      setFormErrors((prev) => {
        const next = { ...prev };
        delete next[name];
        return next;
      });
    }
  };

  const validateForm = (): boolean => {
    if (!hasFormFields) return true;
    const errors: Record<string, string> = {};
    for (const field of formFields) {
      if (field.required) {
        const value = formData[field.name];
        if (value === undefined || value === null || value === '') {
          errors[field.name] = t('workflow.forms.required');
        }
      }
    }
    setFormErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const handleAction = (action: string) => {
    if (!validateForm()) return;

    executeTask(
      {
        taskId,
        data: {
          action,
          comment: comment || undefined,
          ...(hasFormFields ? { formData } : {}),
        },
      },
      {
        onSuccess: () => {
          setComment('');
          setFormData({});
          setFormErrors({});
          onOpenChange(false);
        },
      },
    );
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t('workflow.approval.title')}</DialogTitle>
          <DialogDescription>
            {definitionName}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="flex items-center gap-2">
            <Badge variant="secondary">{entityType}</Badge>
            <span className="text-sm text-muted-foreground truncate">
              {entityId.substring(0, 8)}...
            </span>
          </div>

          {hasFormFields && (
            <DynamicFormRenderer
              fields={formFields}
              values={formData}
              onChange={handleFormChange}
              errors={formErrors}
            />
          )}

          <div className="space-y-2">
            <label className="text-sm font-medium text-foreground">
              {t('workflow.approval.comment')}
            </label>
            <Textarea
              value={comment}
              onChange={(e) => setComment(e.target.value)}
              placeholder={t('workflow.approval.comment')}
              rows={3}
            />
            <p className="text-xs text-muted-foreground">
              {t('workflow.approval.commentHint')}
            </p>
          </div>
        </div>

        <DialogFooter>
          <div className="flex items-center gap-2 w-full sm:justify-end">
            {actions.map((action) => (
              <Button
                key={action}
                variant={ACTION_VARIANTS[action] ?? 'outline'}
                onClick={() => handleAction(action)}
                disabled={isPending}
              >
                {t(ACTION_LABELS[action] ?? action)}
              </Button>
            ))}
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
