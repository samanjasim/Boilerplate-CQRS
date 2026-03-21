import { z } from 'zod';

export const createRoleSchema = z.object({
  name: z
    .string()
    .min(1, 'Role name is required')
    .min(2, 'Role name must be at least 2 characters')
    .max(50, 'Role name must be at most 50 characters'),
  description: z
    .string()
    .max(500, 'Description must be at most 500 characters')
    .optional(),
});

export const updateRoleSchema = z.object({
  name: z
    .string()
    .min(1, 'Role name is required')
    .min(2, 'Role name must be at least 2 characters')
    .max(50, 'Role name must be at most 50 characters')
    .optional(),
  description: z
    .string()
    .max(500, 'Description must be at most 500 characters')
    .optional(),
});

export type CreateRoleFormData = z.infer<typeof createRoleSchema>;
export type UpdateRoleFormData = z.infer<typeof updateRoleSchema>;
