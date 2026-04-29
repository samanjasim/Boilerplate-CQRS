import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { PageHeader } from '@/components/common';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ROUTES } from '@/config';
import { useAuthStore, selectUser } from '@/stores';
import { useTenants } from '@/features/tenants/api';
import { useCreateProduct } from '../api';

const CURRENCIES = ['IQD', 'USD'] as const;

const schema = z.object({
  name: z.string().min(1, 'Name is required').max(200),
  slug: z
    .string()
    .min(1, 'Slug is required')
    .max(200)
    .regex(/^[a-z0-9]([a-z0-9-]*[a-z0-9])?$/, 'Slug must be lowercase alphanumeric with hyphens, starting and ending with a letter or number'),
  description: z.string().max(2000).optional(),
  price: z.number().min(0, 'Price must be non-negative'),
  currency: z.string().min(1, 'Currency is required').max(3),
});

type FormValues = z.infer<typeof schema>;

export default function ProductCreatePage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const user = useAuthStore(selectUser);
  const isPlatformAdmin = !user?.tenantId;

  const createProduct = useCreateProduct();
  const [selectedTenantId, setSelectedTenantId] = useState<string>('');
  const { data: tenantsData } = useTenants(isPlatformAdmin ? { pageSize: 100 } : undefined);
  const tenants = tenantsData?.data ?? [];

  const {
    register,
    handleSubmit,
    setValue,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: '', slug: '', description: '', price: 0, currency: 'USD' },
  });

  // eslint-disable-next-line react-hooks/incompatible-library
  const nameValue = watch('name');

  function generateSlug() {
    const slug = nameValue
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/^-|-$/g, '');
    setValue('slug', slug, { shouldValidate: true });
  }

  async function onSubmit(data: FormValues) {
    const result = await createProduct.mutateAsync({
      ...data,
      ...(isPlatformAdmin && selectedTenantId ? { tenantId: selectedTenantId } : {}),
    });
    if (result?.data) {
      navigate(ROUTES.PRODUCTS.getDetail(result.data));
    } else {
      navigate(ROUTES.PRODUCTS.LIST);
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('products.createTitle', 'Create Product')}
        subtitle={t('products.createSubtitle', 'Add a new product to your catalog')}
        breadcrumbs={[
          { to: ROUTES.PRODUCTS.LIST, label: t('products.title', 'Products') },
          { label: t('products.create.title', 'Create') },
        ]}
      />

      <form onSubmit={handleSubmit(onSubmit)} className="max-w-2xl space-y-6">
        {isPlatformAdmin && (
          <Card variant="glass">
            <CardHeader>
              <CardTitle>{t('products.tenant', 'Tenant')}</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="space-y-2">
                <Label>{t('products.selectTenant', 'Select a tenant for this product')}</Label>
                <Select value={selectedTenantId} onValueChange={setSelectedTenantId}>
                  <SelectTrigger>
                    <SelectValue placeholder={t('products.chooseTenant', 'Choose a tenant...')} />
                  </SelectTrigger>
                  <SelectContent>
                    {tenants.map((tenant: { id: string; name: string }) => (
                      <SelectItem key={tenant.id} value={tenant.id}>
                        {tenant.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </CardContent>
          </Card>
        )}

        <Card variant="glass">
          <CardHeader>
            <CardTitle>{t('products.details', 'Product Details')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="name">{t('products.name', 'Name')}</Label>
              <Input id="name" {...register('name')} />
              {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
            </div>

            <div className="space-y-2">
              <Label htmlFor="slug">{t('products.slug', 'Slug')}</Label>
              <div className="flex gap-2">
                <Input id="slug" {...register('slug')} className="flex-1" />
                <Button type="button" variant="outline" onClick={generateSlug}>
                  {t('products.generateSlug', 'Generate')}
                </Button>
              </div>
              {errors.slug && <p className="text-sm text-destructive">{errors.slug.message}</p>}
            </div>

            <div className="space-y-2">
              <Label htmlFor="description">{t('products.description', 'Description')}</Label>
              <Textarea id="description" {...register('description')} rows={3} />
              {errors.description && (
                <p className="text-sm text-destructive">{errors.description.message}</p>
              )}
            </div>
          </CardContent>
        </Card>

        <Card variant="glass">
          <CardHeader>
            <CardTitle>{t('products.pricing', 'Pricing')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="price">{t('products.price', 'Price')}</Label>
                <Input id="price" type="number" step="0.01" min="0" {...register('price', { valueAsNumber: true })} />
                {errors.price && <p className="text-sm text-destructive">{errors.price.message}</p>}
              </div>

              <div className="space-y-2">
                <Label htmlFor="currency">{t('products.currency', 'Currency')}</Label>
                <Select value={watch('currency')} onValueChange={(v) => setValue('currency', v, { shouldValidate: true })}>
                  <SelectTrigger>
                    <SelectValue placeholder={t('products.selectCurrency', 'Select currency')} />
                  </SelectTrigger>
                  <SelectContent>
                    {CURRENCIES.map((c) => (
                      <SelectItem key={c} value={c}>{c}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {errors.currency && (
                  <p className="text-sm text-destructive">{errors.currency.message}</p>
                )}
              </div>
            </div>
          </CardContent>
        </Card>

        <div className="flex justify-end gap-3">
          <Button type="button" variant="outline" onClick={() => navigate(ROUTES.PRODUCTS.LIST)}>
            {t('common.cancel', 'Cancel')}
          </Button>
          <Button type="submit" disabled={isSubmitting || createProduct.isPending}>
            {t('products.create', 'Create Product')}
          </Button>
        </div>

        <p className="text-xs text-muted-foreground">
          {t('products.uploadHint', 'You can add an image after creating the product')}
        </p>
      </form>
    </div>
  );
}
