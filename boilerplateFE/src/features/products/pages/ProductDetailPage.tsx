import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { PageHeader, ConfirmDialog } from '@/components/common';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Spinner } from '@/components/ui/spinner';
import { ROUTES } from '@/config';
import { Slot } from '@/lib/extensions';
import { usePermissions } from '@/hooks';
import { PERMISSIONS, STATUS_BADGE_VARIANT } from '@/constants';
import { useAuthStore, selectUser } from '@/stores';
import { useProduct, useUpdateProduct, usePublishProduct, useArchiveProduct, useUploadProductImage } from '../api';
import { ProductIdentityPanel } from '../components/ProductIdentityPanel';
import type { Product } from '@/types';

const CURRENCIES = ['IQD', 'USD'] as const;

interface FormValues {
  name: string;
  description?: string;
  price: number;
  currency: string;
}

function getDefaultValues(product: Product): FormValues {
  return {
    name: product.name,
    description: product.description ?? '',
    price: product.price,
    currency: product.currency,
  };
}

export default function ProductDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { data: product, isLoading } = useProduct(id!);

  if (isLoading || !product) {
    return (
      <div className="flex items-center justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  return <ProductDetailForm product={product} />;
}

function ProductDetailForm({ product }: { product: Product }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { hasPermission } = usePermissions();
  const user = useAuthStore(selectUser);
  const isPlatformAdmin = !user?.tenantId;

  const updateProduct = useUpdateProduct();
  const publishProduct = usePublishProduct();
  const archiveProduct = useArchiveProduct();
  const uploadImage = useUploadProductImage();
  const [showArchiveDialog, setShowArchiveDialog] = useState(false);
  const [savedValues, setSavedValues] = useState<FormValues>(() => getDefaultValues(product));

  const canEdit = hasPermission(PERMISSIONS.Products.Update);
  const schema = z.object({
    name: z.string().min(1, t('products.validation.nameRequired')).max(200),
    description: z.string().max(2000).optional(),
    price: z.number().min(0, t('products.validation.priceNonNegative')),
    currency: z.string().min(1, t('products.validation.currencyRequired')).max(3),
  });

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    reset,
    formState: { errors, isDirty, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: savedValues,
  });

  useEffect(() => {
    const nextValues = getDefaultValues(product);
    setSavedValues(nextValues);
    reset(nextValues);
  }, [product, reset]);

  async function onSubmit(data: FormValues) {
    await updateProduct.mutateAsync({
      id: product.id,
      ...data,
    });
    setSavedValues(data);
    reset(data);
  }

  function handleCancel() {
    reset(savedValues);
  }

  async function handlePublish() {
    await publishProduct.mutateAsync(product.id);
  }

  async function handleArchive() {
    await archiveProduct.mutateAsync(product.id);
    navigate(ROUTES.PRODUCTS.LIST);
  }

  async function handleImageUpload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    await uploadImage.mutateAsync({ id: product.id, file });
    e.target.value = '';
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={product.name}
        subtitle={product.slug}
        breadcrumbs={[
          { to: ROUTES.PRODUCTS.LIST, label: t('products.title') },
          { label: product.name },
        ]}
        actions={
          <Badge variant={STATUS_BADGE_VARIANT[product.status] ?? 'secondary'}>
            {t(`products.status.${product.status.toLowerCase()}`, product.status)}
          </Badge>
        }
      />

      <form
        onSubmit={handleSubmit(onSubmit)}
        className="grid gap-6 lg:grid-cols-[minmax(280px,0.4fr)_minmax(0,0.6fr)] lg:items-start"
      >
        <ProductIdentityPanel
          product={product}
          canEdit={canEdit}
          isPlatformAdmin={isPlatformAdmin}
          onPublish={handlePublish}
          onArchive={() => setShowArchiveDialog(true)}
          onUploadImage={handleImageUpload}
          publishPending={publishProduct.isPending}
          archivePending={archiveProduct.isPending}
          uploadPending={uploadImage.isPending}
        />

        <div className="min-h-0 space-y-6">
          <Card variant="glass">
            <CardHeader>
              <CardTitle>{t('products.details')}</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="name">{t('products.name')}</Label>
                <Input id="name" {...register('name')} disabled={!canEdit} />
                {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
              </div>

              <div className="space-y-2">
                <Label htmlFor="description">{t('products.description')}</Label>
                <Textarea id="description" {...register('description')} rows={3} disabled={!canEdit} />
                {errors.description && <p className="text-sm text-destructive">{errors.description.message}</p>}
              </div>
            </CardContent>
          </Card>

          <Card variant="glass">
            <CardHeader>
              <CardTitle>{t('products.pricing')}</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="price">{t('products.price')}</Label>
                  <Input
                    id="price"
                    type="number"
                    step="0.01"
                    min="0"
                    {...register('price', { valueAsNumber: true })}
                    disabled={!canEdit}
                  />
                  {errors.price && <p className="text-sm text-destructive">{errors.price.message}</p>}
                </div>

                <div className="space-y-2">
                  <Label htmlFor="currency">{t('products.currency')}</Label>
                  {/* eslint-disable-next-line react-hooks/incompatible-library */}
                  <Select
                    value={watch('currency')}
                    onValueChange={(v) => setValue('currency', v, { shouldValidate: true, shouldDirty: true })}
                    disabled={!canEdit}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder={t('products.selectCurrency')} />
                    </SelectTrigger>
                    <SelectContent>
                      {CURRENCIES.map((currency) => (
                        <SelectItem key={currency} value={currency}>
                          {currency}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  {errors.currency && <p className="text-sm text-destructive">{errors.currency.message}</p>}
                </div>
              </div>
            </CardContent>
          </Card>

          {canEdit && isDirty && (
            <div className="sticky bottom-0 z-10 flex flex-col gap-3 rounded-2xl border border-border/40 surface-glass-strong p-4 shadow-float sm:flex-row sm:items-center sm:justify-between">
              <p className="text-xs font-bold uppercase tracking-[0.14em] text-muted-foreground">
                {t('products.detail.saveFooter')}
              </p>
              <div className="flex justify-end gap-2">
                <Button type="button" variant="ghost" onClick={handleCancel}>
                  {t('common.cancel')}
                </Button>
                <Button type="submit" disabled={isSubmitting || updateProduct.isPending}>
                  {t('common.saveChanges')}
                </Button>
              </div>
            </div>
          )}
        </div>
      </form>

      <Slot id="entity-detail-workflow" props={{ entityType: 'Product', entityId: product.id }} />
      <Slot id="entity-detail-timeline" props={{ entityType: 'Product', entityId: product.id, tenantId: product.tenantId }} />

      <ConfirmDialog
        isOpen={showArchiveDialog}
        onClose={() => setShowArchiveDialog(false)}
        title={t('products.archiveTitle')}
        description={t('products.archiveDescription')}
        onConfirm={handleArchive}
        confirmLabel={t('products.archive')}
        variant="danger"
        isLoading={archiveProduct.isPending}
      />
    </div>
  );
}
