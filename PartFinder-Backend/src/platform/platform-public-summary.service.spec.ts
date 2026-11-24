import { Test, TestingModule } from '@nestjs/testing';
import { getModelToken } from '@nestjs/mongoose';
import { PlatformPublicSummaryService } from './platform-public-summary.service';
import { Organization } from '../organizations/schemas/organization.schema';
import { AdminUser } from '../users/schemas/admin-user.schema';

describe('PlatformPublicSummaryService', () => {
  let service: PlatformPublicSummaryService;
  const orgModel = {
    countDocuments: jest.fn(),
  };
  const adminModel = {
    countDocuments: jest.fn(),
  };

  beforeEach(async () => {
    jest.clearAllMocks();
    const module: TestingModule = await Test.createTestingModule({
      providers: [
        PlatformPublicSummaryService,
        { provide: getModelToken(Organization.name), useValue: orgModel },
        { provide: getModelToken(AdminUser.name), useValue: adminModel },
      ],
    }).compile();

    service = module.get(PlatformPublicSummaryService);
  });

  it('aggregates organization and admin counts', async () => {
    orgModel.countDocuments
      .mockResolvedValueOnce(42)
      .mockResolvedValueOnce(38)
      .mockResolvedValueOnce(3);
    adminModel.countDocuments.mockResolvedValueOnce(5);

    const result = await service.getSummary();

    expect(result).toEqual({
      totalOrganizations: 42,
      activeOrganizations: 38,
      platformAdminCount: 5,
      organizationsCreatedToday: 3,
    });
  });
});