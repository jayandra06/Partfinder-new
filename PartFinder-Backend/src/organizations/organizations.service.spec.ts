import { BadRequestException, ConflictException, NotFoundException } from '@nestjs/common';
import { Types } from 'mongoose';
import * as nodemailer from 'nodemailer';
import { OrganizationsService } from './organizations.service';

jest.mock('nodemailer', () => ({ createTransport: jest.fn() }));
const createTransport = nodemailer.createTransport as jest.Mock;

const DAY = 24 * 60 * 60 * 1000;

/** In-memory stand-in for the mongoose Organization model (constructor + statics). */
function makeOrgModel() {
  const store: any[] = [];
  class FakeOrg {
    [k: string]: any;
    constructor(data: Record<string, unknown>) {
      Object.assign(this, { _id: new Types.ObjectId(), createdAt: new Date() }, data);
    }
    async save() {
      if (!store.includes(this)) store.push(this);
      return this;
    }
    toJSON() {
      const { save, toJSON, ...rest } = this as any;
      return { ...rest };
    }
    static store = store;
    static find = jest.fn(() => ({ sort: () => ({ exec: async () => [...store].reverse() }) }));
    static findOne = jest.fn((f: any) => ({ exec: async () => store.find((o) => o.orgCode === f.orgCode) ?? null }));
    static findById = jest.fn((id: string) => ({ exec: async () => store.find((o) => String(o._id) === String(id)) ?? null }));
    static findByIdAndDelete = jest.fn((id: string) => ({
      exec: async () => {
        const i = store.findIndex((o) => String(o._id) === String(id));
        return i >= 0 ? store.splice(i, 1)[0] : null;
      },
    }));
  }
  return FakeOrg;
}

function setup(env: Record<string, string> = {}, maintenance: any = { active: false, maintenanceUntil: null }) {
  const Org = makeOrgModel();
  const platform = { getEffectiveMaintenance: jest.fn().mockResolvedValue(maintenance) };
  const config = { get: jest.fn((k: string) => env[k]) };
  const service = new OrganizationsService(Org as any, platform as any, config as any);
  return { service, Org };
}

async function seed(Org: any, data: Record<string, unknown>) {
  const o = new Org({ orgCode: '123456', name: 'Acme', type: 'standard', plan: 'starter', status: 'Active', validity: new Date(Date.now() + 30 * DAY), ...data });
  await o.save();
  return o;
}

