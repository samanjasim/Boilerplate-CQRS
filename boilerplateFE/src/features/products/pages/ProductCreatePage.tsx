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

interface FormValues {
  name: string;
  slug: string;
  description?: string;
  price: number;
  currency: string;
}

export default function ProductCreatePage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const user = useAuthStore(selectUser);
  const isPlatformAdmin = !user?.tenantId;

  const createProduct = useCreateProduct();
  const [selectedTenantId, setSelectedTenantId] = useState<string>('');
  const { data: tenantsData } = useTenants(isPlatformAdmin ? { pageSize: 100 } : undefined);
  const tenants = tenantsData?.data ?? [];
  const schema = z.object({
    name: z.string().min(1, t('products.validation.nameRequired')).max(200),
    slug: z
      .string()
      .min(1, t('products.validation.slugRequired'))
      .max(200)
      .regex(/^[a-z0-9]([a-z0-9-]*[a-z0-9])?$/, t('products.validation.slugInvalid')),
    description: z.string().max(2000).optional(),
    price: z.number().min(0, t('products.validation.priceNonNegative')),
    currency: z.string().min(1, t('products.validation.currencyRequired')).max(3),
  });

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
        title={t('products.createTitle')}
        subtitle={t('products.createSubtitle')}
        breadcrumbs={[
          { to: ROUTES.PRODUCTS.LIST, label: t('products.title') },
          { label: t('common.create') },
        ]}
      />

      <form onSubmit={handleSubmit(onSubmit)} className="max-w-2xl space-y-6">
        {isPlatformAdmin && (
          <Card variant="glass">
            <CardHeader>
              <CardTitle>{t('products.tenant')}</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="space-y-2">
                <Label>{t('products.selectTenant')}</Label>
                <Select value={selectedTenantId} onValueChange={setSelectedTenantId}>
                  <SelectTrigger>
                    <SelectValue placeholder={t('products.chooseTenant')} />
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
            <CardTitle>{t('products.details')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="name">{t('products.name')}</Label>
              <Input id="name" {...register('name')} />
              {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
            </div>

            <div className="space-y-2">
              <Label htmlFor="slug">{t('products.slug')}</Label>
              <div className="flex gap-2">
                <Input id="slug" {...register('slug')} className="flex-1" />
                <Button type="button" variant="outline" onClick={generateSlug}>
                  {t('products.generateSlug')}
                </Button>
              </div>
              {errors.slug && <p className="text-sm text-destructive">{errors.slug.message}</p>}
            </div>

            <div className="space-y-2">
              <Label htmlFor="description">{t('products.description')}</Label>
              <Textarea id="description" {...register('description')} rows={3} />
              {errors.description && (
                <p className="text-sm text-destructive">{errors.description.message}</p>
              )}
            </div>
          </CardContent>
        </Card>

        <Card variant="glass">
          <CardHeader>
            <CardTitle>{t('products.pricing')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="price">{t('products.price')}</Label>
                <Input id="price" type="number" step="0.01" min="0" {...register('price', { valueAsNumber: true })} />
                {errors.price && <p className="text-sm text-destructive">{errors.price.message}</p>}
              </div>

              <div className="space-y-2">
                <Label htmlFor="currency">{t('products.currency')}</Label>
                <Select value={watch('currency')} onValueChange={(v) => setValue('currency', v, { shouldValidate: true })}>
                  <SelectTrigger>
                    <SelectValue placeholder={t('products.selectCurrency')} />
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
            {t('common.cancel')}
          </Button>
          <Button type="submit" disabled={isSubmitting || createProduct.isPending}>
            {t('products.create')}
          </Button>
        </div>

        <p className="text-xs text-muted-foreground">
          {t('products.uploadHint')}
        </p>
      </form>
    </div>
  );
}
