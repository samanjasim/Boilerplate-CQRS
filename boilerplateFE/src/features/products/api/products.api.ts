import { apiClient } from '@/lib/axios';
import { API_ENDPOINTS } from '@/config';
import type { Product, ProductStatusCounts, CreateProductData, UpdateProductData } from '@/types';

export const productsApi = {
  getAll: (params?: Record<string, unknown>) =>
    apiClient.get(API_ENDPOINTS.PRODUCTS.LIST, { params }).then((r) => r.data),

  getById: (id: string) =>
    apiClient.get<{ data: Product }>(API_ENDPOINTS.PRODUCTS.DETAIL(id)).then((r) => r.data.data),

  getStatusCounts: (params?: Record<string, unknown>) =>
    apiClient
      .get<{ data: ProductStatusCounts }>(API_ENDPOINTS.PRODUCTS.STATUS_COUNTS, { params })
      .then((r) => r.data),

  create: (data: CreateProductData) =>
    apiClient.post(API_ENDPOINTS.PRODUCTS.LIST, data).then((r) => r.data),

  update: (data: UpdateProductData) =>
    apiClient.put(API_ENDPOINTS.PRODUCTS.DETAIL(data.id), data).then((r) => r.data),

  publish: (id: string) =>
    apiClient.post(API_ENDPOINTS.PRODUCTS.PUBLISH(id)).then((r) => r.data),

  archive: (id: string) =>
    apiClient.post(API_ENDPOINTS.PRODUCTS.ARCHIVE(id)).then((r) => r.data),

  uploadImage: (id: string, file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    return apiClient
      .post(API_ENDPOINTS.PRODUCTS.IMAGE(id), formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })
      .then((r) => r.data);
  },
};