describe('OrganizationsService', () => {
  beforeEach(() => createTransport.mockReset());

  describe('verifyLicense', () => {
    it('blocks every code during platform maintenance', async () => {
      const { service } = setup({}, { active: true, maintenanceUntil: '2026-12-01T00:00:00Z' });

      await expect(service.verifyLicense('123456')).resolves.toMatchObject({ valid: false, reason: 'PLATFORM_MAINTENANCE', maintenanceUntil: '2026-12-01T00:00:00Z' });
    });

    it.each(['12345', '1234567', 'abcdef', ''])('rejects badly formatted code %p', async (code) => {
      const { service } = setup();
      await expect(service.verifyLicense(code)).resolves.toMatchObject({ valid: false, reason: 'INVALID_FORMAT' });
    });

    it('reports unknown and suspended organisations', async () => {
      const { service, Org } = setup();
      await expect(service.verifyLicense('999999')).resolves.toMatchObject({ reason: 'UNKNOWN_ORG' });
      await seed(Org, { status: 'Suspended' });
      await expect(service.verifyLicense(' 123456 ')).resolves.toMatchObject({ reason: 'SUSPENDED', orgCode: '123456' });
    });

    it('reports permanent and active temporary bans, but ignores expired bans', async () => {
      const { service, Org } = setup();
      const org = await seed(Org, { licensePermanentlyBanned: true });
      await expect(service.verifyLicense('123456')).resolves.toMatchObject({ reason: 'LICENSE_BANNED_PERMANENT' });

      org.licensePermanentlyBanned = false;
      const until = new Date(Date.now() + DAY);
      org.licenseBannedUntil = until.toISOString();
      await expect(service.verifyLicense('123456')).resolves.toMatchObject({ reason: 'LICENSE_BANNED_TEMPORARY', bannedUntil: until.toISOString() });

      org.licenseBannedUntil = new Date(Date.now() - 1000);
      await expect(service.verifyLicense('123456')).resolves.toMatchObject({ valid: true });
    });

    it('treats validity as inclusive through the end of the UTC day', async () => {
      const { service, Org } = setup();
      const org = await seed(Org, { validity: new Date() });
      await expect(service.verifyLicense('123456')).resolves.toMatchObject({ valid: true });

      org.validity = new Date(Date.now() - 2 * DAY);
      await expect(service.verifyLicense('123456')).resolves.toMatchObject({ valid: false, reason: 'SUBSCRIPTION_EXPIRED' });
    });

    it('returns limits with defaults for a valid licence', async () => {
      const { service, Org } = setup();
      await seed(Org, { maxUsers: 5 });

      const r = await service.verifyLicense('123456');
      expect(r).toMatchObject({ valid: true, organizationName: 'Acme', maxUsers: 5, maxParts: 100000, wmsEnabled: true, maxWmsWarehouses: 10, maxStockOwners: 50 });
    });
  });

  describe('create / createSelfServeLicense', () => {
    it('creates an active organisation with a verifiable temporary password hash', async () => {
      const { service, Org } = setup();

      const list = await service.create({ orgCode: ' 654321 ', name: ' Beta ', type: 'standard', plan: 'demo', firstAdminEmail: ' Admin@Beta.COM ' } as any);

      expect(list).toHaveLength(1);
      expect(list[0]).toMatchObject({ orgCode: '654321', name: 'Beta', status: 'Active', licensePermanentlyBanned: false, maxUsers: 50 });
      const stored = Org.store[0];
      expect(stored.firstAdminEmailNormalized).toBe('admin@beta.com');
      expect(stored.firstAdminTemporaryPasswordHash).toMatch(/^pbkdf2\$120000\$/);
      expect(createTransport).not.toHaveBeenCalled(); // SMTP not configured
    });

    it('rejects duplicate org codes', async () => {
      const { service, Org } = setup();
      await seed(Org, {});

      await expect(service.create({ orgCode: '123456', name: 'X', type: 't', plan: 'demo', firstAdminEmail: 'a@b.c' } as any)).rejects.toBeInstanceOf(ConflictException);
    });

    it('self-serve allocates a 6-digit code, returns the temporary password and emails it', async () => {
      const sendMail = jest.fn().mockResolvedValue({});
      createTransport.mockReturnValue({ sendMail });
      const { service, Org } = setup({ EMAIL_HOST: 'smtp.test', EMAIL_USER: 'u', EMAIL_PASS: 'p', SMTP_FROM: 'noreply@test', INVITE_SMTP_PORT: '465' });

      const r = await service.createSelfServeLicense({ name: ' Gamma ', firstAdminEmail: ' Boss@Gamma.io ' });

      expect(r.orgCode).toMatch(/^\d{6}$/);
      expect(r).toMatchObject({ email: 'boss@gamma.io', organisationName: 'Gamma' });
      expect(Org.store[0]).toMatchObject({ plan: 'starter', type: 'standard' });
      expect(service.verifyTemporaryPassword(r.temporaryPassword, Org.store[0].firstAdminTemporaryPasswordHash)).toBe(true);
      expect(createTransport).toHaveBeenCalledWith({ host: 'smtp.test', port: 465, secure: true, auth: { user: 'u', pass: 'p' } });
      const mail = sendMail.mock.calls[0][0];
      expect(mail).toMatchObject({ from: '"PartFinder" <noreply@test>', to: 'boss@gamma.io', subject: `PartFinder admin invite - Org ${r.orgCode}` });
      expect(mail.text).toContain(`Temporary Password: ${r.temporaryPassword}`);
    });

    it('still succeeds when the invite email fails', async () => {
      createTransport.mockReturnValue({ sendMail: jest.fn().mockRejectedValue(new Error('smtp down')) });
      const { service } = setup({ EMAIL_HOST: 'h', EMAIL_USER: 'u', EMAIL_PASS: 'p', SMTP_FROM: 'f@x' });

      await expect(service.createSelfServeLicense({ name: 'D', firstAdminEmail: 'd@x' })).resolves.toMatchObject({ email: 'd@x' });
    });
  });

  describe('verifyTemporaryPassword', () => {
    it.each([
      ['', 'empty'],
      ['bcrypt$1$2$3', 'wrong scheme'],
      ['pbkdf2$100$c2FsdA==$aGFzaA==', 'too few iterations'],
      ['pbkdf2$abc$c2FsdA==$aGFzaA==', 'non-numeric iterations'],
      ['pbkdf2$120000$c2FsdA==', 'missing part'],
    ])('rejects %p (%s)', (stored) => {
      expect(setup().service.verifyTemporaryPassword('pw', stored)).toBe(false);
    });
  });

  describe('update / ban / reactivate / remove', () => {
    it('validates ids and existence', async () => {
      const { service } = setup();
      const missing = new Types.ObjectId().toString();
      for (const call of [
        (id: string) => service.update(id, {} as any),
        (id: string) => service.banLicense(id, { permanent: true } as any),
        (id: string) => service.reactivateLicense(id),
        (id: string) => service.remove(id),
      ]) {
        await expect(call('nope')).rejects.toBeInstanceOf(BadRequestException);
        await expect(call(missing)).rejects.toBeInstanceOf(NotFoundException);
      }
    });

    it('updates fields and recomputes validity when the plan changes', async () => {
      const { service, Org } = setup();
      const org = await seed(Org, { validity: new Date('2000-01-01') });

      const [row] = await service.update(String(org._id), { name: ' Renamed ', plan: 'lifetime', status: 'Suspended' } as any);

      expect(row).toMatchObject({ name: 'Renamed', plan: 'lifetime', status: 'Suspended' });
      expect(new Date(row.validity).getUTCFullYear()).toBeGreaterThan(2100);
    });

    it('applies temporary and permanent bans and reactivation', async () => {
      const { service, Org } = setup();
      const org = await seed(Org, {});
      const id = String(org._id);

      await expect(service.banLicense(id, {} as any)).rejects.toBeInstanceOf(BadRequestException);

      const before = Date.now();
      const [temp] = await service.banLicense(id, { days: 1, hours: 2, minutes: 3 } as any);
      const expectedMs = ((1 * 24 + 2) * 60 + 3) * 60 * 1000;
      const until = new Date(temp.licenseBannedUntil as string).getTime();
      expect(until - before).toBeGreaterThanOrEqual(expectedMs);
      expect(until - before).toBeLessThan(expectedMs + 5000);

      const [perm] = await service.banLicense(id, { permanent: true, days: 3 } as any);
      expect(perm).toMatchObject({ licensePermanentlyBanned: true, licenseBannedUntil: null });

      const [ok] = await service.reactivateLicense(id);
      expect(ok).toMatchObject({ licensePermanentlyBanned: false, licenseBannedUntil: null });
    });

    it('removes an organisation and returns the remaining list', async () => {
      const { service, Org } = setup();
      const a = await seed(Org, { orgCode: '111111' });
      await seed(Org, { orgCode: '222222' });

      const list = await service.remove(String(a._id));

      expect(list.map((o) => o.orgCode)).toEqual(['222222']);
      await expect(service.findByOrgCode(' 222222 ')).resolves.toMatchObject({ orgCode: '222222' });
    });
  });
});
