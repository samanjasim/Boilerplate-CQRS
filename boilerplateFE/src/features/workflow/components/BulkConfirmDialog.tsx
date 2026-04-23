import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';

type BulkAction = 'Approve' | 'Reject' | 'ReturnForRevision';

interface Props {
  action: BulkAction | null;
  count: number;
  isPending: boolean;
  onSubmit: (comment: string | undefined) => void;
  onCancel: () => void;
}

const ACTION_LABEL_KEY: Record<BulkAction, string> = {
  Approve: 'workflow.inbox.approve',
  Reject: 'workflow.inbox.reject',
  ReturnForRevision: 'workflow.inbox.return',
};

export function BulkConfirmDialog({ action, count, isPending, onSubmit, onCancel }: Props) {
  const { t } = useTranslation();
  const [comment, setComment] = useState('');

  const open = action !== null;
  const actionLabel = action ? t(ACTION_LABEL_KEY[action]) : '';

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (!next) onCancel();
      }}
    >
      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            {t('workflow.inbox.bulkConfirmTitle', { action: actionLabel, count })}
          </DialogTitle>
          <DialogDescription>{t('workflow.inbox.bulkConfirmDesc')}</DialogDescription>
        </DialogHeader>
        <Textarea
          placeholder={t('workflow.inbox.bulkCommentPlaceholder')}
          value={comment}
          onChange={(e) => setComment(e.target.value)}
          disabled={isPending}
          maxLength={2000}
        />
        <DialogFooter>
          <Button variant="ghost" onClick={onCancel} disabled={isPending}>
            {t('common.cancel', 'Cancel')}
          </Button>
          <Button
            onClick={() => {
              onSubmit(comment.trim() || undefined);
              setComment('');
            }}
            disabled={isPending}
          >
            {t('common.confirm', 'Confirm')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
