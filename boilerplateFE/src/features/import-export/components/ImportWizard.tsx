import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AlertCircle, AlertTriangle, Download } from 'lucide-react';
import { useAuthStore, selectUser } from '@/stores';
import { useTenants } from '@/features/tenants/api';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Spinner } from '@/components/ui/spinner';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { FileUpload } from '@/components/common';
import { ImportProgressCard } from './ImportProgressCard';
import {
  useEntityTypes,
  usePreviewImport,
  useStartImport,
  useDownloadTemplate,
  useImportJob,
} from '../api';
import type { ImportJob, Tenant } from '@/types';

type Step = 'upload' | 'preview' | 'progress';

interface ImportWizardProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

// Conflict mode enum values matching the backend
const CONFLICT_SKIP = 0;
const CONFLICT_UPDATE = 1;

export function ImportWizard({ open, onOpenChange }: ImportWizardProps) {
  const { t } = useTranslation();

  // Step state
  const [step, setStep] = useState<Step>('upload');

  // Upload step state
  const [entityType, setEntityType] = useState('');
  const [fileId, setFileId] = useState<string | null>(null);
  const [conflictMode, setConflictMode] = useState<number>(CONFLICT_SKIP);

  // Tenant targeting (platform admin only)
  const user = useAuthStore(selectUser);
  const isPlatformAdmin = !user?.tenantId;
  const [targetTenantId, setTargetTenantId] = useState<string>('');

  // Progress step state
  const [jobId, setJobId] = useState<string | null>(null);
  const [completedJob, setCompletedJob] = useState<ImportJob | null>(null);

  // Queries & mutations
  const { data: entityTypesData, isLoading: loadingTypes } = useEntityTypes();
  const previewMutation = usePreviewImport();
  const startMutation = useStartImport();
  const downloadTemplate = useDownloadTemplate();
  const { data: tenantsData } = useTenants(
    isPlatformAdmin ? { pageSize: 100 } : undefined
  );
  const tenants: Tenant[] = isPlatformAdmin ? (tenantsData?.data ?? []) : [];

  // Poll the job if we're on the progress step
  const { data: polledJob } = useImportJob(jobId ?? '');

  const entityTypes = (entityTypesData ?? []).filter((e) => e.supportsImport);
  const selectedEntity = entityTypes.find((e) => e.entityType === entityType);
  const currentJob = polledJob ?? completedJob;
  const isJobDone =
    currentJob &&
    ['Completed', 'PartialSuccess', 'Failed'].includes(currentJob.status);

  const handleClose = (isOpen: boolean) => {
    if (!isOpen) {
      // Reset all state on close
      setStep('upload');
      setEntityType('');
      setFileId(null);
      setConflictMode(CONFLICT_SKIP);
      setTargetTenantId('');
      setJobId(null);
      setCompletedJob(null);
      previewMutation.reset();
    }
    onOpenChange(isOpen);
  };

  const handleNext = async () => {
    if (!fileId || !entityType) return;
    previewMutation.reset();
    previewMutation.mutate(
      { fileId, entityType },
      {
        onSuccess: () => setStep('preview'),
      }
    );
  };

  const handleStartImport = () => {
    if (!fileId || !entityType) return;
    startMutation.mutate(
      { fileId, entityType, conflictMode, targetTenantId: targetTenantId && targetTenantId !== '__none__' ? targetTenantId : undefined },
      {
        onSuccess: (job) => {
          setJobId(job.id);
          setCompletedJob(job);
          setStep('progress');
        },
      }
    );
  };

  const handleDownloadTemplate = () => {
    if (!entityType) return;
    downloadTemplate.mutate(entityType);
  };

  const preview = previewMutation.data;
  const hasValidationErrors =
    preview && preview.validationErrors && preview.validationErrors.length > 0;
  const hasUnrecognizedColumns =
    preview && preview.unrecognizedColumns && preview.unrecognizedColumns.length > 0;

  const stepLabels: Record<Step, string> = {
    upload: t('importExport.step1Upload'),
    preview: t('importExport.step2Preview'),
    progress: t('importExport.step4Progress'),
  };

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className="sm:max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{t('importExport.startImport')}</DialogTitle>
          <DialogDescription>{stepLabels[step]}</DialogDescription>
        </DialogHeader>

        {/* Step indicator */}
        <div className="flex items-center gap-2 text-sm">
          {(['upload', 'preview', 'progress'] as Step[]).map((s, idx) => (
            <div key={s} className="flex items-center gap-2">
              <div
                className={`flex h-6 w-6 items-center justify-center rounded-full text-xs font-medium ${
                  step === s
                    ? 'bg-primary text-primary-foreground'
                    : idx < (['upload', 'preview', 'progress'] as Step[]).indexOf(step)
                    ? 'bg-green-500 text-white'
                    : 'bg-muted text-muted-foreground'
                }`}
              >
                {idx + 1}
              </div>
              <span
                className={
                  step === s ? 'text-foreground font-medium' : 'text-muted-foreground'
                }
              >
                {stepLabels[s]}
              </span>
              {idx < 2 && <span className="text-muted-foreground">→</span>}
            </div>
          ))}
        </div>

        {/* ─── Step 1: Upload ─────────────────────────────────────────────── */}
        {step === 'upload' && (
          <div className="space-y-4">
            {/* Entity type selector */}
            <div className="space-y-2">
              <Label>{t('importExport.selectEntityType')}</Label>
              {loadingTypes ? (
                <div className="flex items-center gap-2 text-sm text-muted-foreground">
                  <Spinner size="sm" />
                  <span>{t('common.loading', 'Loading...')}</span>
                </div>
              ) : (
                <Select value={entityType} onValueChange={(v) => { setEntityType(v); setTargetTenantId(''); }}>
                  <SelectTrigger>
                    <SelectValue placeholder={t('importExport.selectEntityType')} />
                  </SelectTrigger>
                  <SelectContent>
                    {entityTypes.map((et) => (
                      <SelectItem key={et.entityType} value={et.entityType}>
                        {et.displayName}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            </div>

            {/* Download template */}
            {entityType && (
              <div className="flex items-center justify-between rounded-lg border bg-muted/30 p-3">
                <p className="text-sm text-muted-foreground">
                  {t('importExport.templateDescription')}
                </p>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={handleDownloadTemplate}
                  disabled={downloadTemplate.isPending}
                  className="shrink-0 ltr:ml-3 rtl:mr-3"
                >
                  {downloadTemplate.isPending ? (
                    <Spinner size="sm" className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
                  ) : (
                    <Download className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
                  )}
                  {t('importExport.downloadTemplate')}
                </Button>
              </div>
            )}

            {/* Target tenant selector (platform admin only) */}
            {isPlatformAdmin && entityType && (
              <div className="space-y-2">
                <Label>
                  {selectedEntity?.requiresTenant
                    ? t('importExport.targetTenantRequired')
                    : t('importExport.targetTenantOptional')}
                </Label>
                <Select
                  value={targetTenantId || '__none__'}
                  onValueChange={(v) => setTargetTenantId(v === '__none__' ? '' : v)}
                >
                  <SelectTrigger>
                    <SelectValue placeholder={t('importExport.selectTenant')} />
                  </SelectTrigger>
                  <SelectContent>
                    {!selectedEntity?.requiresTenant && (
                      <SelectItem value="__none__">
                        {t('importExport.platformWide')}
                      </SelectItem>
                    )}
                    {tenants.map((tenant) => (
                      <SelectItem key={tenant.id} value={tenant.id}>
                        {tenant.name} ({tenant.slug})
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <p className="text-xs text-muted-foreground">
                  {selectedEntity?.requiresTenant
                    ? t('importExport.tenantRequiredHint')
                    : t('importExport.tenantOptionalHint')}
                </p>
              </div>
            )}

            {/* File upload */}
            <div className="space-y-2">
              <Label>{t('importExport.uploadFile')}</Label>
              <FileUpload
                mode="temp"
                accept=".csv"
                onUploaded={(id) => setFileId(id)}
                onFileSelected={() => setFileId(null)}
              />
            </div>

            {/* Conflict mode */}
            <div className="space-y-2">
              <Label>{t('importExport.conflictMode')}</Label>
              <div className="flex flex-col gap-2">
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="radio"
                    name="conflictMode"
                    checked={conflictMode === CONFLICT_SKIP}
                    onChange={() => setConflictMode(CONFLICT_SKIP)}
                    className="accent-primary"
                  />
                  <span className="text-sm">{t('importExport.skipDuplicates')}</span>
                </label>
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="radio"
                    name="conflictMode"
                    checked={conflictMode === CONFLICT_UPDATE}
                    onChange={() => setConflictMode(CONFLICT_UPDATE)}
                    className="accent-primary"
                  />
                  <span className="text-sm">{t('importExport.updateExisting')}</span>
                </label>
              </div>
            </div>

            {/* Actions */}
            <div className="flex justify-end gap-2 pt-2">
              <Button variant="outline" onClick={() => handleClose(false)}>
                {t('common.cancel')}
              </Button>
              <Button
                onClick={handleNext}
                disabled={!fileId || !entityType || previewMutation.isPending || (isPlatformAdmin && !!selectedEntity?.requiresTenant && !targetTenantId)}
              >
                {previewMutation.isPending ? (
                  <>
                    <Spinner size="sm" className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
                    {t('importExport.preview')}...
                  </>
                ) : (
                  t('common.next', 'Next')
                )}
              </Button>
            </div>
          </div>
        )}

        {/* ─── Step 2: Preview ────────────────────────────────────────────── */}
        {step === 'preview' && (
          <div className="space-y-4">
            {previewMutation.isPending ? (
              <div className="flex flex-col items-center gap-2 py-8">
                <Spinner size="lg" />
                <p className="text-sm text-muted-foreground">{t('importExport.preview')}...</p>
              </div>
            ) : previewMutation.isError ? (
              <div className="text-center py-8 space-y-3">
                <p className="text-destructive">{t('common.errorOccurred')}</p>
                <Button variant="outline" onClick={() => setStep('upload')}>
                  {t('common.goBack', 'Go Back')}
                </Button>
              </div>
            ) : preview ? (
              <>
                {/* Row count */}
                <p className="text-sm text-muted-foreground">
                  {t('importExport.rowsFound', { count: preview.totalRowCount })}
                </p>

                {/* Validation errors */}
                {hasValidationErrors && (
                  <div className="flex gap-2 rounded-lg border border-destructive/50 bg-destructive/10 p-3">
                    <AlertCircle className="h-4 w-4 text-destructive shrink-0 mt-0.5" />
                    <div className="space-y-1">
                      <p className="text-sm font-medium text-destructive">
                        {t('importExport.validationErrors')}
                      </p>
                      <ul className="list-disc list-inside space-y-0.5">
                        {preview.validationErrors.map((err, i) => (
                          <li key={i} className="text-xs text-destructive">
                            {err}
                          </li>
                        ))}
                      </ul>
                    </div>
                  </div>
                )}

                {/* Unrecognized columns */}
                {hasUnrecognizedColumns && (
                  <div className="flex gap-2 rounded-lg border border-amber-500/50 bg-amber-50 dark:bg-amber-950/20 p-3">
                    <AlertTriangle className="h-4 w-4 text-amber-600 shrink-0 mt-0.5" />
                    <div className="space-y-1">
                      <p className="text-sm font-medium text-amber-700 dark:text-amber-400">
                        {t('importExport.unrecognizedColumns')}
                      </p>
                      <div className="flex flex-wrap gap-1">
                        {preview.unrecognizedColumns.map((col) => (
                          <Badge
                            key={col}
                            variant="outline"
                            className="text-xs border-amber-400 text-amber-700 dark:text-amber-400"
                          >
                            {col}
                          </Badge>
                        ))}
                      </div>
                    </div>
                  </div>
                )}

                {/* Preview table */}
                {preview.previewRows.length > 0 && (
                  <div className="overflow-auto rounded-xl border">
                    <Table>
                      <TableHeader>
                        <TableRow>
                          {preview.headers.map((h) => (
                            <TableHead
                              key={h}
                              className="bg-muted/50 text-xs font-semibold"
                            >
                              {h}
                            </TableHead>
                          ))}
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {preview.previewRows.slice(0, 5).map((row, rowIdx) => (
                          <TableRow key={rowIdx}>
                            {row.map((cell, cellIdx) => (
                              <TableCell key={cellIdx} className="text-xs">
                                {cell}
                              </TableCell>
                            ))}
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </div>
                )}
              </>
            ) : null}

            {/* Actions */}
            <div className="flex justify-between gap-2 pt-2">
              <Button variant="outline" onClick={() => setStep('upload')}>
                {t('common.back', 'Back')}
              </Button>
              <Button
                onClick={handleStartImport}
                disabled={startMutation.isPending || (hasValidationErrors ?? false)}
              >
                {startMutation.isPending ? (
                  <>
                    <Spinner size="sm" className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
                    {t('importExport.confirm')}...
                  </>
                ) : (
                  t('importExport.confirm')
                )}
              </Button>
            </div>
          </div>
        )}

        {/* ─── Step 3: Progress ───────────────────────────────────────────── */}
        {step === 'progress' && currentJob && (
          <div className="space-y-4">
            <ImportProgressCard job={currentJob} />

            {isJobDone && (
              <div className="flex justify-end">
                <Button onClick={() => handleClose(false)}>
                  {t('common.close', 'Close')}
                </Button>
              </div>
            )}
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
