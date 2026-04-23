import { useTranslation } from 'react-i18next';
import { CheckCheck, X, RotateCcw, XCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';

interface BulkActionBarProps {
  selectedCount: number;
  isPending: boolean;
  onApprove: () => void;
  onReject: () => void;
  onReturn: () => void;
  onClear: () => void;
}

export function BulkActionBar({
  selectedCount,
  isPending,
  onApprove,
  onReject,
  onReturn,
  onClear,
}: BulkActionBarProps) {
  const { t } = useTranslation();

  return (
    <div className="fixed inset-x-0 bottom-6 z-40 flex justify-center px-4 pointer-events-none">
      <div className="pointer-events-auto flex items-center gap-2 rounded-2xl border bg-card shadow-card px-4 py-3">
        <span className="text-sm font-medium text-foreground ltr:mr-2 rtl:ml-2">
          {t('workflow.inbox.selected', { count: selectedCount })}
        </span>
        <Button size="sm" onClick={onApprove} disabled={isPending}>
          <CheckCheck className="h-4 w-4 ltr:mr-1 rtl:ml-1" />
          {t('workflow.inbox.bulkApprove')}
        </Button>
        <Button size="sm" variant="outline" onClick={onReject} disabled={isPending}>
          <XCircle className="h-4 w-4 ltr:mr-1 rtl:ml-1" />
          {t('workflow.inbox.bulkReject')}
        </Button>
        <Button size="sm" variant="outline" onClick={onReturn} disabled={isPending}>
          <RotateCcw className="h-4 w-4 ltr:mr-1 rtl:ml-1" />
          {t('workflow.inbox.bulkReturn')}
        </Button>
        <Button size="sm" variant="ghost" onClick={onClear} disabled={isPending}>
          <X className="h-4 w-4 ltr:mr-1 rtl:ml-1" />
          {t('workflow.inbox.clearSelection')}
        </Button>
      </div>
    </div>
  );
}
