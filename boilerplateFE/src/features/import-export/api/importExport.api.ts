import { apiClient } from '@/lib/axios';
import { API_ENDPOINTS } from '@/config';
import type {
  EntityType,
  ImportJob,
  ImportPreview,
  StartImportData,
  PreviewImportData,
} from '@/types';

export const importExportApi = {
  getEntityTypes: () =>
    apiClient.get<{ data: EntityType[] }>(API_ENDPOINTS.IMPORT_EXPORT.TYPES).then(r => r.data.data),

  downloadTemplate: async (entityType: string) => {
    const response = await apiClient.get(API_ENDPOINTS.IMPORT_EXPORT.TEMPLATE(entityType), {
      responseType: 'blob',
    });
    const url = window.URL.createObjectURL(new Blob([response.data]));
    const link = document.createElement('a');
    link.href = url;
    link.download = `${entityType}_template.csv`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
  },

  previewImport: (data: PreviewImportData) =>
    apiClient.post<{ data: ImportPreview }>(API_ENDPOINTS.IMPORT_EXPORT.PREVIEW, data).then(r => r.data.data),

  startImport: (data: StartImportData) =>
    apiClient.post<{ data: ImportJob }>(API_ENDPOINTS.IMPORT_EXPORT.IMPORT, data).then(r => r.data.data),

  getImportJobs: (params?: Record<string, unknown>) =>
    apiClient.get(API_ENDPOINTS.IMPORT_EXPORT.IMPORTS, { params }).then(r => r.data),

  getImportJobById: (id: string) =>
    apiClient.get<{ data: ImportJob }>(API_ENDPOINTS.IMPORT_EXPORT.IMPORT_DETAIL(id)).then(r => r.data.data),

  getImportErrorUrl: (id: string) =>
    apiClient.get<{ data: string }>(API_ENDPOINTS.IMPORT_EXPORT.IMPORT_ERRORS(id)).then(r => r.data.data),

  deleteImportJob: (id: string) =>
    apiClient.delete(API_ENDPOINTS.IMPORT_EXPORT.IMPORT_DETAIL(id)).then(r => r.data),
};
