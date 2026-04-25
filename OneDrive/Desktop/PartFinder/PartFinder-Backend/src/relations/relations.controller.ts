import { Body, Controller, Delete, Get, Headers, Param, Patch, Post, UseGuards } from '@nestjs/common';
import { AuthGuard } from '@nestjs/passport';
import { CreateRelationDto, UpdateRelationDto } from './dto/create-relation.dto';
import { RelationsService } from './relations.service';

@Controller('relations')
@UseGuards(AuthGuard('jwt'))
export class RelationsController {
  constructor(private readonly relationsService: RelationsService) {}

  @Post()
  async create(@Headers('x-org-id') orgId: string, @Body() dto: CreateRelationDto) {
    const data = await this.relationsService.create(orgId, dto);
    return { data, success: true, message: 'Relation created' };
  }

  @Get()
  async list(@Headers('x-org-id') orgId: string) {
    const data = await this.relationsService.list(orgId);
    return { data, success: true, message: 'Relations fetched' };
  }

  @Get(':id')
  async getOne(@Headers('x-org-id') orgId: string, @Param('id') id: string) {
    const data = await this.relationsService.getOne(orgId, id);
    return { data, success: true, message: 'Relation fetched' };
  }

  @Patch(':id')
  async update(@Headers('x-org-id') orgId: string, @Param('id') id: string, @Body() dto: UpdateRelationDto) {
    const data = await this.relationsService.update(orgId, id, dto);
    return { data, success: true, message: 'Relation updated' };
  }

  @Delete(':id')
  async remove(@Headers('x-org-id') orgId: string, @Param('id') id: string) {
    const data = await this.relationsService.remove(orgId, id);
    return { data, success: true, message: 'Relation deleted' };
  }
}
