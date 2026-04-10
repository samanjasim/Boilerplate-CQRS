import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Upload } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ImportWizard } from './ImportWizard';

/**
 * Slot entry for the `users-list-toolbar` extension point.
 *
 * Renders an "Import" button that opens the ImportWizard preconfigured for
 * the Users entity type. Manages its own modal state so the host page does
 * not need to know about it.
 *
 * When the wizard modal closes, <c>onRefresh</c> fires so the host users
 * list refetches — this covers both "import finished, close" and "cancel".
 * A cancel still triggers a refetch, which is cheap and idempotent.
 */
export function UsersImportButton({ onRefresh }: { onRefresh: () => void }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);

  const handleOpenChange = (isOpen: boolean) => {
    setOpen(isOpen);
    if (!isOpen) onRefresh();
  };

  return (
    <>
      <Button variant="outline" onClick={() => setOpen(true)}>
        <Upload className="mr-2 h-4 w-4" />
        {t('users.import')}
      </Button>
      <ImportWizard open={open} onOpenChange={handleOpenChange} entityType="Users" />
    </>
  );
}

export default UsersImportButton;
