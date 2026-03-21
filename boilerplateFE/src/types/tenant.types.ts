export interface Tenant {
  id: string;
  name: string;
  slug: string | null;
  status: string;
  createdAt: string;
}

export interface RegisterTenantData {
  companyName: string;
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  confirmPassword: string;
}
