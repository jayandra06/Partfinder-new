/**
 * Runs wms-03a2-staging-acceptance Jest suite with WMS_RUN_STAGING_ACCEPTANCE=true.
 * Works from CMD, PowerShell, and npm scripts (no cross-env required).
 */
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

import './mongodb-atlas-dns.mjs';

const backendRoot = join(dirname(fileURLToPath(import.meta.url)), '..');
const jestBin = join(backendRoot, 'node_modules', 'jest', 'bin', 'jest.js');

process.env.WMS_RUN_STAGING_ACCEPTANCE = 'true';

const result = spawnSync(
  process.execPath,
  [
    jestBin,
    '--testPathPatterns=wms-03a2-staging-acceptance',
    '--runInBand',
    ...process.argv.slice(2),
  ],
  {
    cwd: backendRoot,
    stdio: 'inherit',
    env: process.env,
  },
);

process.exit(result.status ?? 1);