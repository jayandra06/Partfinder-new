import { ConfigService } from '@nestjs/config';

const DEFAULT_FORM_PATH = '/api/public/setup/reset-password/form';

/** Public URL of the reset-password HTML form (no trailing slash). */
export function resolvePasswordResetFormBaseUrl(config: ConfigService): string {
  const explicit = config.get<string>('PASSWORD_RESET_BASE_URL')?.trim();
  if (explicit) {
    return explicit.replace(/\/$/, '');
  }

  const publicApi =
    config.get<string>('PUBLIC_API_BASE_URL')?.trim() ||
    config.get<string>('BACKEND_PUBLIC_URL')?.trim() ||
    'https://backend.partfinder.store';

  const root = publicApi.replace(/\/$/, '');
  if (root.endsWith(DEFAULT_FORM_PATH)) {
    return root;
  }

  return `${root}${DEFAULT_FORM_PATH}`;
}

export function buildPasswordResetLink(
  config: ConfigService,
  token: string,
): string {
  const base = resolvePasswordResetFormBaseUrl(config);
  return `${base}?token=${encodeURIComponent(token)}`;
}

export function resolvePasswordResetApiUrl(config: ConfigService): string {
  const publicApi =
    config.get<string>('PUBLIC_API_BASE_URL')?.trim() ||
    config.get<string>('BACKEND_PUBLIC_URL')?.trim() ||
    'https://backend.partfinder.store';
  return `${publicApi.replace(/\/$/, '')}/api/public/setup/reset-password`;
}