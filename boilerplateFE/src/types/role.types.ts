export interface Permission {
  id: string;
  name: string;
  description?: string;
  module: string;
  isActive: boolean;
}

export interface Role {
  id: string;
  name: string;
  description?: string;
  isSystemRole: boolean;
  isActive: boolean;
  createdAt: string;
  modifiedAt?: string;
  userCount: number;
  permissions?: Permission[];
}

export interface CreateRoleData {
  name: string;
  description?: string;
}

export interface UpdateRoleData {
  name?: string;
  description?: string;
}

export interface UpdateRolePermissionsData {
  permissionIds: string[];
}
