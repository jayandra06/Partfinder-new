import { BadRequestException, ConflictException, UnauthorizedException } from '@nestjs/common';
import { createHmac } from 'crypto';
import { AuthService } from './auth.service';

const user = { id: 'u1', email: 'admin@example.com', passwordHash: 'h', totpEnabled: false, totpSecretBase32: undefined as string | undefined };

function setup(overrides: Record<string, unknown> = {}) {
  const users = {
    findByEmail: jest.fn().mockResolvedValue(null),
    findById: jest.fn().mockResolvedValue(null),
    validatePassword: jest.fn().mockResolvedValue(false),
    createAdmin: jest.fn().mockResolvedValue(user),
    countAdmins: jest.fn().mockResolvedValue(0),
    setPassword: jest.fn().mockResolvedValue(undefined),
    setTotpSecret: jest.fn().mockResolvedValue(undefined),
    clearTotp: jest.fn().mockResolvedValue(undefined),
    deleteAllAdmins: jest.fn().mockResolvedValue(undefined),
    ...overrides,
  };
  const jwt = { signAsync: jest.fn().mockResolvedValue('signed-token') };
  const events = { emit: jest.fn() };
  const service = new AuthService(users as any, jwt as any, events as any);
  return { service, users, jwt, events };
}

/** RFC 6238 TOTP for the current 30s step (independent oracle). */
function currentTotp(secretBase32: string): string {
  const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
  let bits = '';
  for (const c of secretBase32) bits += alphabet.indexOf(c).toString(2).padStart(5, '0');
  const key = Buffer.from(bits.match(/.{8}/g)!.map((b) => parseInt(b, 2)));
  const counter = Buffer.alloc(8);
  counter.writeBigUInt64BE(BigInt(Math.floor(Date.now() / 1000 / 30)));
  const hash = createHmac('sha1', key).update(counter).digest();
  const offset = hash[hash.length - 1] & 0x0f;
  const code = (hash.readUInt32BE(offset) & 0x7fffffff) % 1_000_000;
  return code.toString().padStart(6, '0');
}

