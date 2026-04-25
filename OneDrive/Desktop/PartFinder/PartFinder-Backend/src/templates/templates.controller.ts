import { Body, Controller, Delete, Get, Headers, Param, ParseIntPipe, Patch, Post, Query, UseGuards } from '@nestjs/common';
import { AuthGuard } from '@nestjs/passport';
import { CreateColumnDto } from './dto/create-column.dto';
import { CreateRowDto, UpdateRowDto } from './dto/create-row.dto';
import { CreateTemplateDto } from './dto/create-template.dto';
import { UpdateColumnDto } from './dto/update-column.dto';
import { TemplatesService } from './templates.service';

@Controller('templates')
@UseGuards(AuthGuard('jwt'))
export class TemplatesController {
  constructor(private readonly templatesService: TemplatesService) {}

  private ok(data: unknown, message: string): { data: unknown; success: boolean; message: string } {
    return { data, success: true, message };
  }

  @Post()
  async create(@Headers('x-org-id') orgId: string, @Body() dto: CreateTemplateDto): Promise<{ data: unknown; success: boolean; message: string }> {
    const data = await this.templatesService.create(orgId, dto);
    return this.ok(data, 'Template created');
  }

  @Get()
  async list(@Headers('x-org-id') orgId: string): Promise<{ data: unknown; success: boolean; message: string }> {
    const data = await this.templatesService.list(orgId);
    return this.ok(data, 'Templates fetched');
  }

  @Get(':id')
  async getOne(@Headers('x-org-id') orgId: string, @Param('id') id: string): Promise<{ data: unknown; success: boolean; message: string }> {
    const data = await this.templatesService.getOne(orgId, id);
    return this.ok(data, 'Template fetched');
  }

  @Patch(':id')
  async rename(@Headers('x-org-id') orgId: string, @Param('id') id: string, @Body() dto: CreateTemplateDto): Promise<{ data: unknown; success: boolean; message: string }> {
    const data = await this.templatesService.rename(orgId, id, dto);
    return this.ok(data, 'Template updated');
  }

  @Delete(':id')
  async remove(@Headers('x-org-id') orgId: string, @Param('id') id: string): Promise<{ data: unknown; success: boolean; message: string }> {
    const data = await this.templatesService.delete(orgId, id);
    return this.ok(data, 'Template deleted');
  }

  @Post(':id/columns')
  async addColumn(@Headers('x-org-id') orgId: string, @Param('id') id: string, @Body() dto: CreateColumnDto): Promise<{ data: unknown; success: boolean; message: string }> {
    const data = await this.templatesService.addColumn(orgId, id, dto);
    return this.ok(data, 'Column created');
  }

  @Patch(':id/columns/:colId')
  async updateColumn(
    @Headers('x-org-id') orgId: string,
    @Param('id') id: string,
    @Param('colId') colId: string,
    @Body() dto: UpdateColumnDto,
  ): Promise<{ data: unknown; success: boolean; message: string }> {
    const data = await this.templatesService.updateColumn(orgId, id, colId, dto);
    return this.ok(data, 'Column updated');
  }

  @Delete(':id/columns/:colId')
  async deleteColumn(@Headers('x-org-id') orgId: string, @Param('id') id: string, @Param('colId') colId: string): Promise<{ data: unknown; success: boolean; message: string }> {
    const data = await this.templatesService.deleteColumn(orgId, id, colId);
    return this.ok(data, 'Column deleted');
  }

  @Post(':id/rows')
  async addRow(@Headers('x-org-id') orgId: string, @Param('id') id: string, @Body() dto: CreateRowDto): Promise<{ data: unknown; success: boolean; message: string }> {
    const data = await this.templatesService.addRow(orgId, id, dto);
    return this.ok(data, 'Row created');
  }

  @Get(':id/rows')
  async getRows(
    @Headers('x-org-id') orgId: string,
    @Param('id') id: string,
    @Query('page', new ParseIntPipe({ optional: true })) page = 1,
    @Query('limit', new ParseIntPipe({ optional: true })) limit = 100,
  ): Promise<{ data: unknown; success: boolean; message: string }> {
    const data = await this.templatesService.getRows(orgId, id, page, limit);
    return this.ok(data, 'Rows fetched');
  }

  @Patch(':id/rows/:rowId')
  async updateRow(
    @Headers('x-org-id') orgId: string,
    @Param('id') id: string,
    @Param('rowId') rowId: string,
    @Body() dto: UpdateRowDto,
  ): Promise<{ data: unknown; success: boolean; message: string }> {
    const data = await this.templatesService.updateRow(orgId, id, rowId, dto);
    return this.ok(data, 'Row updated');
  }

  @Delete(':id/rows/:rowId')
  async deleteRow(@Headers('x-org-id') orgId: string, @Param('id') id: string, @Param('rowId') rowId: string): Promise<{ data: unknown; success: boolean; message: string }> {
    const data = await this.templatesService.deleteRow(orgId, id, rowId);
    return this.ok(data, 'Row deleted');
  }
}
