import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Zap, Plus, Pencil, Trash2, Power } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader, EmptyState, ConfirmDialog } from '@/components/common';
import {
  useTriggerRules,
  useDeleteTriggerRule,
  useToggleTriggerRule,
} from '../api';
import { TriggerRuleFormDialog } from '../components/TriggerRuleFormDialog';
import { usePermissions } from '@/hooks';
import { PERMISSIONS, STATUS_BADGE_VARIANT } from '@/constants';
import type { TriggerRuleDto } from '@/types/communication.types';

export default function TriggerRulesPage() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();

  const [formOpen, setFormOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<TriggerRuleDto | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<TriggerRuleDto | null>(null);

  const { data, isLoading, isError } = useTriggerRules();
  const deleteMutation = useDeleteTriggerRule();
  const toggleMutation = useToggleTriggerRule();

  const rules: TriggerRuleDto[] = data?.data ?? [];
  const canManage = hasPermission(PERMISSIONS.Communication.ManageTriggerRules);

  const handleDelete = async () => {
    if (!deleteTarget) return;
    await deleteMutation.mutateAsync(deleteTarget.id);
    setDeleteTarget(null);
  };

  const handleEdit = (rule: TriggerRuleDto) => {
    setEditTarget(rule);
    setFormOpen(true);
  };

  if (isError) {
    return (
      <div className="space-y-6">
        <PageHeader title={t('communication.triggerRules.title')} />
        <EmptyState
          icon={Zap}
          title={t('common.errorOccurred')}
          description={t('common.tryAgain')}
        />
      </div>
    );
  }

  if (isLoading && !data) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('communication.triggerRules.title')}
        subtitle={t('communication.triggerRules.subtitle')}
        actions={
          canManage ? (
            <Button onClick={() => { setEditTarget(null); setFormOpen(true); }}>
              <Plus className="mr-2 h-4 w-4" />
              {t('communication.triggerRules.addRule')}
            </Button>
          ) : undefined
        }
      />

      {rules.length === 0 ? (
        <EmptyState
          icon={Zap}
          title={t('communication.triggerRules.noRules')}
          description={t('communication.triggerRules.noRulesDescription')}
          action={
            canManage
              ? { label: t('communication.triggerRules.addRule'), onClick: () => { setEditTarget(null); setFormOpen(true); } }
              : undefined
          }
        />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('communication.triggerRules.fields.name')}</TableHead>
              <TableHead>{t('communication.triggerRules.fields.event')}</TableHead>
              <TableHead>{t('communication.triggerRules.fields.template')}</TableHead>
              <TableHead>{t('communication.triggerRules.fields.channelSequence')}</TableHead>
              <TableHead>{t('communication.triggerRules.fields.status')}</TableHead>
              {canManage && <TableHead className="text-end">{t('common.actions')}</TableHead>}
            </TableRow>
          </TableHeader>
          <TableBody>
            {rules.map((rule) => (
              <TableRow key={rule.id}>
                <TableCell className="font-medium">{rule.name}</TableCell>
                <TableCell className="text-muted-foreground">{rule.eventName}</TableCell>
                <TableCell className="text-muted-foreground">{rule.messageTemplateName ?? '—'}</TableCell>
                <TableCell>
                  <div className="flex flex-wrap gap-1">
                    {rule.channelSequence.map((ch, idx) => (
                      <Badge key={ch} variant="secondary" className="text-xs">
                        {idx + 1}. {ch}
                      </Badge>
                    ))}
                  </div>
                </TableCell>
                <TableCell>
                  <Badge variant={STATUS_BADGE_VARIANT[rule.status] ?? 'secondary'}>
                    {rule.status}
                  </Badge>
                </TableCell>
                {canManage && (
                  <TableCell>
                    <div className="flex justify-end gap-1">
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => handleEdit(rule)}
                        title={t('common.edit')}
                      >
                        <Pencil className="h-4 w-4" />
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => toggleMutation.mutate(rule.id)}
                        disabled={toggleMutation.isPending}
                        title={rule.status === 'Active' ? t('common.deactivate') : t('common.activate')}
                      >
                        <Power className="h-4 w-4" />
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => setDeleteTarget(rule)}
                        title={t('common.delete')}
                      >
                        <Trash2 className="h-4 w-4 text-destructive" />
                      </Button>
                    </div>
                  </TableCell>
                )}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      {/* Dialogs */}
      <TriggerRuleFormDialog
        open={formOpen}
        onOpenChange={(open) => {
          setFormOpen(open);
          if (!open) setEditTarget(null);
        }}
        editRule={editTarget}
      />

      <ConfirmDialog
        isOpen={!!deleteTarget}
        onClose={() => setDeleteTarget(null)}
        title={t('common.delete')}
        description={t('communication.triggerRules.confirmDelete')}
        confirmLabel={t('common.delete')}
        onConfirm={handleDelete}
        isLoading={deleteMutation.isPending}
        variant="danger"
      />
    </div>
  );
}
