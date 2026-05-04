import { Injectable } from '@nestjs/common';
import { newEnforcer, Enforcer } from 'casbin';
import { MongoAdapter } from 'casbin-mongodb-adapter';
import { join } from 'path';

@Injectable()
export class CasbinService {
  private readonly modelPath = join(process.cwd(), 'src/casbin/rbac_model.conf');

  async getEnforcer(uri: string): Promise<Enforcer> {
    // Extract database name from URI (e.g. mongodb://host:port/dbname)
    const url = new URL(uri);
    const databaseName = url.pathname.split('/').pop() || 'partfinder';

    const adapter = await MongoAdapter.newAdapter({
      uri: uri,
      database: databaseName,
      collection: 'casbin_rule',
    });
    const enforcer = await newEnforcer(this.modelPath, adapter);
    await enforcer.loadPolicy();
    return enforcer;
  }

  async syncUserPermissions(
    uri: string,
    email: string,
    role: string,
    templatePermissions?: { add?: boolean; view?: boolean; edit?: boolean; delete?: boolean },
    masterDataPermissions?: { view?: boolean; edit?: boolean; add?: boolean; delete?: boolean; copy?: boolean }
  ) {
    const enforcer = await this.getEnforcer(uri);
    
    // Clear existing permissions for this user in Casbin
    // p, sub, obj, act
    await enforcer.removeFilteredPolicy(0, email);
    await enforcer.removeFilteredGroupingPolicy(0, email);

    // Add role
    await enforcer.addGroupingPolicy(email, role);

    // Add granular template permissions
    if (templatePermissions) {
      if (templatePermissions.add) await enforcer.addPolicy(email, 'templates', 'add');
      if (templatePermissions.view) await enforcer.addPolicy(email, 'templates', 'view');
      if (templatePermissions.edit) await enforcer.addPolicy(email, 'templates', 'edit');
      if (templatePermissions.delete) await enforcer.addPolicy(email, 'templates', 'delete');
    }

    // Add granular master data permissions
    if (masterDataPermissions) {
      if (masterDataPermissions.view) await enforcer.addPolicy(email, 'masterdata', 'view');
      if (masterDataPermissions.edit) await enforcer.addPolicy(email, 'masterdata', 'edit');
      if (masterDataPermissions.add) await enforcer.addPolicy(email, 'masterdata', 'add');
      if (masterDataPermissions.delete) await enforcer.addPolicy(email, 'masterdata', 'delete');
      if (masterDataPermissions.copy) await enforcer.addPolicy(email, 'masterdata', 'copy');
    }

    await enforcer.savePolicy();
  }

  async checkPermission(uri: string, sub: string, obj: string, act: string): Promise<boolean> {
    const enforcer = await this.getEnforcer(uri);
    return enforcer.enforce(sub, obj, act);
  }
}
