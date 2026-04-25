import { Controller, Get } from '@nestjs/common';
import { PlatformSettingsService } from './platform-settings.service';

@Controller('public/platform')
export class PublicPlatformController {
  constructor(private readonly platform: PlatformSettingsService) {}

  /** Used by clients to show maintenance countdown without an org code. */
  @Get('maintenance')
  async maintenance() {
    return this.platform.getEffectiveMaintenance();
  }
}
