import { Body, Controller, Get, Patch, UseGuards } from '@nestjs/common';
import { AuthGuard } from '@nestjs/passport';
import { UpdateMaintenanceDto } from './dto/update-maintenance.dto';
import { PlatformSettingsService } from './platform-settings.service';

@Controller('admin/platform')
@UseGuards(AuthGuard('jwt'))
export class AdminPlatformController {
  constructor(private readonly platform: PlatformSettingsService) {}

  @Get('maintenance')
  getMaintenance() {
    return this.platform.getRawForAdmin();
  }

  @Patch('maintenance')
  patchMaintenance(@Body() dto: UpdateMaintenanceDto) {
    return this.platform.updateMaintenance(dto);
  }
}
