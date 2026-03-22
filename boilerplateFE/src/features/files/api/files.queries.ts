import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { filesApi } from './files.api';
import { queryKeys } from '@/lib/query/keys';
import i18n from '@/i18n';
import type { UploadFileData, UpdateFileData } from '@/types';

export function useFiles(params?: Record<string, unknown>) {
  return useQuery({
    queryKey: queryKeys.files.list(params),
    queryFn: () => filesApi.getFiles(params),
  });
}

export function useFile(id: string) {
  return useQuery({
    queryKey: queryKeys.files.detail(id),
    queryFn: () => filesApi.getFileById(id),
    enabled: !!id,
  });
}

export function useFileUrl(id: string) {
  return useQuery({
    queryKey: queryKeys.files.url(id),
    queryFn: () => filesApi.getFileUrl(id),
    enabled: !!id,
    staleTime: 50 * 60 * 1000, // 50 minutes (signed URLs expire in 60)
  });
}

export function useUploadFile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: UploadFileData) => filesApi.uploadFile(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.files.all });
      toast.success(i18n.t('files.fileUploaded'));
    },
  });
}

export function useUpdateFile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateFileData }) => filesApi.updateFile(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.files.all });
      toast.success(i18n.t('files.fileUpdated'));
    },
  });
}

export function useDeleteFile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => filesApi.deleteFile(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.files.all });
      toast.success(i18n.t('files.fileDeleted'));
    },
  });
}
