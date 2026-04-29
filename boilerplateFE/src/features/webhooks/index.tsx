import type { WebModule } from '@/lib/modules';

/**
 * Webhooks module entry point.
 *
 * No slot contributions yet — Webhooks is currently opted in via routes
 * and the sidebar nav only. This file establishes the registration pattern
 * for future contributions (e.g., a "Webhook activity" tab on user detail).
 */
export const webhooksModule: WebModule = {
  id: 'webhooks',
  register(): void {
    // intentionally empty
  },
};
