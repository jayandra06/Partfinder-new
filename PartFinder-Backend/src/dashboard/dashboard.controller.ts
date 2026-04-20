import { Controller, Get, Headers, UseGuards } from '@nestjs/common';
import { AuthGuard } from '@nestjs/passport';
import { DashboardService } from './dashboard.service';

@Controller('dashboard')
@UseGuards(AuthGuard('jwt'))
export class DashboardController {
  constructor(private readonly dashboardService: DashboardService) {}

  @Get('stats')
  async stats(@Headers('x-org-id') orgId: string) {
    const data = await this.dashboardService.stats(orgId);
    return { data, success: true, message: 'Dashboard stats fetched' };
  }

  @Get('trend')
  async trend(@Headers('x-org-id') orgId: string) {
    const data = await this.dashboardService.trend(orgId);
    return { data, success: true, message: 'Dashboard trend fetched' };
  }
}
