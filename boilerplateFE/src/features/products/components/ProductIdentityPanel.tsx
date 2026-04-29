import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Archive, Package, Send, Upload } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { STATUS_BADGE_VARIANT } from '@/constants';
import { useFileUrl } from '@/features/files/api';
import type { Product } from '@/types';
import { TenantReassignDialog } from './TenantReassignDialog';

interface ProductIdentityPanelProps {
  product: Product;
  canEdit: boolean;
  isPlatformAdmin: boolean;
  onPublish: () => void;
  onArchive: () => void;
  onUploadImage: (event: React.ChangeEvent<HTMLInputElement>) => Promise<void>;
  publishPending: boolean;
  archivePending: boolean;
  uploadPending: boolean;
}

export function ProductIdentityPanel({
  product,
  canEdit,
  isPlatformAdmin,
  onPublish,
  onArchive,
  onUploadImage,
  publishPending,
  archivePending,
  uploadPending,
}: ProductIdentityPanelProps) {
  const { t } = useTranslation();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [reassignOpen, setReassignOpen] = useState(false);
  const { data: imageUrl, isLoading } = useFileUrl(product.imageFileId ?? '');

  return (
    <aside className="surface-glass min-h-0 self-start rounded-2xl border border-border/40 p-5 shadow-card">
      <div className="space-y-5">
        <div className="flex aspect-square w-full items-center justify-center overflow-hidden rounded-2xl border border-border/40 bg-[var(--active-bg)]">
          {product.imageFileId && isLoading ? (
            <Spinner size="md" />
          ) : imageUrl ? (
            <img src={imageUrl} alt={product.name} className="h-full w-full object-contain" />
          ) : (
            <div className="px-4 text-center">
              <Package className="mx-auto h-12 w-12 text-[var(--tinted-fg)]/70" />
              <p className="mt-3 text-sm text-muted-foreground">{t('products.noImage')}</p>
            </div>
          )}
        </div>

        <div className="space-y-3">
          <div className="gradient-text font-display text-3xl font-semibold tabular-nums">
            {product.price.toFixed(2)} {product.currency}
          </div>
          <p className="font-mono text-xs tracking-tight text-muted-foreground">{product.slug}</p>
          <Badge variant={STATUS_BADGE_VARIANT[product.status] ?? 'secondary'}>
            {t(`products.status.${product.status.toLowerCase()}`, product.status)}
          </Badge>
        </div>

        {isPlatformAdmin && (
          <div className="space-y-2 rounded-xl border border-[var(--border-strong)] bg-[var(--active-bg)] p-3">
            <div className="text-xs font-medium text-[var(--tinted-fg)]">
              {t('common.tenant')}: {product.tenantName ?? t('common.none')}
            </div>
            {canEdit && (
              <Button type="button" variant="ghost" size="sm" onClick={() => setReassignOpen(true)}>
                {t('products.detail.reassignTenant')}
              </Button>
            )}
          </div>
        )}

        <div className="flex flex-wrap gap-2">
          {canEdit && (
            <>
              <input
                ref={fileInputRef}
                type="file"
                accept="image/*"
                className="hidden"
                onChange={onUploadImage}
              />
              <Button
                type="button"
                variant="outline"
                onClick={() => fileInputRef.current?.click()}
                disabled={uploadPending}
              >
                <Upload className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
                {uploadPending
                  ? t('common.uploading')
                  : product.imageFileId
                    ? t('products.changeImage')
                    : t('products.uploadImage')}
              </Button>
            </>
          )}

          {canEdit && product.status === 'Draft' && (
            <Button type="button" className="btn-primary-gradient" onClick={onPublish} disabled={publishPending}>
              <Send className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
              {t('products.publish')}
            </Button>
          )}

          {canEdit && product.status === 'Active' && (
            <Button type="button" variant="outline" onClick={onArchive} disabled={archivePending}>
              <Archive className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
              {t('products.archive')}
            </Button>
          )}
        </div>
      </div>

      <TenantReassignDialog
        isOpen={reassignOpen}
        onClose={() => setReassignOpen(false)}
        product={product}
      />
    </aside>
  );
}
