import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { ShieldCheck, ShieldOff, Copy, Check } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useSetup2FA, useVerify2FA, useDisable2FA } from '@/features/auth/api';
import type { User } from '@/types';

interface TwoFactorSetupProps {
  user: User;
}

type Step = 'idle' | 'scanning' | 'backup' | 'disabling';

export function TwoFactorSetup({ user }: TwoFactorSetupProps) {
  const { t } = useTranslation();
  const [step, setStep] = useState<Step>('idle');
  const [secret, setSecret] = useState('');
  const [qrCodeUri, setQrCodeUri] = useState('');
  const [verifyCode, setVerifyCode] = useState('');
  const [backupCodes, setBackupCodes] = useState<string[]>([]);
  const [disableCode, setDisableCode] = useState('');
  const [copiedBackup, setCopiedBackup] = useState(false);

  const { mutate: setup2FA, isPending: isSettingUp } = useSetup2FA();
  const { mutate: verify2FA, isPending: isVerifying } = useVerify2FA();
  const { mutate: disable2FA, isPending: isDisabling } = useDisable2FA();

  const handleSetup = () => {
    setup2FA(undefined, {
      onSuccess: (data) => {
        setSecret(data.secret);
        setQrCodeUri(data.qrCodeUri);
        setStep('scanning');
      },
      onError: () => setStep('idle'),
    });
  };

  const handleVerify = () => {
    if (!verifyCode.trim()) return;
    verify2FA(
      { secret, code: verifyCode.trim() },
      {
        onSuccess: (data) => {
          setBackupCodes(data.backupCodes);
          setStep('backup');
          setVerifyCode('');
        },
      }
    );
  };

  const handleDisable = () => {
    if (!disableCode.trim()) return;
    disable2FA(
      { code: disableCode.trim() },
      {
        onSuccess: () => {
          setStep('idle');
          setDisableCode('');
          setSecret('');
          setQrCodeUri('');
        },
      }
    );
  };

  const handleCopyBackupCodes = async () => {
    try {
      const text = backupCodes.join('\n');
      await navigator.clipboard.writeText(text);
      setCopiedBackup(true);
      setTimeout(() => setCopiedBackup(false), 2000);
    } catch {
      toast.error(t('common.copyFailed'));
    }
  };

  const handleDone = () => {
    setStep('idle');
    setBackupCodes([]);
    setSecret('');
    setQrCodeUri('');
  };

  const isEnabled = user.twoFactorEnabled;

  return (
    <Card>
      <CardContent className="py-6">
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-2">
            <ShieldCheck className="h-5 w-5 text-primary" />
            <h3 className="text-lg font-semibold text-foreground">
              {t('twoFactor.title')}
            </h3>
          </div>
          {isEnabled && step === 'idle' && (
            <Badge variant="default">{t('twoFactor.enabled')}</Badge>
          )}
          {!isEnabled && step === 'idle' && (
            <Badge variant="secondary">{t('twoFactor.disabled')}</Badge>
          )}
        </div>

        {/* Idle state - show enable/disable buttons */}
        {step === 'idle' && !isEnabled && (
          <div>
            <p className="text-sm text-muted-foreground mb-4">
              {t('twoFactor.description')}
            </p>
            <Button onClick={handleSetup} disabled={isSettingUp}>
              <ShieldCheck className="h-4 w-4" />
              {isSettingUp ? t('common.loading') : t('twoFactor.enable')}
            </Button>
          </div>
        )}

        {step === 'idle' && isEnabled && (
          <div className="space-y-4">
            <p className="text-sm text-muted-foreground">
              {t('twoFactor.enabledDescription')}
            </p>
            <Button
              variant="destructive"
              onClick={() => setStep('disabling')}
            >
              <ShieldOff className="h-4 w-4" />
              {t('twoFactor.disable')}
            </Button>
          </div>
        )}

        {/* Step 1: Scan QR code */}
        {step === 'scanning' && (
          <div className="space-y-4">
            <p className="text-sm text-muted-foreground">
              {t('twoFactor.scanQR')}
            </p>
            <div className="flex justify-center">
              <img
                src={`https://api.qrserver.com/v1/create-qr-code/?size=200x200&data=${encodeURIComponent(qrCodeUri)}`}
                alt={t('twoFactor.qrCodeAlt')}
                className="rounded-lg border"
                width={200}
                height={200}
              />
            </div>
            <div className="space-y-1">
              <Label className="text-xs text-muted-foreground">
                {t('twoFactor.manualEntry')}
              </Label>
              <code className="block rounded bg-muted px-3 py-2 text-xs break-all select-all">
                {secret}
              </code>
            </div>
            <div className="space-y-2">
              <Label htmlFor="verifyCode">{t('twoFactor.enterCode')}</Label>
              <Input
                id="verifyCode"
                type="text"
                placeholder="000000"
                maxLength={6}
                value={verifyCode}
                onChange={(e) => setVerifyCode(e.target.value.replace(/[^0-9]/g, ''))}
                className="text-center text-lg tracking-widest max-w-48"
                autoFocus
              />
            </div>
            <div className="flex gap-2">
              <Button onClick={handleVerify} disabled={isVerifying || !verifyCode.trim()}>
                {isVerifying ? t('common.loading') : t('twoFactor.verifyAndEnable')}
              </Button>
              <Button
                variant="outline"
                onClick={() => {
                  setStep('idle');
                  setVerifyCode('');
                }}
              >
                {t('common.cancel')}
              </Button>
            </div>
          </div>
        )}

        {/* Step 2: Show backup codes */}
        {step === 'backup' && (
          <div className="space-y-4">
            <div className="rounded-lg border border-amber-200 bg-amber-50 dark:border-amber-800 dark:bg-amber-950 p-4">
              <h4 className="font-semibold text-amber-800 dark:text-amber-200 mb-2">
                {t('twoFactor.backupCodes')}
              </h4>
              <p className="text-sm text-amber-700 dark:text-amber-300 mb-3">
                {t('twoFactor.backupCodesDesc')}
              </p>
              <div className="grid grid-cols-2 gap-2">
                {backupCodes.map((code) => (
                  <code
                    key={code}
                    className="rounded bg-white dark:bg-amber-900 px-3 py-1.5 text-sm font-mono text-center"
                  >
                    {code}
                  </code>
                ))}
              </div>
              <Button
                variant="outline"
                size="sm"
                className="mt-3"
                onClick={handleCopyBackupCodes}
              >
                {copiedBackup ? (
                  <Check className="h-4 w-4" />
                ) : (
                  <Copy className="h-4 w-4" />
                )}
                {copiedBackup ? t('twoFactor.copied') : t('twoFactor.copyAll')}
              </Button>
            </div>
            <Button onClick={handleDone}>{t('twoFactor.done')}</Button>
          </div>
        )}

        {/* Disable flow */}
        {step === 'disabling' && (
          <div className="space-y-4">
            <p className="text-sm text-muted-foreground">
              {t('twoFactor.disableConfirm')}
            </p>
            <div className="space-y-2">
              <Label htmlFor="disableCode">{t('twoFactor.enterCode')}</Label>
              <Input
                id="disableCode"
                type="text"
                placeholder="000000"
                value={disableCode}
                onChange={(e) => setDisableCode(e.target.value.replace(/[^0-9]/g, ''))}
                className="text-center text-lg tracking-widest max-w-48"
                autoFocus
              />
            </div>
            <div className="flex gap-2">
              <Button
                variant="destructive"
                onClick={handleDisable}
                disabled={isDisabling || !disableCode.trim()}
              >
                {isDisabling ? t('common.loading') : t('twoFactor.disable')}
              </Button>
              <Button
                variant="outline"
                onClick={() => {
                  setStep('idle');
                  setDisableCode('');
                }}
              >
                {t('common.cancel')}
              </Button>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
