import { apiClient } from '@/lib/axios';
import { API_ENDPOINTS } from '@/config';
import type { FileMetadata, UploadFileData, UpdateFileData } from '@/types';

export const filesApi = {
  getFiles: (params?: Record<string, unknown>) =>
    apiClient.get(API_ENDPOINTS.FILES.LIST, { params }).then(r => r.data),

  getFileById: async (id: string): Promise<FileMetadata> => {
    const response = await apiClient.get<{ data: FileMetadata }>(API_ENDPOINTS.FILES.DETAIL(id));
    return response.data.data;
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
    formData.append('isPublic', String(data.isPublic ?? false));

    const response = await apiClient.post<{ data: FileMetadata }>(
      API_ENDPOINTS.FILES.UPLOAD,
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
};
