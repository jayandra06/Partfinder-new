import { existsSync, readFileSync } from 'fs';
import { join } from 'path';

/** PartFinder-Backend package root (independent of shell cwd). */
export function backendRootDir(): string {
  let dir = __dirname;
  for (let depth = 0; depth < 6; depth++) {
    const pkgPath = join(dir, 'package.json');
    if (existsSync(pkgPath)) {
      try {
        const pkg = JSON.parse(readFileSync(pkgPath, 'utf8')) as { name?: string };
        if (pkg.name === 'PartFinder-Backend') {
          return dir;
        }
      } catch {
        /* try parent */
      }
    }
    dir = join(dir, '..');
  }
  return join(__dirname, '..', '..');
}

/** `.env` paths under the backend package, not the shell cwd. */
export function backendEnvFilePaths(): string[] {
  const root = backendRootDir();
  const paths = [join(root, '.env.local'), join(root, '.env')];
  const existing = paths.filter((p) => existsSync(p));
  return existing.length > 0 ? existing : [join(root, '.env')];
}
