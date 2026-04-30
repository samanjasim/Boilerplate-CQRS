import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router-dom';
import { FileText, Mail, Smartphone, Bell, MessageCircle, Inbox } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader, EmptyState } from '@/components/common';
import { useMessageTemplates } from '../api';
import { TemplateEditorDialog } from '../components/TemplateEditorDialog';
import { TemplateCategoryRail } from '../components/TemplateCategoryRail';
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
  const [searchParams, setSearchParams] = useSearchParams();

  const [selectedTemplateId, setSelectedTemplateId] = useState<string | null>(null);

  // Fetch the full unfiltered list — categories derived client-side.
  const { data, isLoading, isError } = useMessageTemplates();
  const templates: MessageTemplateDto[] = data?.data ?? [];

  const canManageTemplates = hasPermission(PERMISSIONS.Communication.ManageTemplates);

  // Group templates by category. Categories sorted alphabetically; templates within each
  // category sorted by name.
  const grouped = useMemo(() => {
    const buckets = new Map<string, MessageTemplateDto[]>();
    for (const tpl of templates) {
      const list = buckets.get(tpl.category) ?? [];
      list.push(tpl);
      buckets.set(tpl.category, list);
    }
    for (const list of buckets.values()) {
      list.sort((a, b) => a.name.localeCompare(b.name));
    }
    return Array.from(buckets.entries())
      .map(([name, list]) => ({ name, list }))
      .sort((a, b) => a.name.localeCompare(b.name));
  }, [templates]);

  const totalCount = templates.length;
  const categoryDescriptors = grouped.map(({ name, list }) => ({ name, count: list.length }));

  const urlCategory = searchParams.get('category') ?? undefined;
  const knownCategoryNames = new Set(grouped.map((g) => g.name));
  const selectedCategory =
    urlCategory && knownCategoryNames.has(urlCategory) ? urlCategory : undefined;

  const handleSelect = (category: string | undefined) => {
    const next = new URLSearchParams(searchParams);
    if (category === undefined) {
      next.delete('category');
    } else {
      next.set('category', category);
    }
    setSearchParams(next, { replace: true });
  };

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

  const visibleGroups =
    selectedCategory === undefined
      ? grouped
      : grouped.filter((g) => g.name === selectedCategory);

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('communication.templates.title')}
        subtitle={t('communication.templates.subtitle')}
      />

      {totalCount === 0 ? (
        <EmptyState
          icon={FileText}
          title={t('communication.templates.noTemplates')}
          description={t('communication.templates.noTemplatesDescription')}
        />
      ) : (
        <div className="grid gap-6 lg:grid-cols-[200px_minmax(0,1fr)]">
          {/* lg+ rail */}
          <div className="hidden lg:block">
            <TemplateCategoryRail
              categories={categoryDescriptors}
              selectedCategory={selectedCategory}
              onSelect={handleSelect}
              totalCount={totalCount}
              variant="rail"
            />
          </div>

          {/* <lg chip row */}
          <div className="lg:hidden lg:col-span-2">
            <TemplateCategoryRail
              categories={categoryDescriptors}
              selectedCategory={selectedCategory}
              onSelect={handleSelect}
              totalCount={totalCount}
              variant="chips"
            />
          </div>

          {/* Main column */}
          <div className="space-y-8 min-w-0">
            {visibleGroups.map(({ name, list }) => (
              <section key={name} className="space-y-3">
                <div className="flex items-center gap-2">
                  <h3 className="text-lg font-semibold text-foreground">{name}</h3>
                  <Badge variant="secondary" className="text-xs">{list.length}</Badge>
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
                    {list.map((tpl) => {
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
              </section>
            ))}
          </div>
        </div>
      )}

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
