export interface Product {
  id: string;
  tenantId?: string;
  tenantName?: string;
  name: string;
  slug: string;
  description?: string;
  price: number;
  currency: string;
  status: ProductStatus;
  imageFileId?: string;
  createdAt: string;
  modifiedAt?: string;
}

export type ProductStatus = 'Draft' | 'Active' | 'Archived';

export interface CreateProductData {
  name: string;
  slug: string;
  description?: string;
  price: number;
  currency: string;
  tenantId?: string;
}

export interface UpdateProductData {
  id: string;
  name: string;
  description?: string;
  price: number;
  currency: string;
  tenantId?: string;
}