describe('AuthService', () => {
  describe('login', () => {
    it('rejects unknown emails', async () => {
      const { service } = setup();
      await expect(service.login({ email: 'x@y.z', password: 'p' } as any)).rejects.toBeInstanceOf(UnauthorizedException);
    });

    it('rejects a wrong password with the same message', async () => {
      const { service } = setup({ findByEmail: jest.fn().mockResolvedValue(user) });
      await expect(service.login({ email: user.email, password: 'bad' } as any)).rejects.toThrow('Invalid email or password');
    });

    it('returns a signed token for valid credentials', async () => {
      const { service, jwt } = setup({
        findByEmail: jest.fn().mockResolvedValue(user),
        validatePassword: jest.fn().mockResolvedValue(true),
      });

      const result = await service.login({ email: user.email, password: 'Good1234' } as any);

      expect(jwt.signAsync).toHaveBeenCalledWith({ sub: 'u1', email: user.email });
      expect(result).toEqual({ accessToken: 'signed-token', user: { id: 'u1', email: user.email } });
    });
  });

  describe('registerAdmin', () => {
    it('rejects duplicate emails', async () => {
      const { service } = setup({ findByEmail: jest.fn().mockResolvedValue(user) });
      await expect(service.registerAdmin({ email: user.email, password: 'Strong123' } as any)).rejects.toBeInstanceOf(ConflictException);
    });

    it.each(['short1A', 'alllowercase1', 'ALLUPPERCASE1', 'NoDigitsHere'])('rejects weak password %s', async (password) => {
      const { service, users } = setup();
      await expect(service.registerAdmin({ email: 'n@e.w', password } as any)).rejects.toBeInstanceOf(BadRequestException);
      expect(users.createAdmin).not.toHaveBeenCalled();
    });

    it('creates the admin for a strong password', async () => {
      const { service, users } = setup();

      const result = await service.registerAdmin({ email: 'n@e.w', password: 'Strong123' } as any);

      expect(users.createAdmin).toHaveBeenCalledWith('n@e.w', 'Strong123');
      expect(result.ok).toBe(true);
      expect(result.accessToken).toBe('signed-token');
    });

    it('rejects an invalid gate password when one is supplied', async () => {
      const { service } = setup();
      await expect(
        service.registerAdmin({ email: 'n@e.w', password: 'Strong123', gatePassword: 'nope' } as any),
      ).rejects.toBeInstanceOf(UnauthorizedException);
    });
  });

  describe('bootstrapFirstAdmin', () => {
    it('only works when there are no admins yet', async () => {
      const { service } = setup({ countAdmins: jest.fn().mockResolvedValue(1) });
      await expect(service.bootstrapFirstAdmin({ email: 'a@b.c', password: 'x' } as any)).rejects.toBeInstanceOf(ConflictException);
    });

    it('creates the first admin and signs a token', async () => {
      const { service, users } = setup();
      const result = await service.bootstrapFirstAdmin({ email: 'a@b.c', password: 'x' } as any);
      expect(users.createAdmin).toHaveBeenCalledWith('a@b.c', 'x');
      expect(result.accessToken).toBe('signed-token');
    });
  });

  describe('changeAdminPassword', () => {
    it('requires an existing user', async () => {
      const { service } = setup();
      await expect(service.changeAdminPassword('u1', { currentPassword: 'a', newPassword: 'b' } as any)).rejects.toBeInstanceOf(UnauthorizedException);
    });

    it('requires the current password to match', async () => {
      const { service } = setup({ findById: jest.fn().mockResolvedValue(user) });
      await expect(service.changeAdminPassword('u1', { currentPassword: 'a', newPassword: 'b' } as any)).rejects.toThrow('Current password is incorrect');
    });

    it('updates the password and emits an event', async () => {
      const { service, users, events } = setup({
        findById: jest.fn().mockResolvedValue(user),
        validatePassword: jest.fn().mockResolvedValue(true),
      });

      await expect(
        service.changeAdminPassword('u1', { currentPassword: 'old', newPassword: 'New12345', email: 't@x.y' } as any),
      ).resolves.toEqual({ ok: true });
      expect(users.setPassword).toHaveBeenCalledWith('u1', 'New12345');
      expect(events.emit).toHaveBeenCalledWith('admin.password.changed', expect.objectContaining({ email: user.email, targetEmail: 't@x.y' }));
    });
  });

  it('createAdminUser rejects duplicates and creates new users', async () => {
    const dup = setup({ findByEmail: jest.fn().mockResolvedValue(user) });
    await expect(dup.service.createAdminUser({ email: user.email, password: 'p' } as any)).rejects.toBeInstanceOf(ConflictException);

    const ok = setup();
    await expect(ok.service.createAdminUser({ email: 'n@e.w', password: 'p' } as any)).resolves.toEqual({ user: { id: 'u1', email: user.email } });
  });

  it('syncAdminTotp validates the secret before saving', async () => {
    const { service, users } = setup();
    await expect(service.syncAdminTotp('u1', { secretBase32: '!!' } as any)).rejects.toBeInstanceOf(BadRequestException);
    expect(users.setTotpSecret).not.toHaveBeenCalled();

    await expect(service.syncAdminTotp('u1', { secretBase32: ' JBSWY3DPEHPK3PXP ' } as any)).resolves.toEqual({ ok: true });
    expect(users.setTotpSecret).toHaveBeenCalledWith('u1', 'JBSWY3DPEHPK3PXP');
  });

  it('clearAdminTotp delegates to the users service', async () => {
    const { service, users } = setup();
    await expect(service.clearAdminTotp('u1')).resolves.toEqual({ ok: true });
    expect(users.clearTotp).toHaveBeenCalledWith('u1');
  });

  describe('resetPasswordWithTotp', () => {
    const secret = 'JBSWY3DPEHPK3PXP';

    it('fails when TOTP is not enabled', async () => {
      const { service } = setup({ findByEmail: jest.fn().mockResolvedValue(user) });
      await expect(
        service.resetPasswordWithTotp({ email: user.email, totpCode: '000000', newPassword: 'Strong123' } as any),
      ).rejects.toThrow('Recovery failed');
    });

    it('fails with a wrong code', async () => {
      const totpUser = { ...user, totpEnabled: true, totpSecretBase32: secret };
      const { service, users } = setup({ findByEmail: jest.fn().mockResolvedValue(totpUser) });
      const wrong = currentTotp(secret) === '000000' ? '111111' : '000000';
      await expect(
        service.resetPasswordWithTotp({ email: user.email, totpCode: wrong, newPassword: 'Strong123' } as any),
      ).rejects.toBeInstanceOf(UnauthorizedException);
      expect(users.setPassword).not.toHaveBeenCalled();
    });

    it('resets the password with a valid code and strong password', async () => {
      const totpUser = { ...user, totpEnabled: true, totpSecretBase32: secret };
      const { service, users } = setup({ findByEmail: jest.fn().mockResolvedValue(totpUser) });

      await expect(
        service.resetPasswordWithTotp({ email: user.email, totpCode: currentTotp(secret), newPassword: 'Strong123' } as any),
      ).resolves.toEqual({ ok: true });
      expect(users.setPassword).toHaveBeenCalledWith('u1', 'Strong123');
    });

    it('still enforces password strength after a valid code', async () => {
      const totpUser = { ...user, totpEnabled: true, totpSecretBase32: secret };
      const { service } = setup({ findByEmail: jest.fn().mockResolvedValue(totpUser) });
      await expect(
        service.resetPasswordWithTotp({ email: user.email, totpCode: currentTotp(secret), newPassword: 'weak' } as any),
      ).rejects.toBeInstanceOf(BadRequestException);
    });
  });
});
