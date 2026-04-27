import { useAuthStore, selectUser } from '@/stores';
import { LanguageSwitcher } from '@/components/common/LanguageSwitcher';
import { NotificationBell } from '@/components/common/NotificationBell';
import { ThemeToggle } from '@/components/common/ThemeToggle';
import { UserAvatar } from '@/components/common/UserAvatar';
import { Section } from '../Section';

export function FunctionalSection() {
  const user = useAuthStore(selectUser);

  return (
    <Section
      id="functional"
      eyebrow="Functional UI"
      title="Header right cluster"
      deck="The four user-facing controls present on every authenticated page: notifications, language, theme, and user avatar. All ride the J4 surface-glass + primary-tinted active state."
    >
      <div className="surface-glass rounded-2xl p-4">
        <div className="flex items-center justify-end gap-2">
          <NotificationBell />
          <LanguageSwitcher />
          <ThemeToggle />
          <UserAvatar firstName={user?.firstName} lastName={user?.lastName} size="sm" />
        </div>
      </div>
      <p className="mt-2 text-xs text-muted-foreground">
        Click each to open its menu. The dropdown surfaces use <code>.surface-glass</code> with backdrop blur.
        The unread-count pill on the bell uses <code>.btn-primary-gradient</code> with mono numerics.
      </p>
    </Section>
  );
}
