export type WmsDeploymentTier = 'development' | 'staging' | 'production';

/** Explicit deployment class; falls back to NODE_ENV=production when tier unset. */
export function getWmsDeploymentTier(): WmsDeploymentTier {
  const explicit = process.env.WMS_DEPLOYMENT_TIER?.trim().toLowerCase();
  if (explicit === 'production' || explicit === 'prod') return 'production';
  if (explicit === 'staging' || explicit === 'stage') return 'staging';
  if (explicit === 'development' || explicit === 'dev') return 'development';
  if (process.env.NODE_ENV === 'production') return 'production';
  return 'development';
}

export function isWmsProductionDeployment(): boolean {
  return getWmsDeploymentTier() === 'production';
}