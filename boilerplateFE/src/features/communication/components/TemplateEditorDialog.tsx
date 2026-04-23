import { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Eye, RotateCcw } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { Separator } from '@/components/ui/separator';
import { Spinner } from '@/components/ui/spinner';
import { ConfirmDialog } from '@/components/common';
import {
  useMessageTemplate,
  useCreateTemplateOverride,
  useUpdateTemplateOverride,
  useDeleteTemplateOverride,
  usePreviewTemplate,
} from '../api';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';

interface TemplateEditorDialogProps {
  templateId: string | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function TemplateEditorDialog({ templateId, open, onOpenChange }: TemplateEditorDialogProps) {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const canManage = hasPermission(PERMISSIONS.Communication.ManageTemplates);

  const { data, isLoading } = useMessageTemplate(templateId ?? '');
  const createOverride = useCreateTemplateOverride();
  const updateOverride = useUpdateTemplateOverride();
  const deleteOverride = useDeleteTemplateOverride();
  const previewMutation = usePreviewTemplate();

  const template = data?.data;

  const [isEditing, setIsEditing] = useState(false);
  const [subjectTemplate, setSubjectTemplate] = useState('');
  const [bodyTemplate, setBodyTemplate] = useState('');
  const [showPreview, setShowPreview] = useState(false);
  const [showResetConfirm, setShowResetConfirm] = useState(false);

  // Reset state when template changes
  useEffect(() => {
    if (template) {
      const hasOverride = !!template.override;
      // eslint-disable-next-line react-hooks/set-state-in-effect -- sync form state when selected template changes
      setIsEditing(false);
      setShowPreview(false);
      setSubjectTemplate(
        hasOverride
          ? (template.override!.subjectTemplate ?? '')
          : (template.subjectTemplate ?? '')
      );
      setBodyTemplate(
        hasOverride
          ? template.override!.bodyTemplate
          : template.bodyTemplate
      );
    }
  }, [template]);

  const handleStartEditing = () => {
    if (template) {
      // If there's an override, edit that; otherwise start from system template
      setSubjectTemplate(
        template.override
          ? (template.override.subjectTemplate ?? '')
          : (template.subjectTemplate ?? '')
      );
      setBodyTemplate(
        template.override
          ? template.override.bodyTemplate
          : template.bodyTemplate
      );
      setIsEditing(true);
    }
  };

  const handleSave = async () => {
    if (!template || !templateId) return;

    const data = {
      subjectTemplate: subjectTemplate || null,
      bodyTemplate,
    };

    if (template.override) {
      await updateOverride.mutateAsync({ id: templateId, data });
    } else {
      await createOverride.mutateAsync({ id: templateId, data });
    }

    setIsEditing(false);
  };

  const handleReset = async () => {
    if (!templateId) return;
    await deleteOverride.mutateAsync(templateId);
    setShowResetConfirm(false);
    setIsEditing(false);
  };

  const handlePreview = () => {
    if (!templateId) return;
    previewMutation.mutate({
      id: templateId,
      variables: template?.sampleVariables ?? undefined,
    });
    setShowPreview(true);
  };

  const isSaving = createOverride.isPending || updateOverride.isPending;

  return (
    <>
      <Dialog open={open} onOpenChange={onOpenChange}>
        <DialogContent className="max-w-3xl max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>
              {isEditing
                ? t('communication.templates.editor.editTitle')
                : t('communication.templates.editor.title')}
            </DialogTitle>
          </DialogHeader>

          {isLoading && (
            <div className="flex justify-center py-8">
              <Spinner size="lg" />
            </div>
          )}

          {template && (
            <div className="space-y-6">
              {/* Template info header */}
              <div className="space-y-2">
                <div className="flex items-center gap-2 flex-wrap">
                  <h3 className="text-lg font-semibold text-foreground">{template.name}</h3>
                  {template.isSystem && (
                    <Badge variant="secondary">{t('communication.templates.systemTemplate')}</Badge>
                  )}
                  {template.override && (
                    <Badge variant="default">{t('communication.templates.customized')}</Badge>
                  )}
                </div>
                {template.description && (
                  <p className="text-sm text-muted-foreground">{template.description}</p>
                )}
                <div className="flex items-center gap-4 text-sm text-muted-foreground">
                  <span>{t('communication.templates.moduleSource')}: {template.moduleSource}</span>
                  <span>{t('communication.templates.category')}: {template.category}</span>
                  <span>{t('communication.templates.defaultChannel')}: {template.defaultChannel}</span>
                </div>
                {template.availableChannels.length > 0 && (
                  <div className="flex items-center gap-1.5">
                    <span className="text-sm text-muted-foreground">{t('communication.templates.availableChannels')}:</span>
                    {template.availableChannels.map((ch) => (
                      <Badge key={ch} variant="secondary" className="text-xs">{ch}</Badge>
                    ))}
                  </div>
                )}
              </div>

              <Separator />

              {/* Variable reference panel */}
              {template.variableSchema && Object.keys(template.variableSchema).length > 0 && (
                <div className="rounded-xl border border-border bg-muted/30 p-4 space-y-2">
                  <h4 className="text-sm font-semibold text-foreground">{t('communication.templates.variableReference')}</h4>
                  <div className="grid grid-cols-2 gap-x-4 gap-y-1">
                    {Object.entries(template.variableSchema).map(([key, type]) => (
                      <div key={key} className="flex items-center gap-2 text-sm">
                        <code className="rounded bg-muted px-1.5 py-0.5 text-xs font-mono">{`{{${key}}}`}</code>
                        <span className="text-muted-foreground">{type}</span>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* System default (read-only) */}
              {!isEditing && (
                <div className="space-y-4">
                  <h4 className="text-sm font-semibold text-foreground">
                    {template.override
                      ? t('communication.templates.editor.yourOverride')
                      : t('communication.templates.editor.systemDefault')}
                  </h4>

                  {(template.override?.subjectTemplate ?? template.subjectTemplate) && (
                    <div className="space-y-1">
                      <Label className="text-muted-foreground">{t('communication.templates.editor.subjectTemplate')}</Label>
                      <div className="rounded-xl border border-border bg-muted/30 p-3 text-sm font-mono whitespace-pre-wrap">
                        {template.override?.subjectTemplate ?? template.subjectTemplate}
                      </div>
                    </div>
                  )}

                  <div className="space-y-1">
                    <Label className="text-muted-foreground">{t('communication.templates.editor.bodyTemplate')}</Label>
                    <div className="rounded-xl border border-border bg-muted/30 p-3 text-sm font-mono whitespace-pre-wrap max-h-64 overflow-y-auto">
                      {template.override?.bodyTemplate ?? template.bodyTemplate}
                    </div>
                  </div>

                  {/* Action buttons */}
                  <div className="flex items-center gap-2 pt-2">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={handlePreview}
                      disabled={previewMutation.isPending}
                    >
                      <Eye className="mr-2 h-4 w-4" />
                      {t('communication.templates.preview')}
                    </Button>

                    {canManage && (
                      <Button size="sm" onClick={handleStartEditing}>
                        {t('communication.templates.customize')}
                      </Button>
                    )}

                    {canManage && template.override && (
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setShowResetConfirm(true)}
                      >
                        <RotateCcw className="mr-2 h-4 w-4" />
                        {t('communication.templates.resetToDefault')}
                      </Button>
                    )}
                  </div>
                </div>
              )}

              {/* Editing mode */}
              {isEditing && (
                <div className="space-y-4">
                  <h4 className="text-sm font-semibold text-foreground">
                    {t('communication.templates.editor.yourOverride')}
                  </h4>

                  <div className="space-y-1.5">
                    <Label htmlFor="override-subject">{t('communication.templates.editor.subjectTemplate')}</Label>
                    <Input
                      id="override-subject"
                      value={subjectTemplate}
                      onChange={(e) => setSubjectTemplate(e.target.value)}
                      placeholder={t('communication.templates.editor.subjectPlaceholder')}
                    />
                  </div>

                  <div className="space-y-1.5">
                    <Label htmlFor="override-body">{t('communication.templates.editor.bodyTemplate')}</Label>
                    <Textarea
                      id="override-body"
                      value={bodyTemplate}
                      onChange={(e) => setBodyTemplate(e.target.value)}
                      placeholder={t('communication.templates.editor.bodyPlaceholder')}
                      rows={10}
                      className="font-mono text-sm"
                    />
                  </div>

                  {/* System default reference (collapsed) */}
                  {template.override && (
                    <div className="rounded-xl border border-border bg-muted/30 p-3 space-y-2">
                      <h5 className="text-xs font-semibold text-muted-foreground">
                        {t('communication.templates.editor.systemDefault')}
                      </h5>
                      {template.subjectTemplate && (
                        <p className="text-xs text-muted-foreground font-mono">{template.subjectTemplate}</p>
                      )}
                      <p className="text-xs text-muted-foreground font-mono line-clamp-3">{template.bodyTemplate}</p>
                    </div>
                  )}

                  <div className="flex items-center gap-2 pt-2">
                    <Button
                      onClick={handleSave}
                      disabled={isSaving || !bodyTemplate.trim()}
                    >
                      {isSaving ? t('common.saving') : t('common.save')}
                    </Button>
                    <Button
                      variant="outline"
                      onClick={() => setIsEditing(false)}
                    >
                      {t('common.cancel')}
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={handlePreview}
                      disabled={previewMutation.isPending}
                    >
                      <Eye className="mr-2 h-4 w-4" />
                      {t('communication.templates.preview')}
                    </Button>
                  </div>
                </div>
              )}

              {/* Preview output */}
              {showPreview && previewMutation.data && (
                <>
                  <Separator />
                  <div className="space-y-3">
                    <h4 className="text-sm font-semibold text-foreground">{t('communication.templates.previewTitle')}</h4>
                    {previewMutation.data.data.renderedSubject && (
                      <div className="space-y-1">
                        <Label className="text-muted-foreground">{t('communication.templates.editor.subjectTemplate')}</Label>
                        <div className="rounded-xl border border-border p-3 text-sm">
                          {previewMutation.data.data.renderedSubject}
                        </div>
                      </div>
                    )}
                    <div className="space-y-1">
                      <Label className="text-muted-foreground">{t('communication.templates.editor.bodyTemplate')}</Label>
                      <div
                        className="rounded-xl border border-border p-3 text-sm max-h-64 overflow-y-auto"
                        dangerouslySetInnerHTML={{ __html: previewMutation.data.data.renderedBody }}
                      />
                    </div>
                  </div>
                </>
              )}
            </div>
          )}
        </DialogContent>
      </Dialog>

      <ConfirmDialog
        isOpen={showResetConfirm}
        onClose={() => setShowResetConfirm(false)}
        title={t('communication.templates.resetToDefault')}
        description={t('communication.templates.resetConfirm')}
        confirmLabel={t('communication.templates.resetToDefault')}
        onConfirm={handleReset}
        isLoading={deleteOverride.isPending}
        variant="danger"
      />
    </>
  );
}
