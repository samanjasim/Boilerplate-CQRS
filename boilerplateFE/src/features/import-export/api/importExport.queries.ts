import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { queryKeys } from '@/lib/query/keys';
import { importExportApi } from './importExport.api';
import type { StartImportData, PreviewImportData, ImportJob } from '@/types';
import { toast } from 'sonner';
import i18n from '@/i18n';

// ── Queries ────────────────────────────────────────────────────────────────

export function useEntityTypes() {
  return useQuery({
    queryKey: queryKeys.importExport.types(),
    queryFn: () => importExportApi.getEntityTypes(),
  });
}

export function useImportJobs(params?: Record<string, unknown>) {
  return useQuery({
    queryKey: queryKeys.importExport.imports.list(params),
    queryFn: () => importExportApi.getImportJobs(params),
    refetchInterval: (query) => {
      const jobs = query.state.data?.data ?? [];
      const hasActive = jobs.some((j: ImportJob) =>
        ['Pending', 'Validating', 'Processing'].includes(j.status)
      );
      return hasActive ? 5000 : false;
    },
  });
}

export function useImportJob(id: string) {
  return useQuery({
    queryKey: queryKeys.importExport.imports.detail(id),
    queryFn: () => importExportApi.getImportJobById(id),
    enabled: !!id,
  });
}

// ── Mutations ──────────────────────────────────────────────────────────────

export function useStartImport() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: StartImportData) => importExportApi.startImport(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.importExport.imports.all });
      toast.success(i18n.t('importExport.importCompleted'));
    },
    // onError is handled by the global axios error interceptor (error.interceptor.ts)
  });
}

export function useDeleteImportJob() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => importExportApi.deleteImportJob(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.importExport.imports.all });
    },
    // onError is handled by the global axios error interceptor (error.interceptor.ts)
  });
}

export function usePreviewImport() {
  return useMutation({
    mutationFn: (data: PreviewImportData) => importExportApi.previewImport(data),
    // onError is handled by the global axios error interceptor (error.interceptor.ts)
  });
}

export function useDownloadTemplate() {
  return useMutation({
    mutationFn: (entityType: string) => importExportApi.downloadTemplate(entityType),
    // onError is handled by the global axios error interceptor (error.interceptor.ts)
  });
}
