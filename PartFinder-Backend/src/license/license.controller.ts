import { Body, Controller, Post } from '@nestjs/common';
import { OrganizationsService } from '../organizations/organizations.service';
import { VerifyLicenseDto } from './verify-license.dto';

/** No JWT — used by PartFinder Windows client to treat org code as a license key. */
@Controller('public/license')
export class LicenseController {
  constructor(private readonly organizationsService: OrganizationsService) {}

  @Post('verify')
  verify(@Body() dto: VerifyLicenseDto) {
    return this.organizationsService.verifyLicense(dto.orgCode);
  }
}