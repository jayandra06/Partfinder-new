import {
  Body,
  Controller,
  Get,
  Param,
  Patch,
  Post,
  Query,
  UseGuards,
} from '@nestjs/common';
import { AuthGuard } from '@nestjs/passport';
import { WmsCtx } from './decorators/wms-context.decorator';
import { WmsSessionGuard } from './guards/wms-session.guard';
import { CreateWmsWarehouseDto, UpdateWmsWarehouseDto } from './dto/wms-warehouse.dto';
import { CreateWmsStockOwnerDto, UpdateWmsStockOwnerDto } from './dto/wms-stock-owner.dto';
import { CreateWmsZoneDto, UpdateWmsZoneDto } from './dto/wms-zone.dto';
import { CreateWmsLocationDto, UpdateWmsLocationDto } from './dto/wms-location.dto';
import { WmsSetupService } from './services/wms-setup.service';
import { UpsertOperatorGrantDto } from './dto/wms-operator-grant.dto';
import { WmsOperatorGrantsService } from './services/wms-operator-grants.service';
import { WmsDiagnosticsService } from './services/wms-diagnostics.service';
import type { WmsRequestContext } from './wms.types';
import { isWmsLiveStockPostingEnabled } from './wms-live-stock.util';
import { isWmsStagingStockPostingEnabled } from './wms-staging-stock.util';

@Controller('wms/v1')
@UseGuards(AuthGuard('jwt'), WmsSessionGuard)
export class WmsSetupController {
  constructor(
    private readonly setup: WmsSetupService,
    private readonly diagnostics: WmsDiagnosticsService,
    private readonly operatorGrants: WmsOperatorGrantsService,
  ) {}

  @Get('session/profile')
  sessionProfile(@WmsCtx() ctx: WmsRequestContext) {
    return {
      data: {
        email: ctx.email,
        role: ctx.role,
        isWmsAdmin: ctx.isWmsAdmin,
        operatorPermissions: ctx.operatorPermissions,
        allowedWarehouseIds: ctx.allowedWarehouseIds,
        allowedOwnerIds: ctx.allowedOwnerIds,
      },
      success: true,
    };
  }

  @Get('operator-grants')
  async listOperatorGrants(@WmsCtx() ctx: WmsRequestContext) {
    const data = await this.operatorGrants.list(ctx);
    return { data, success: true };
  }

  @Post('operator-grants')
  async upsertOperatorGrant(
    @WmsCtx() ctx: WmsRequestContext,
    @Body() dto: UpsertOperatorGrantDto,
  ) {
    const data = await this.operatorGrants.upsert(ctx, dto);
    return { data, success: true };
  }

  @Post('operator-grants/:email/remove')
  async removeOperatorGrant(
    @WmsCtx() ctx: WmsRequestContext,
    @Param('email') email: string,
  ) {
    const data = await this.operatorGrants.remove(ctx, email);
    return { data, success: true };
  }

  @Get('health')
  health(@WmsCtx() ctx: WmsRequestContext) {
    return {
      ok: true,
      orgCode: ctx.orgCode,
      phase: '02B.1',
      stockMutations:
        isWmsStagingStockPostingEnabled() || isWmsLiveStockPostingEnabled(),
      stagingStockPosting: isWmsStagingStockPostingEnabled(),
      liveStockPosting: isWmsLiveStockPostingEnabled(),
    };
  }

  @Get('diagnostics/transactions')
  async transactionProbe(@WmsCtx() ctx: WmsRequestContext) {
    const result = await this.diagnostics.probeTransactions(ctx.tenantUri);
    return { data: result, success: true };
  }

  @Get('warehouses')
  async listWarehouses(
    @WmsCtx() ctx: WmsRequestContext,
    @Query('activeOnly') activeOnly?: string,
  ) {
    const data = await this.setup.listWarehouses(ctx, activeOnly === 'true');
    return { data, success: true };
  }

  @Post('warehouses')
  async createWarehouse(
    @WmsCtx() ctx: WmsRequestContext,
    @Body() dto: CreateWmsWarehouseDto,
  ) {
    const data = await this.setup.createWarehouse(ctx, dto);
    return { data, success: true };
  }

  @Patch('warehouses/:id')
  async updateWarehouse(
    @WmsCtx() ctx: WmsRequestContext,
    @Param('id') id: string,
    @Body() dto: UpdateWmsWarehouseDto,
  ) {
    const data = await this.setup.updateWarehouse(ctx, id, dto);
    return { data, success: true };
  }

  @Get('stock-owners')
  async listStockOwners(
    @WmsCtx() ctx: WmsRequestContext,
    @Query('activeOnly') activeOnly?: string,
  ) {
    const data = await this.setup.listStockOwners(ctx, activeOnly === 'true');
    return { data, success: true };
  }

  @Post('stock-owners')
  async createStockOwner(
    @WmsCtx() ctx: WmsRequestContext,
    @Body() dto: CreateWmsStockOwnerDto,
  ) {
    const data = await this.setup.createStockOwner(ctx, dto);
    return { data, success: true };
  }

  @Patch('stock-owners/:id')
  async updateStockOwner(
    @WmsCtx() ctx: WmsRequestContext,
    @Param('id') id: string,
    @Body() dto: UpdateWmsStockOwnerDto,
  ) {
    const data = await this.setup.updateStockOwner(ctx, id, dto);
    return { data, success: true };
  }

  @Get('zones')
  async listZones(
    @WmsCtx() ctx: WmsRequestContext,
    @Query('warehouseId') warehouseId: string,
    @Query('activeOnly') activeOnly?: string,
  ) {
    const data = await this.setup.listZones(
      ctx,
      warehouseId,
      activeOnly === 'true',
    );
    return { data, success: true };
  }

  @Post('zones')
  async createZone(@WmsCtx() ctx: WmsRequestContext, @Body() dto: CreateWmsZoneDto) {
    const data = await this.setup.createZone(ctx, dto);
    return { data, success: true };
  }

  @Patch('zones/:id')
  async updateZone(
    @WmsCtx() ctx: WmsRequestContext,
    @Param('id') id: string,
    @Body() dto: UpdateWmsZoneDto,
  ) {
    const data = await this.setup.updateZone(ctx, id, dto);
    return { data, success: true };
  }

  @Get('locations')
  async listLocations(
    @WmsCtx() ctx: WmsRequestContext,
    @Query('warehouseId') warehouseId: string,
    @Query('zoneId') zoneId?: string,
    @Query('activeOnly') activeOnly?: string,
  ) {
    const data = await this.setup.listLocations(
      ctx,
      warehouseId,
      zoneId,
      activeOnly === 'true',
    );
    return { data, success: true };
  }

  @Post('locations')
  async createLocation(
    @WmsCtx() ctx: WmsRequestContext,
    @Body() dto: CreateWmsLocationDto,
  ) {
    const data = await this.setup.createLocation(ctx, dto);
    return { data, success: true };
  }

  @Patch('locations/:id')
  async updateLocation(
    @WmsCtx() ctx: WmsRequestContext,
    @Param('id') id: string,
    @Body() dto: UpdateWmsLocationDto,
  ) {
    const data = await this.setup.updateLocation(ctx, id, dto);
    return { data, success: true };
  }
}