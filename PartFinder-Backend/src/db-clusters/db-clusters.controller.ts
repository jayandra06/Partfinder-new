import {
  Body,
  Controller,
  Get,
  HttpCode,
  HttpStatus,
  Post,
  UseGuards,
} from '@nestjs/common';
import { AuthGuard } from '@nestjs/passport';
import { DbClustersService } from './db-clusters.service';
import { DbClusterUriDto } from './dto/db-cluster-uri.dto';

@Controller('admin/db-clusters')
@UseGuards(AuthGuard('jwt'))
export class DbClustersController {
  constructor(private readonly dbClustersService: DbClustersService) {}

  @Get()
  list() {
    return this.dbClustersService.findAllForAdmin();
  }

  @Post('test')
  @HttpCode(HttpStatus.OK)
  test(@Body() dto: DbClusterUriDto) {
    return this.dbClustersService.probeConnection(dto.uri);
  }

  @Post()
  create(@Body() dto: DbClusterUriDto) {
    return this.dbClustersService.create(dto.uri);
  }
}
