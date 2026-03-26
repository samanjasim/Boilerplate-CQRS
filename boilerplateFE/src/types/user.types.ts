export interface User {
  id: string;
  username: string;
  email: string;
  firstName: string;
  lastName: string;
  fullName?: string;
  phoneNumber?: string | null;
  emailConfirmed?: boolean;
  phoneConfirmed?: boolean;
  status?: string;
  tenantId?: string;
  tenantName?: string;
  tenantSlug?: string | null;
  roles?: string[];
  permissions?: string[];
  lastLoginAt?: string;
  createdAt?: string;
  updatedAt?: string;
  twoFactorEnabled?: boolean;
}

export interface UpdateUserData {
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string | null;
}

export interface UserListParams {
  status?: string;
  role?: string;
  emailConfirmed?: boolean;
  pageNumber?: number;
  pageSize?: number;
  sortBy?: string;
  sortDescending?: boolean;
  searchTerm?: string;
}
