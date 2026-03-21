import { z } from 'zod';
import type { TFunction } from 'i18next';

export const updateUserSchema = (t: TFunction) =>
  z.object({
    firstName: z
      .string()
      .min(1, t('validation.required', { field: t('parents.firstName') }))
      .max(100, t('validation.maxLength', { field: t('parents.firstName'), max: 100 })),
    lastName: z
      .string()
      .min(1, t('validation.required', { field: t('parents.lastName') }))
      .max(100, t('validation.maxLength', { field: t('parents.lastName'), max: 100 })),
    email: z
      .string()
      .min(1, t('validation.required', { field: t('parents.email') }))
      .email(t('validation.invalidEmail'))
      .max(256, t('validation.maxLength', { field: t('parents.email'), max: 256 })),
    phoneNumber: z
      .string()
      .regex(/^\+?[1-9]\d{6,14}$/, t('validation.invalidPhone'))
      .optional()
      .or(z.literal('')),
  });

export type UpdateUserFormData = z.infer<ReturnType<typeof updateUserSchema>>;
