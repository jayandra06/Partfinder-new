import { Controller, Get, Headers, Param, UseGuards } from '@nestjs/common';
import { AuthGuard } from '@nestjs/passport';
import { ViewDataService } from './view-data.service';

@Controller('view-data')
@UseGuards(AuthGuard('jwt'))
export class ViewDataController {
  constructor(private readonly viewDataService: ViewDataService) {}

  @Get(':primaryTemplateId')
  async getEnrichedRows(
    @Headers('x-org-id') orgId: string,
    @Param('primaryTemplateId') primaryTemplateId: string,
  ): Promise<{ data: unknown; success: boolean; message: string }> {
    const data = await this.viewDataService.getEnrichedRows(orgId, primaryTemplateId);
    return { data, success: true, message: 'Enriched rows fetched' };
  }
}
