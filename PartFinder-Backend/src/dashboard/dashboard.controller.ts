import { Controller, Get, Headers, UseGuards } from '@nestjs/common';
import { AuthGuard } from '@nestjs/passport';
import { DashboardService } from './dashboard.service';

@Controller('dashboard')
@UseGuards(AuthGuard('jwt'))
export class DashboardController {
  constructor(private readonly dashboardService: DashboardService) {}

  @Get('stats')
  async stats(@Headers('x-org-id') orgId: string) {
    if (!orgId) {
      console.warn('[DashboardController.stats] Missing x-org-id header');
    }
    const data = await this.dashboardService.stats(orgId);
    return { data, success: true, message: 'Dashboard stats fetched' };
  }

  @Get('debug/template-count')
  async debugTemplateCount(@Headers('x-org-id') orgId: string) {
    if (!orgId) {
      console.warn('[DashboardController.debug] Missing x-org-id header');
    }
    const count = await this.dashboardService.debugGetTemplateCount(orgId);
    return {
      data: { orgId, templateCount: count },
      success: true,
      message: 'Template count (debug)',
    };
  }

  @Get('trend')
  async trend(@Headers('x-org-id') orgId: string) {
    if (!orgId) {
      console.warn('[DashboardController.trend] Missing x-org-id header');
    }
    const data = await this.dashboardService.trend(orgId);
    return { data, success: true, message: 'Dashboard trend fetched' };
  }
}
