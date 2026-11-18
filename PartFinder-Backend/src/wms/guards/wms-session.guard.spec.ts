import {
  ForbiddenException,
  UnauthorizedException,
} from '@nestjs/common';
import { WmsSessionGuard } from './wms-session.guard';

function mockContext(user: object | undefined, headers: Record<string, string>) {
  return {
    switchToHttp: () => ({
      getRequest: () => ({ user, headers }),
    }),
  };
}

function makeGuard(overrides?: {
  license?: object;
  org?: object;
  admin?: boolean;
  appUser?: object | null;
  grants?: {
    warehouseIds: string[];
    ownerIds: string[];
    raw: Record<string, unknown> | null;
  };
}) {
  const orgs = {
    verifyLicense: jest.fn().mockResolvedValue(
      overrides?.license ?? {
        valid: true,
        wmsEnabled: true,
        message: null,
      },
    ),
    findByOrgCode: jest.fn().mockResolvedValue(
      overrides?.org ?? {
        name: 'Test Org',
        orgDatabaseUri: 'mongodb://127.0.0.1/tenant',
        wmsEnabled: true,
        maxWmsWarehouses: 10,
        maxStockOwners: 50,
      },
    ),
  };
  const tenant = {
    findOrgAdminByEmail: jest
      .fn()
      .mockResolvedValue(overrides?.admin === false ? null : { email: 'a@b.com' }),
    findOrgAppUserByEmail: jest.fn().mockResolvedValue(
      overrides?.appUser === null
        ? null
        : (overrides?.appUser ?? { email: 'a@b.com', role: 'Employee' }),
    ),
    getConnection: jest.fn().mockResolvedValue({
      db: {
        collection: () => ({
          findOne: jest.fn().mockResolvedValue(
            overrides?.grants ?? {
              warehouseIds: ['w1'],
              ownerIds: ['o1'],
              permissions: { receiving: true, qc: true, putaway: true, supervise: false },
            },
          ),
        }),
      },
    }),
  };
  return { guard: new WmsSessionGuard(orgs as never, tenant as never), orgs };
}

describe('WmsSessionGuard', () => {
  it('requires authentication', async () => {
    const { guard } = makeGuard();
    await expect(
      guard.canActivate(mockContext(undefined, { 'x-org-id': '884470' }) as never),
    ).rejects.toBeInstanceOf(UnauthorizedException);
  });

  it('rejects invalid org header format before tenant lookup', async () => {
    const { guard, orgs } = makeGuard();
    await expect(
      guard.canActivate(
        mockContext(
          { userId: 'a@b.com', email: 'a@b.com', orgCode: '884470' },
          { 'x-org-id': 'abc' },
        ) as never,
      ),
    ).rejects.toBeInstanceOf(ForbiddenException);
    expect(orgs.verifyLicense).not.toHaveBeenCalled();
  });

  it('rejects legacy JWT without orgCode', async () => {
    const { guard, orgs } = makeGuard();
    await expect(
      guard.canActivate(
        mockContext(
          { userId: 'a@b.com', email: 'a@b.com' },
          { 'x-org-id': '884470' },
        ) as never,
      ),
    ).rejects.toBeInstanceOf(ForbiddenException);
    expect(orgs.verifyLicense).not.toHaveBeenCalled();
  });

  it('rejects cross-tenant X-Org-Id', async () => {
    const { guard, orgs } = makeGuard();
    await expect(
      guard.canActivate(
        mockContext(
          { userId: 'a@b.com', email: 'a@b.com', orgCode: '111111' },
          { 'x-org-id': '222222' },
        ) as never,
      ),
    ).rejects.toBeInstanceOf(ForbiddenException);
    expect(orgs.verifyLicense).not.toHaveBeenCalled();
  });

  it('rejects expired or invalid licence', async () => {
    const { guard } = makeGuard({
      license: { valid: false, wmsEnabled: false, message: 'Subscription expired.' },
    });
    await expect(
      guard.canActivate(
        mockContext(
          { userId: 'a@b.com', email: 'a@b.com', orgCode: '884470' },
          { 'x-org-id': '884470' },
        ) as never,
      ),
    ).rejects.toThrow(/subscription|License|expired/i);
  });

  it('rejects WMS when product not entitled', async () => {
    const { guard } = makeGuard({
      license: { valid: true, wmsEnabled: false, message: null },
      org: {
        name: 'Test',
        orgDatabaseUri: 'mongodb://127.0.0.1/tenant',
        wmsEnabled: false,
      },
    });
    await expect(
      guard.canActivate(
        mockContext(
          { userId: 'a@b.com', email: 'a@b.com', orgCode: '884470' },
          { 'x-org-id': '884470' },
        ) as never,
      ),
    ).rejects.toThrow(/not enabled/i);
  });

  it('rejects employee with no operator grants', async () => {
    const { guard } = makeGuard({
      admin: false,
      grants: { warehouseIds: [], ownerIds: [] },
    });
    await expect(
      guard.canActivate(
        mockContext(
          { userId: 'emp@b.com', email: 'emp@b.com', orgCode: '884470' },
          { 'x-org-id': '884470' },
        ) as never,
      ),
    ).rejects.toThrow(/operator grants/i);
  });

  it('allows admin with matching JWT org', async () => {
    const { guard } = makeGuard();
    const ok = await guard.canActivate(
      mockContext(
        { userId: 'a@b.com', email: 'a@b.com', orgCode: '884470' },
        { 'x-org-id': '884470' },
      ) as never,
    );
    expect(ok).toBe(true);
  });
});