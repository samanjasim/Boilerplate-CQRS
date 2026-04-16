import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Spinner } from '@/components/ui/spinner';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Label } from '@/components/ui/label';
import { ConfirmDialog } from '@/components/common';
import {
  useRequiredNotifications,
  useSetRequiredNotification,
  useRemoveRequiredNotification,
  useTemplateCategories,
} from '../api';
import type { NotificationChannel } from '@/types/communication.types';

const CHANNELS: { value: NotificationChannel; label: string }[] = [
  { value: 'Email', label: 'Email' },
  { value: 'Sms', label: 'SMS' },
  { value: 'Push', label: 'Push' },
  { value: 'WhatsApp', label: 'WhatsApp' },
  { value: 'InApp', label: 'In-App' },
];

export function RequiredNotificationsManager() {
  const { t } = useTranslation();
  const { data: requiredData, isLoading } = useRequiredNotifications();
  const { data: categoriesData } = useTemplateCategories();
  const setMutation = useSetRequiredNotification();
  const removeMutation = useRemoveRequiredNotification();

  const [addOpen, setAddOpen] = useState(false);
  const [deleteId, setDeleteId] = useState<string | null>(null);
  const [category, setCategory] = useState('');
  const [channel, setChannel] = useState<NotificationChannel | ''>('');

  const required = requiredData?.data ?? [];
  const categories = categoriesData?.data ?? [];

  const handleAdd = () => {
    if (!category || !channel) return;
    setMutation.mutate(
      { category, channel: channel as NotificationChannel },
      {
        onSuccess: () => {
          setAddOpen(false);
          setCategory('');
          setChannel('');
        },
      }
    );
  };

  const handleDelete = () => {
    if (!deleteId) return;
    removeMutation.mutate(deleteId, {
      onSuccess: () => setDeleteId(null),
    });
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <Spinner className="h-6 w-6" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-lg font-semibold">{t('communication.requiredNotifications.title')}</h3>
          <p className="text-sm text-muted-foreground">{t('communication.requiredNotifications.subtitle')}</p>
        </div>
        <Button variant="outline" onClick={() => setAddOpen(true)}>
          <Plus className="me-2 h-4 w-4" />
          {t('communication.requiredNotifications.addRequired')}
        </Button>
      </div>

      {required.length === 0 ? (
        <div className="text-center py-8 text-muted-foreground">
          {t('communication.requiredNotifications.subtitle')}
        </div>
      ) : (
        <div className="space-y-2">
          {required.map((rule) => (
            <div
              key={rule.id}
              className="flex items-center justify-between rounded-xl border bg-card px-4 py-3"
            >
              <div className="flex items-center gap-3">
                <span className="font-medium capitalize">{rule.category}</span>
                <Badge variant="secondary">
                  {CHANNELS.find((c) => c.value === rule.channel)?.label ?? rule.channel}
                </Badge>
              </div>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setDeleteId(rule.id)}
              >
                <Trash2 className="h-4 w-4 text-destructive" />
              </Button>
            </div>
          ))}
        </div>
      )}

      {/* Add Required Dialog */}
      <Dialog open={addOpen} onOpenChange={setAddOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('communication.requiredNotifications.addRequired')}</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-4">
            <div className="space-y-2">
              <Label>{t('communication.requiredNotifications.selectCategory')}</Label>
              <Select value={category} onValueChange={setCategory}>
                <SelectTrigger>
                  <SelectValue placeholder={t('communication.requiredNotifications.selectCategory')} />
                </SelectTrigger>
                <SelectContent>
                  {categories.map((cat) => (
                    <SelectItem key={cat} value={cat}>
                      <span className="capitalize">{cat}</span>
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>{t('communication.requiredNotifications.selectChannel')}</Label>
              <Select value={channel} onValueChange={(v) => setChannel(v as NotificationChannel)}>
                <SelectTrigger>
                  <SelectValue placeholder={t('communication.requiredNotifications.selectChannel')} />
                </SelectTrigger>
                <SelectContent>
                  {CHANNELS.map((ch) => (
                    <SelectItem key={ch.value} value={ch.value}>
                      {ch.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAddOpen(false)}>
              {t('common.cancel')}
            </Button>
            <Button onClick={handleAdd} disabled={!category || !channel || setMutation.isPending}>
              {setMutation.isPending && <Spinner className="me-2 h-4 w-4" />}
              {t('common.save')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Delete Confirmation */}
      <ConfirmDialog
        isOpen={!!deleteId}
        onClose={() => setDeleteId(null)}
        title={t('communication.requiredNotifications.confirmRemove')}
        description=""
        onConfirm={handleDelete}
        isLoading={removeMutation.isPending}
        variant="danger"
      />
    </div>
  );
}
