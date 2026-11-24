import { ForbiddenException } from '@nestjs/common';
import { WmsSessionGuard } from './wms-session.guard';

describe('WmsSessionGuard org header validation', () => {
  it('rejects invalid org header format', async () => {
    const orgs = {
      verifyLicense: jest.fn(),
      findByOrgCode: jest.fn(),
    };
    const tenant = {
      findOrgAdminByEmail: jest.fn(),
      findOrgAppUserByEmail: jest.fn(),
      getConnection: jest.fn(),
    };
    const guard = new WmsSessionGuard(orgs as never, tenant as never);
    const ctx = {
      switchToHttp: () => ({
        getRequest: () => ({
          user: { userId: 'a@b.com', email: 'a@b.com' },
          headers: { 'x-org-id': 'abc' },
        }),
      }),
    };

    await expect(guard.canActivate(ctx as never)).rejects.toBeInstanceOf(
      ForbiddenException,
    );
  });
});