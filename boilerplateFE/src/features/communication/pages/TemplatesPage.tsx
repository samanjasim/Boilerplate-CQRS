import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { FileText, Mail, Smartphone, Bell, MessageCircle, Inbox } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader, EmptyState } from '@/components/common';
import { useMessageTemplates, useTemplateCategories } from '../api';
import { TemplateEditorDialog } from '../components/TemplateEditorDialog';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import type { MessageTemplateDto, NotificationChannel } from '@/types/communication.types';

const CHANNEL_ICONS: Record<NotificationChannel, typeof Mail> = {
  Email: Mail,
  Sms: Smartphone,
  Push: Bell,
  WhatsApp: MessageCircle,
  InApp: Inbox,
};

export default function TemplatesPage() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();

  const [selectedCategory, setSelectedCategory] = useState<string | undefined>(undefined);
  const [selectedTemplateId, setSelectedTemplateId] = useState<string | null>(null);

  const { data: categoriesData } = useTemplateCategories();
  const { data, isLoading, isError } = useMessageTemplates(selectedCategory);

  const templates: MessageTemplateDto[] = data?.data ?? [];
  const categories: string[] = categoriesData?.data ?? [];

  const canManageTemplates = hasPermission(PERMISSIONS.Communication.ManageTemplates);

  // Group templates by category
  const grouped = templates.reduce<Record<string, MessageTemplateDto[]>>((acc, tpl) => {
    if (!acc[tpl.category]) acc[tpl.category] = [];
    acc[tpl.category].push(tpl);
    return acc;
  }, {});

  const sortedCategories = Object.keys(grouped).sort();

  if (isError) {
    return (
      <div className="space-y-6">
        <PageHeader title={t('communication.templates.title')} />
        <EmptyState
          icon={FileText}
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
        title={t('communication.templates.title')}
        subtitle={t('communication.templates.subtitle')}
      />

      {/* Category filter tabs */}
      {categories.length > 0 && (
        <div className="flex flex-wrap gap-2">
          <Button
            variant={selectedCategory === undefined ? 'default' : 'outline'}
            size="sm"
            onClick={() => setSelectedCategory(undefined)}
          >
            {t('communication.templates.allCategories')}
          </Button>
          {categories.map((cat) => (
            <Button
              key={cat}
              variant={selectedCategory === cat ? 'default' : 'outline'}
              size="sm"
              onClick={() => setSelectedCategory(cat)}
            >
              {cat}
            </Button>
          ))}
        </div>
      )}

      {templates.length === 0 ? (
        <EmptyState
          icon={FileText}
          title={t('communication.templates.noTemplates')}
          description={t('communication.templates.noTemplatesDescription')}
        />
      ) : (
        <div className="space-y-8">
          {sortedCategories.map((category) => {
            const categoryTemplates = grouped[category];
            if (!categoryTemplates || categoryTemplates.length === 0) return null;

            return (
              <div key={category} className="space-y-3">
                <div className="flex items-center gap-2">
                  <h3 className="text-lg font-semibold text-foreground">{category}</h3>
                  <Badge variant="secondary" className="text-xs">{categoryTemplates.length}</Badge>
                </div>

                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>{t('common.name', 'Name')}</TableHead>
                      <TableHead className="hidden md:table-cell">{t('communication.templates.moduleSource')}</TableHead>
                      <TableHead className="hidden lg:table-cell">{t('communication.templates.defaultChannel')}</TableHead>
                      <TableHead>{t('common.status')}</TableHead>
                      <TableHead className="text-end">{t('common.actions')}</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {categoryTemplates.map((tpl) => {
                      const ChannelIcon = CHANNEL_ICONS[tpl.defaultChannel];
                      return (
                        <TableRow key={tpl.id}>
                          <TableCell>
                            <div className="space-y-1">
                              <p className="font-medium text-foreground">{tpl.name}</p>
                              {tpl.description && (
                                <p className="text-sm text-muted-foreground line-clamp-1">{tpl.description}</p>
                              )}
                            </div>
                          </TableCell>
                          <TableCell className="hidden md:table-cell">
                            <span className="text-sm text-muted-foreground">{tpl.moduleSource}</span>
                          </TableCell>
                          <TableCell className="hidden lg:table-cell">
                            <div className="flex items-center gap-1.5">
                              {ChannelIcon && <ChannelIcon className="h-4 w-4 text-muted-foreground" />}
                              <span className="text-sm">{tpl.defaultChannel}</span>
                            </div>
                          </TableCell>
                          <TableCell>
                            <div className="flex items-center gap-1.5">
                              {tpl.isSystem && (
                                <Badge variant="secondary">{t('communication.templates.systemTemplate')}</Badge>
                              )}
                              {tpl.hasOverride && (
                                <Badge variant="default">{t('communication.templates.customized')}</Badge>
                              )}
                            </div>
                          </TableCell>
                          <TableCell className="text-end">
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => setSelectedTemplateId(tpl.id)}
                            >
                              {canManageTemplates
                                ? t('common.edit')
                                : t('common.view', 'View')}
                            </Button>
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </div>
            );
          })}
        </div>
      )}

      {/* Template Editor Dialog */}
      <TemplateEditorDialog
        templateId={selectedTemplateId}
        open={!!selectedTemplateId}
        onOpenChange={(open) => {
          if (!open) setSelectedTemplateId(null);
        }}
      />
    </div>
  );
}
