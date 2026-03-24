import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { reportsApi } from './reports.api';
import { queryKeys } from '@/lib/query/keys';
import i18n from '@/i18n';
import type { ReportRequest, RequestReportData } from '@/types';

export function useReports(params?: Record<string, unknown>, options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: queryKeys.reports.list(params ?? {}),
    queryFn: () => reportsApi.getReports(params),
    enabled: options?.enabled,
    refetchInterval: (query) => {
      const data = query.state.data;
      const reports = (data as any)?.data ?? [];
      const hasActive = reports.some(
        (r: any) => r.status === 'Pending' || r.status === 'Processing'
      );
      return hasActive ? 5000 : false;
    },
  });
}

export function useRequestReport() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: RequestReportData) => reportsApi.requestReport(data),
    onSuccess: (report: ReportRequest) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.reports.all });
      if (report.status === 'Completed') {
        toast.success(i18n.t('reports.alreadyReady'), {
          action: {
            label: i18n.t('reports.viewReports'),
            onClick: () => { window.location.href = '/reports'; },
          },
        });
      } else {
        toast.success(i18n.t('reports.requested'), {
          description: i18n.t('reports.requestedDesc'),
          action: {
            label: i18n.t('reports.viewReports'),
            onClick: () => { window.location.href = '/reports'; },
          },
        });
      }
    },
    onError: () => {},
  });
}

export function useDownloadReport() {
  return useMutation({
    mutationFn: (id: string) => reportsApi.getDownloadUrl(id),
    onSuccess: (url: string) => {
      if (!url) {
        toast.error(i18n.t('reports.downloadFailed'));
        return;
      }
      const link = document.createElement('a');
      link.href = url;
      link.target = '_blank';
      link.rel = 'noopener noreferrer';
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
    },
    onError: () => { toast.error(i18n.t('reports.downloadFailed')); },
  });
}

export function useDeleteReport() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => reportsApi.deleteReport(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.reports.all });
    },
    onError: () => {},
  });
}
