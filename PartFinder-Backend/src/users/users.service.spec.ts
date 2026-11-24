import * as bcrypt from 'bcrypt';
import { UsersService } from './users.service';

type Doc = { _id: string; email: string; passwordHash: string; save: jest.Mock };

function exec<T>(value: T) {
  return { exec: jest.fn().mockResolvedValue(value) };
}

function makeModel() {
  const saved: Doc[] = [];
  // Mongoose models are constructors; emulate `new Model(data).save()`.
  const model: any = jest.fn().mockImplementation((data: { email: string; passwordHash: string }) => {
    const doc: Doc = {
      _id: `id-${saved.length + 1}`,
      ...data,
      save: jest.fn().mockImplementation(async () => {
        saved.push(doc);
        return doc;
      }),
    };
    return doc;
  });
  model.countDocuments = jest.fn().mockReturnValue(exec(3));
  model.findOne = jest.fn().mockReturnValue(exec(null));
  model.findById = jest.fn().mockReturnValue(exec(null));
  model.findByIdAndUpdate = jest.fn().mockReturnValue(exec({}));
  model.deleteMany = jest.fn().mockReturnValue(exec({}));
  return { model, saved };
}

describe('UsersService', () => {
  beforeEach(() => {
    jest.spyOn(console, 'log').mockImplementation(() => undefined);
    jest.spyOn(console, 'warn').mockImplementation(() => undefined);
  });

  afterEach(() => jest.restoreAllMocks());

  it('normalises email when looking users up', async () => {
    const { model } = makeModel();
    const service = new UsersService(model);

    await service.findByEmail('  Admin@Example.COM ');

    expect(model.findOne).toHaveBeenCalledWith({ email: 'admin@example.com' });
  });

  it('stores a bcrypt hash, never the plain password', async () => {
    const { model, saved } = makeModel();
    const service = new UsersService(model);

    const user = await service.createAdmin(' New@Example.com ', 'Secret123');

    expect(saved).toHaveLength(1);
    expect(user.email).toBe('new@example.com');
    expect(user.passwordHash).not.toBe('Secret123');
    await expect(bcrypt.compare('Secret123', user.passwordHash)).resolves.toBe(true);
  });

  it('validatePassword checks against the stored hash', async () => {
    const { model } = makeModel();
    const service = new UsersService(model);
    const passwordHash = await bcrypt.hash('Right123', 4);
    const user = { passwordHash } as any;

    await expect(service.validatePassword(user, 'Right123')).resolves.toBe(true);
    await expect(service.validatePassword(user, 'wrong')).resolves.toBe(false);
  });

  it('setPassword writes a fresh hash for the user', async () => {
    const { model } = makeModel();
    const service = new UsersService(model);

    await service.setPassword('u1', 'Another123');

    const [id, update] = model.findByIdAndUpdate.mock.calls[0];
    expect(id).toBe('u1');
    await expect(bcrypt.compare('Another123', update.passwordHash)).resolves.toBe(true);
  });

  it('setTotpSecret trims and enables TOTP', async () => {
    const { model } = makeModel();
    const service = new UsersService(model);

    await service.setTotpSecret('u1', '  ABCDEF  ');

    expect(model.findByIdAndUpdate).toHaveBeenCalledWith('u1', {
      totpSecretBase32: 'ABCDEF',
      totpEnabled: true,
    });
  });

  it('clearTotp disables TOTP and unsets the secret', async () => {
    const { model } = makeModel();
    const service = new UsersService(model);

    await service.clearTotp('u1');

    expect(model.findByIdAndUpdate).toHaveBeenCalledWith('u1', {
      $set: { totpEnabled: false },
      $unset: { totpSecretBase32: 1 },
    });
  });

  it('countAdmins returns the model count', async () => {
    const { model } = makeModel();

    await expect(new UsersService(model).countAdmins()).resolves.toBe(3);
  });

  it('onModuleInit seeds missing default admins', async () => {
    const { model, saved } = makeModel();
    const service = new UsersService(model);

    await service.onModuleInit();

    expect(saved.length).toBeGreaterThan(0);
    expect(saved.every((d) => d.email === d.email.toLowerCase())).toBe(true);
  });

  it('onModuleInit never throws when the database fails', async () => {
    const { model } = makeModel();
    model.findOne = jest.fn().mockReturnValue({ exec: jest.fn().mockRejectedValue(new Error('down')) });

    await expect(new UsersService(model).onModuleInit()).resolves.toBeUndefined();
  });
});
