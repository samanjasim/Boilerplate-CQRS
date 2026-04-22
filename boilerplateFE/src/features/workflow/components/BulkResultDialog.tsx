import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { ChevronDown, ChevronRight } from 'lucide-react';
import type { BatchExecuteResult } from '@/types/workflow.types';

interface Props {
  result: BatchExecuteResult | null;
  taskLabels?: Record<string, string>;
  onClose: () => void;
}

export function BulkResultDialog({ result, taskLabels, onClose }: Props) {
  const { t } = useTranslation();
  const [expanded, setExpanded] = useState(false);

  const open = result !== null;

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (!next) onClose();
      }}
    >
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t('workflow.inbox.bulkResultTitle')}</DialogTitle>
        </DialogHeader>

        {result && (
          <div className="space-y-3">
            <p className="text-sm text-foreground">
              {t('workflow.inbox.bulkResultSummary', {
                succeeded: result.succeeded,
                failed: result.failed,
                skipped: result.skipped,
              })}
            </p>

            <button
              type="button"
              className="flex items-center gap-1 text-sm text-primary hover:underline"
              onClick={() => setExpanded((x) => !x)}
            >
              {expanded ? (
                <ChevronDown className="h-4 w-4" />
              ) : (
                <ChevronRight className="h-4 w-4" />
              )}
              {t('workflow.inbox.bulkViewDetails')}
            </button>

            {expanded && (
              <ul className="max-h-64 overflow-auto rounded-xl border p-3 space-y-2">
                {result.items.map((item) => (
                  <li key={item.taskId} className="flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <div className="text-sm text-foreground truncate">
                        {taskLabels?.[item.taskId] ?? t('workflow.inbox.bulkResultUnknownTask')}
                      </div>
                      {item.error && (
                        <div className="text-xs text-destructive">{item.error}</div>
                      )}
                    </div>
                    <Badge
                      variant={
                        item.status === 'Succeeded'
                          ? 'default'
                          : item.status === 'Failed'
                            ? 'destructive'
                            : 'secondary'
                      }
                    >
                      {t(`workflow.inbox.bulkResult${item.status}`)}
                    </Badge>
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}

        <DialogFooter>
          <Button onClick={onClose}>{t('common.close', 'Close')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
