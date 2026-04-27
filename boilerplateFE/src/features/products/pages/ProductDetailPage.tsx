import { useRef, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Archive, ImageIcon, Send, Upload } from 'lucide-react';
import { PageHeader } from '@/components/common';
import { ConfirmDialog } from '@/components/common';
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
import { PERMISSIONS } from '@/constants';
import { useAuthStore, selectUser } from '@/stores';
import { useTenants } from '@/features/tenants/api';
import { useProduct, useUpdateProduct, usePublishProduct, useArchiveProduct, useUploadProductImage } from '../api';
import { useFileUrl } from '@/features/files/api';
import type { Product } from '@/types';

const CURRENCIES = ['IQD', 'USD'] as const;

const schema = z.object({
  name: z.string().min(1, 'Name is required').max(200),
  description: z.string().max(2000).optional(),
  price: z.number().min(0, 'Price must be non-negative'),
  currency: z.string().min(1, 'Currency is required').max(3),
});

type FormValues = z.infer<typeof schema>;

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
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [showArchiveDialog, setShowArchiveDialog] = useState(false);
  const [selectedTenantId, setSelectedTenantId] = useState<string>(product.tenantId ?? '');

  const { data: tenantsData } = useTenants(isPlatformAdmin ? { pageSize: 100 } : undefined);
  const tenants = tenantsData?.data ?? [];

  const canEdit = hasPermission(PERMISSIONS.Products.Update);

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    formState: { errors, isDirty: isFormDirty, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: product.name,
      description: product.description ?? '',
      price: product.price,
      currency: product.currency,
    },
  });

  const isTenantDirty = isPlatformAdmin && selectedTenantId !== (product.tenantId ?? '');
  const isDirty = isFormDirty || isTenantDirty;

  async function onSubmit(data: FormValues) {
    await updateProduct.mutateAsync({
      id: product.id,
      ...data,
      ...(isPlatformAdmin && selectedTenantId ? { tenantId: selectedTenantId } : {}),
    });
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
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={product.name}
        subtitle={product.slug}
        breadcrumbs={[
          { to: ROUTES.PRODUCTS.LIST, label: t('products.title', 'Products') },
          { label: product?.name ?? t('common.loading') },
        ]}
        actions={
          <div className="flex items-center gap-2">
            <Badge
              variant={
                product.status === 'Active' ? 'default' : product.status === 'Draft' ? 'secondary' : 'outline'
              }
            >
              {product.status}
            </Badge>
            {canEdit && product.status === 'Draft' && (
              <Button onClick={handlePublish} disabled={publishProduct.isPending}>
                <Send className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
                {t('products.publish', 'Publish')}
              </Button>
            )}
            {canEdit && product.status === 'Active' && (
              <Button variant="outline" onClick={() => setShowArchiveDialog(true)}>
                <Archive className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
                {t('products.archive', 'Archive')}
              </Button>
            )}
          </div>
        }
      />

      <form onSubmit={handleSubmit(onSubmit)} className="max-w-2xl space-y-6">
        {(isPlatformAdmin || product.tenantName) && (
          <Card>
            <CardHeader>
              <CardTitle>{t('common.tenant', 'Tenant')}</CardTitle>
            </CardHeader>
            <CardContent>
              {isPlatformAdmin ? (
                <div className="space-y-2">
                  <Label>{t('products.assignedTenant', 'Assigned Tenant')}</Label>
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
              ) : (
                <div className="space-y-2">
                  <Label>{t('common.tenant', 'Tenant')}</Label>
                  <p className="text-sm text-muted-foreground">{product.tenantName}</p>
                </div>
              )}
            </CardContent>
          </Card>
        )}

        <Card>
          <CardHeader>
            <CardTitle>{t('products.details', 'Product Details')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="name">{t('products.name', 'Name')}</Label>
              <Input id="name" {...register('name')} disabled={!canEdit} />
              {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
            </div>

            <div className="space-y-2">
              <Label htmlFor="description">{t('products.description', 'Description')}</Label>
              <Textarea id="description" {...register('description')} rows={3} disabled={!canEdit} />
              {errors.description && (
                <p className="text-sm text-destructive">{errors.description.message}</p>
              )}
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>{t('products.pricing', 'Pricing')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="price">{t('products.price', 'Price')}</Label>
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
                <Label htmlFor="currency">{t('products.currency', 'Currency')}</Label>
                {/* eslint-disable-next-line react-hooks/incompatible-library */}
                <Select value={watch('currency')} onValueChange={(v) => setValue('currency', v, { shouldValidate: true, shouldDirty: true })} disabled={!canEdit}>
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

        <Card>
          <CardHeader>
            <CardTitle>{t('products.image', 'Product Image')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <ProductImagePreview imageFileId={product.imageFileId} productName={product.name} />
            {canEdit && (
              <>
                <input
                  ref={fileInputRef}
                  type="file"
                  accept="image/*"
                  className="hidden"
                  onChange={handleImageUpload}
                />
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => fileInputRef.current?.click()}
                  disabled={uploadImage.isPending}
                >
                  <Upload className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
                  {uploadImage.isPending
                    ? t('common.uploading', 'Uploading...')
                    : product.imageFileId
                      ? t('products.changeImage', 'Change Image')
                      : t('products.uploadImage', 'Upload Image')}
                </Button>
              </>
            )}
          </CardContent>
        </Card>

        {canEdit && (
          <div className="flex justify-end">
            <Button type="submit" disabled={!isDirty || isSubmitting || updateProduct.isPending}>
              {t('common.saveChanges', 'Save Changes')}
            </Button>
          </div>
        )}
      </form>

      <Slot id="entity-detail-workflow" props={{ entityType: 'Product', entityId: product.id }} />
      <Slot id="entity-detail-timeline" props={{ entityType: 'Product', entityId: product.id, tenantId: product.tenantId }} />

      <ConfirmDialog
        isOpen={showArchiveDialog}
        onClose={() => setShowArchiveDialog(false)}
        title={t('products.archiveTitle', 'Archive Product')}
        description={t(
          'products.archiveDescription',
          'Are you sure you want to archive this product? This action can be reversed.',
        )}
        onConfirm={handleArchive}
        confirmLabel={t('products.archive', 'Archive')}
        variant="danger"
      />
    </div>
  );
}

function ProductImagePreview({ imageFileId, productName }: { imageFileId?: string; productName: string }) {
  const { t } = useTranslation();
  const { data: imageUrl, isLoading } = useFileUrl(imageFileId ?? '');

  if (!imageFileId) {
    return (
      <div className="flex h-48 items-center justify-center rounded-xl border border-dashed border-muted-foreground/25 bg-muted/50">
        <div className="text-center">
          <ImageIcon className="mx-auto h-10 w-10 text-muted-foreground/40" />
          <p className="mt-2 text-sm text-muted-foreground">{t('products.noImage', 'No image uploaded')}</p>
        </div>
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className="flex h-48 items-center justify-center rounded-xl bg-muted/50">
        <Spinner size="md" />
      </div>
    );
  }

  return (
    <div className="overflow-hidden rounded-xl border bg-muted/50">
      <img
        src={imageUrl}
        alt={productName}
        className="h-48 w-full object-contain"
      />
    </div>
  );
}
