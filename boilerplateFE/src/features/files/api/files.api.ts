import { apiClient } from '@/lib/axios';
import { API_ENDPOINTS } from '@/config';
import type { FileMetadata, UploadFileData, UpdateFileData, ApiResponse } from '@/types';

/** Tags come as comma-separated string from API — normalize to array. */
function normalizeTags(file: FileMetadata): FileMetadata {
  return {
    ...file,
    tags: typeof file.tags === 'string'
      ? (file.tags as string).split(',').map(t => t.trim()).filter(Boolean)
      : file.tags,
  };
}

export const filesApi = {
  getFiles: (params?: Record<string, unknown>) =>
    apiClient.get(API_ENDPOINTS.FILES.LIST, { params }).then(r => {
      const data = r.data;
      if (data.data && Array.isArray(data.data)) {
        data.data = data.data.map(normalizeTags);
      }
      return data;
    }),

  getFileById: async (id: string): Promise<FileMetadata> => {
    const response = await apiClient.get<{ data: FileMetadata }>(API_ENDPOINTS.FILES.DETAIL(id));
    return normalizeTags(response.data.data);
  },

  getFileUrl: async (id: string): Promise<string> => {
    const response = await apiClient.get<{ data: string }>(API_ENDPOINTS.FILES.URL(id));
    return response.data.data;
  },

  uploadFile: async (data: UploadFileData): Promise<FileMetadata> => {
    const formData = new FormData();
    formData.append('file', data.file);
    if (data.category) formData.append('category', data.category);
    if (data.description) formData.append('description', data.description);
    if (data.tags) formData.append('tags', data.tags);
    if (data.entityType) formData.append('entityType', data.entityType);
    if (data.entityId) formData.append('entityId', data.entityId);
    formData.append('visibility', data.visibility ?? 'Private');

    const response = await apiClient.post<{ data: FileMetadata }>(
      API_ENDPOINTS.FILES.UPLOAD,
      formData,
      { headers: { 'Content-Type': 'multipart/form-data' } }
    );
    return normalizeTags(response.data.data);
  },

  uploadTemp: async (file: File, description?: string): Promise<FileMetadata> => {
    const formData = new FormData();
    formData.append('file', file);
    if (description) formData.append('description', description);
    const response = await apiClient.post<ApiResponse<FileMetadata>>(
      API_ENDPOINTS.FILES.UPLOAD_TEMP,
      formData,
      { headers: { 'Content-Type': 'multipart/form-data' } }
    );
    return response.data.data;
  },

  updateFile: async (id: string, data: UpdateFileData): Promise<void> => {
    await apiClient.put(API_ENDPOINTS.FILES.DETAIL(id), data);
  },

  deleteFile: async (id: string): Promise<void> => {
    await apiClient.delete(API_ENDPOINTS.FILES.DELETE(id));
  },

  getStorageSummary: async (params?: { allTenants?: boolean }): Promise<StorageSummary> => {
    const response = await apiClient.get<{ data: StorageSummary }>(
      API_ENDPOINTS.FILES.STORAGE_SUMMARY,
      { params },
    );
    return response.data.data;
  },
};

export interface StorageSummary {
  totalBytes: number;
  byCategory: { category: string; bytes: number; fileCount: number }[];
  byEntityType: { entityType: string | null; bytes: number; fileCount: number }[];
  topUploaders: { userId: string; userName: string | null; bytes: number; fileCount: number }[];
}
