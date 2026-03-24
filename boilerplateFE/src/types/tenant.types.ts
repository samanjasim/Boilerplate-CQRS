export interface Tenant {
  id: string;
  name: string;
  slug: string | null;
  status: string;
  createdAt: string;
  logoFileId: string | null;
  faviconFileId: string | null;
  primaryColor: string | null;
  secondaryColor: string | null;
  description: string | null;
  address: string | null;
  phone: string | null;
  website: string | null;
  taxId: string | null;
  loginPageTitle: string | null;
  loginPageSubtitle: string | null;
  emailFooterText: string | null;
  logoUrl: string | null;
  faviconUrl: string | null;
}

export interface TenantBranding {
  logoUrl: string | null;
  faviconUrl: string | null;
  primaryColor: string | null;
  secondaryColor: string | null;
  loginPageTitle: string | null;
  loginPageSubtitle: string | null;
  tenantName: string;
}

export interface RegisterTenantData {
  companyName: string;
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  confirmPassword: string;
}
