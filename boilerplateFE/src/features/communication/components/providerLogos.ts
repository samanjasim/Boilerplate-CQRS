/**
 * Inline SVG strings for known channel providers and integration types.
 *
 * Logos are intentionally minimal monochrome marks (use currentColor) so they
 * tint with the surrounding text and adapt to light/dark themes without
 * per-theme variants. For brand-faithful logos, swap individual entries with
 * vendor-supplied SVG marks while keeping the (24, 24) viewBox contract.
 */

export const PROVIDER_LOGOS: Record<string, string> = {
  // Email
  Smtp: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="5" width="18" height="14" rx="2"/><path d="m3 7 9 6 9-6"/></svg>',
  SendGrid: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M3 3h9v9H3zM12 12h9v9h-9z" opacity=".7"/><path d="M12 3h9v9h-9zM3 12h9v9H3z"/></svg>',
  Ses: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M5 4h14l-1 4H6zM4 9h16l-1 11H5z" opacity=".85"/></svg>',
  // SMS / Voice
  Twilio: '<svg viewBox="0 0 24 24" fill="currentColor"><circle cx="12" cy="12" r="9" opacity=".15"/><circle cx="9" cy="9" r="1.6"/><circle cx="15" cy="9" r="1.6"/><circle cx="9" cy="15" r="1.6"/><circle cx="15" cy="15" r="1.6"/></svg>',
  // Push
  Fcm: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M6 4h12l-2 7 3 5-4-1-1 4-3-3-3 3-1-4-4 1 3-5z"/></svg>',
  Apns: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M16 13c-.7 1.5-2 2.6-3.5 3-1.5-.4-2.8-1.5-3.5-3-1-2 0-5 2-5.5.7-.2 1.3 0 2 .3.7-.3 1.3-.5 2-.3 2 .5 3 3.5 2 5.5zM13 4c-.6.6-1.5 1-2 1.7-.5 0-1-.5-1-1.2.5-.8 1.5-1.5 2-1.5.5 0 1 .3 1 1z"/></svg>',
  // WhatsApp
  TwilioWhatsApp: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 2a10 10 0 0 0-8 16l-2 4 4-2a10 10 0 1 0 6-18zm5 13c-.5 1-2 2-3 2-1.5 0-4-2-6-4s-4-4.5-4-6c0-1 1-2.5 2-3l1 1-1 2 1 2 2 2 2 1 2-1 1 1-1 1z"/></svg>',
  MetaWhatsApp: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 2a10 10 0 0 0-8 16l-2 4 4-2a10 10 0 1 0 6-18zm5 13c-.5 1-2 2-3 2-1.5 0-4-2-6-4s-4-4.5-4-6c0-1 1-2.5 2-3l1 1-1 2 1 2 2 2 2 1 2-1 1 1-1 1z"/></svg>',
  // Realtime
  Ably: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 3 3 21h18zM12 9l4 8H8z"/></svg>',
  // Team integrations
  Slack: '<svg viewBox="0 0 24 24" fill="currentColor"><rect x="9" y="3" width="3" height="9" rx="1.5"/><rect x="3" y="12" width="9" height="3" rx="1.5"/><rect x="12" y="9" width="3" height="9" rx="1.5"/><rect x="12" y="15" width="9" height="3" rx="1.5" opacity=".7"/></svg>',
  Telegram: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="m22 3-9 17-2-7-8-3z" opacity=".85"/><path d="m22 3-11 9 2 8z"/></svg>',
  Discord: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M19 5a16 16 0 0 0-4-1l-.5 1A14 14 0 0 0 9.5 5L9 4a16 16 0 0 0-4 1c-2 3-3 7-2 11 1.5 1 3 1.7 5 2l1-2c-1-.3-2-.7-3-1.3a8 8 0 0 0 14 0c-1 .6-2 1-3 1.3l1 2c2-.3 3.5-1 5-2 1-4 0-8-2-11zM10 14a1.6 1.6 0 1 1 0-3 1.6 1.6 0 0 1 0 3zm4 0a1.6 1.6 0 1 1 0-3 1.6 1.6 0 0 1 0 3z"/></svg>',
  MicrosoftTeams: '<svg viewBox="0 0 24 24" fill="currentColor"><rect x="2" y="6" width="11" height="12" rx="1"/><text x="7.5" y="15" fill="var(--background)" font-size="9" font-weight="700" text-anchor="middle">T</text><circle cx="17" cy="9" r="3"/><rect x="14" y="13" width="6" height="6" rx="2"/></svg>',
};

export type KnownProvider = keyof typeof PROVIDER_LOGOS;

export function isKnownProvider(name: string): name is KnownProvider {
  return name in PROVIDER_LOGOS;
}
