const enforcer = {
  loadPolicy: jest.fn(),
  removeFilteredPolicy: jest.fn(),
  removeFilteredGroupingPolicy: jest.fn(),
  addGroupingPolicy: jest.fn(),
  addPolicy: jest.fn(),
  savePolicy: jest.fn(),
  enforce: jest.fn(),
};

jest.mock('casbin', () => ({ newEnforcer: jest.fn(async () => enforcer) }));
jest.mock('casbin-mongodb-adapter', () => ({ MongoAdapter: { newAdapter: jest.fn(async () => ({})) } }));

import { MongoAdapter } from 'casbin-mongodb-adapter';
import { CasbinService } from './casbin.service';

describe('CasbinService', () => {
  beforeEach(() => jest.clearAllMocks());

  it('uses the database name from the URI path', async () => {
    await new CasbinService().getEnforcer('mongodb://localhost:27017/tenant_a');

    expect(MongoAdapter.newAdapter).toHaveBeenCalledWith({ uri: 'mongodb://localhost:27017/tenant_a', database: 'tenant_a', collection: 'casbin_rule' });
    expect(enforcer.loadPolicy).toHaveBeenCalled();
  });

  it('defaults the database name when the URI has no path', async () => {
    await new CasbinService().getEnforcer('mongodb://localhost:27017');

    expect(MongoAdapter.newAdapter).toHaveBeenCalledWith(expect.objectContaining({ database: 'partfinder' }));
  });

  it('syncUserPermissions replaces the user policies with only granted actions', async () => {
    await new CasbinService().syncUserPermissions(
      'mongodb://h/db',
      'e@x.com',
      'employee',
      { view: true, edit: false, add: true },
      { copy: true, delete: false },
    );

    expect(enforcer.removeFilteredPolicy).toHaveBeenCalledWith(0, 'e@x.com');
    expect(enforcer.removeFilteredGroupingPolicy).toHaveBeenCalledWith(0, 'e@x.com');
    expect(enforcer.addGroupingPolicy).toHaveBeenCalledWith('e@x.com', 'employee');
    expect(enforcer.addPolicy.mock.calls).toEqual([
      ['e@x.com', 'templates', 'add'],
      ['e@x.com', 'templates', 'view'],
      ['e@x.com', 'masterdata', 'copy'],
    ]);
    expect(enforcer.savePolicy).toHaveBeenCalled();
  });

  it('syncUserPermissions with no granular permissions only assigns the role', async () => {
    await new CasbinService().syncUserPermissions('mongodb://h/db', 'a@x.com', 'admin');

    expect(enforcer.addGroupingPolicy).toHaveBeenCalledWith('a@x.com', 'admin');
    expect(enforcer.addPolicy).not.toHaveBeenCalled();
  });

  it('checkPermission returns the enforcer decision', async () => {
    enforcer.enforce.mockResolvedValueOnce(true).mockResolvedValueOnce(false);
    const svc = new CasbinService();

    await expect(svc.checkPermission('mongodb://h/db', 'e', 'templates', 'view')).resolves.toBe(true);
    await expect(svc.checkPermission('mongodb://h/db', 'e', 'templates', 'delete')).resolves.toBe(false);
    expect(enforcer.enforce).toHaveBeenLastCalledWith('e', 'templates', 'delete');
  });
});
