import { Controller, Get } from '@nestjs/common';
import { PlatformPublicSummaryService } from './platform-public-summary.service';
import { PlatformSettingsService } from './platform-settings.service';

@Controller('public/platform')
export class PublicPlatformController {
  constructor(
    private readonly platform: PlatformSettingsService,
    private readonly summary: PlatformPublicSummaryService,
  ) {}

  /** Used by clients to show maintenance countdown without an org code. */
  @Get('maintenance')
  async maintenance() {
    return this.platform.getEffectiveMaintenance();
  }

  /** Aggregate, non-identifying counts for the public admin landing dashboard. */
  @Get('summary')
  async publicSummary() {
    const data = await this.summary.getSummary();
    return { success: true, data };
  }
}
