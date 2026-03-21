export interface ApiResponse<T> {
  data: T;
  message?: string | null;
  success: boolean;
  errors?: Record<string, string[]> | null;
  validationErrors?: Record<string, string[]> | null;
}

export interface ApiError {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  success?: boolean;
  message?: string;
  errors?: Record<string, string[]> | null;
  validationErrors?: Record<string, string[]> | null;
}

export interface PaginatedResponse<T> {
  data: T[];
  pagination: PaginationMeta;
  success: boolean;
  message?: string | null;
  errors?: Record<string, string[]> | null;
  validationErrors?: Record<string, string[]> | null;
}

export interface PaginationMeta {
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  totalCount: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface PaginationParams {
  pageNumber?: number;
  pageSize?: number;
}

export interface SortParams {
  sortBy?: string;
  sortDescending?: boolean;
}

export interface SearchParams {
  searchTerm?: string;
}

export type QueryParams = PaginationParams & SortParams & SearchParams;
