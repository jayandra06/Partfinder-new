import { Body, Controller, Delete, Get, Headers, Param, ParseIntPipe, Patch, Post, Query, UseGuards } from '@nestjs/common';
import { AuthGuard } from '@nestjs/passport';
import { CreatePartDto, UpdatePartDto } from './dto/part.dto';
import { PartsService } from './parts.service';

@Controller('parts')
@UseGuards(AuthGuard('jwt'))
export class PartsController {
  constructor(private readonly partsService: PartsService) {}

  @Get()
  async list(
    @Headers('x-org-id') orgId: string,
    @Query('templateId') templateId?: string,
    @Query('search') search?: string,
    @Query('page', new ParseIntPipe({ optional: true })) page = 1,
    @Query('limit', new ParseIntPipe({ optional: true })) limit = 100,
  ) {
    const data = await this.partsService.list(orgId, templateId, search, page, limit);
    return { data, success: true, message: 'Parts fetched' };
  }

  @Post()
  async create(@Headers('x-org-id') orgId: string, @Body() dto: CreatePartDto) {
    const data = await this.partsService.create(orgId, dto);
    return { data, success: true, message: 'Part created' };
  }

  @Get('low-stock')
  async lowStock(@Headers('x-org-id') orgId: string) {
    const data = await this.partsService.lowStock(orgId);
    return { data, success: true, message: 'Low stock parts fetched' };
  }

  @Get(':id')
  async getById(@Headers('x-org-id') orgId: string, @Param('id') id: string) {
    const data = await this.partsService.getById(orgId, id);
    return { data, success: true, message: 'Part fetched' };
  }

  @Patch(':id')
  async update(@Headers('x-org-id') orgId: string, @Param('id') id: string, @Body() dto: UpdatePartDto) {
    const data = await this.partsService.update(orgId, id, dto);
    return { data, success: true, message: 'Part updated' };
  }

  @Delete(':id')
  async remove(@Headers('x-org-id') orgId: string, @Param('id') id: string) {
    const data = await this.partsService.remove(orgId, id);
    return { data, success: true, message: 'Part deleted' };
  }
}
