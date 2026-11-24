import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import {
  Organization,
  OrganizationDocument,
} from '../organizations/schemas/organization.schema';
import { AdminUser, AdminUserDocument } from '../users/schemas/admin-user.schema';

export type PlatformPublicSummary = {
  totalOrganizations: number;
  activeOrganizations: number;
  platformAdminCount: number;
  organizationsCreatedToday: number;
};

@Injectable()
export class PlatformPublicSummaryService {
  constructor(
    @InjectModel(Organization.name)
    private readonly orgModel: Model<OrganizationDocument>,
    @InjectModel(AdminUser.name)
    private readonly adminModel: Model<AdminUserDocument>,
  ) {}

  async getSummary(): Promise<PlatformPublicSummary> {
    const startOfToday = new Date();
    startOfToday.setHours(0, 0, 0, 0);

    const [totalOrganizations, activeOrganizations, platformAdminCount, organizationsCreatedToday] =
      await Promise.all([
        this.orgModel.countDocuments(),
        this.orgModel.countDocuments({ status: 'Active' }),
        this.adminModel.countDocuments(),
        this.orgModel.countDocuments({ createdAt: { $gte: startOfToday } }),
      ]);

    return {
      totalOrganizations,
      activeOrganizations,
      platformAdminCount,
      organizationsCreatedToday,
    };
  }
}