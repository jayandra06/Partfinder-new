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
import type { WmsRequestContext, WmsSkuStatus } from './wms.types';
import {
  AddWmsSkuIdentifierDto,
  CreateWmsErpConnectorDto,
  CreateWmsSkuDto,
  CreateWmsUomDto,
  CreateWmsUomTemplateMappingDto,
  MergeWmsSkuDto,
  RetireWmsSkuDto,
  UpdateWmsSkuDto,
} from './dto/wms-sku.dto';
import { WmsSkuService } from './services/wms-sku.service';
import { WmsUomService } from './services/wms-uom.service';
import { WmsLedgerDryRunService } from './services/wms-ledger-dry-run.service';
import { WmsErpFoundationService } from './services/wms-erp-foundation.service';
import { isWmsLiveStockPostingEnabled } from './wms-live-stock.util';
import { isWmsStagingStockPostingEnabled } from './wms-staging-stock.util';
import { WmsPostInventoryDto } from './dto/wms-inventory.dto';
import {
  WmsErpLocationMappingDto,
  WmsErpSkuMappingDto,
} from './dto/wms-inventory.dto';
import { WmsInventoryPostingService } from './services/wms-inventory-posting.service';
import { WmsInventoryReadService } from './services/wms-inventory-read.service';

@Controller('wms/v1')
@UseGuards(AuthGuard('jwt'), WmsSessionGuard)
export class WmsPhase02Controller {
  constructor(
    private readonly skus: WmsSkuService,
    private readonly uom: WmsUomService,
    private readonly ledgerDryRun: WmsLedgerDryRunService,
    private readonly erp: WmsErpFoundationService,
    private readonly inventoryPost: WmsInventoryPostingService,
    private readonly inventoryRead: WmsInventoryReadService,
  ) {}

  @Get('catalog/status')
  catalogStatus() {
    return {
      phase: '03A',
      stagingStockPosting: isWmsStagingStockPostingEnabled(),
      liveStockPosting: isWmsLiveStockPostingEnabled(),
      erpLiveConnectors: false,
    };
  }

  @Get('uom')
  async listUom(@WmsCtx() ctx: WmsRequestContext) {
    const data = await this.uom.listAll(ctx);
    return { data: { items: data }, success: true };
  }

  @Get('uom/template-mappings')
  async listUomTemplateMappings(
    @WmsCtx() ctx: WmsRequestContext,
    @Query('templateId') templateId?: string,
  ) {
    const items = await this.uom.listTemplateMappings(ctx, templateId);
    return { data: { items }, success: true };
  }

  @Post('uom/template-mappings')
  async upsertUomTemplateMapping(
    @WmsCtx() ctx: WmsRequestContext,
    @Body() dto: CreateWmsUomTemplateMappingDto,
  ) {
    const data = await this.uom.upsertTemplateMapping(
      ctx,
      dto.templateId,
      dto.sourceLabel,
      dto.uomCode,
    );
    return { data, success: true };
  }

  @Post('uom/resolve-template')
  async resolveTemplateUom(
    @WmsCtx() ctx: WmsRequestContext,
    @Body() body: { templateId: string; sourceLabel: string },
  ) {
    const data = await this.uom.resolveTemplateUom(
      ctx,
      body.templateId,
      body.sourceLabel,
    );
    return { data, success: true };
  }

  @Post('uom')
  async createUom(@WmsCtx() ctx: WmsRequestContext, @Body() dto: CreateWmsUomDto) {
    const category =
      dto.category === 'count' ||
      dto.category === 'mass' ||
      dto.category === 'length' ||
      dto.category === 'volume' ||
      dto.category === 'custom'
        ? dto.category
        : 'custom';
    const data = await this.uom.createCustom(ctx, dto.code, dto.name, category);
    return { data, success: true };
  }

  @Get('skus')
  async listSkus(
    @WmsCtx() ctx: WmsRequestContext,
    @Query('status') status?: string,
  ) {
    const st =
      status === 'active' || status === 'merged' || status === 'retired'
        ? (status as WmsSkuStatus)
        : undefined;
    const data = await this.skus.listSkus(ctx, st);
    return { data, success: true };
  }

  @Post('skus')
  async createSku(@WmsCtx() ctx: WmsRequestContext, @Body() dto: CreateWmsSkuDto) {
    const data = await this.skus.createSku(ctx, dto);
    return { data, success: true };
  }

  @Get('skus/:skuId')
  async getSku(@WmsCtx() ctx: WmsRequestContext, @Param('skuId') skuId: string) {
    const data = await this.skus.getSku(ctx, skuId);
    return { data, success: true };
  }

  @Patch('skus/:skuId')
  async updateSku(
    @WmsCtx() ctx: WmsRequestContext,
    @Param('skuId') skuId: string,
    @Body() dto: UpdateWmsSkuDto,
  ) {
    const data = await this.skus.updateSku(ctx, skuId, dto);
    return { data, success: true };
  }

  @Post('skus/:skuId/identifiers')
  async addIdentifier(
    @WmsCtx() ctx: WmsRequestContext,
    @Param('skuId') skuId: string,
    @Body() dto: AddWmsSkuIdentifierDto,
  ) {
    const data = await this.skus.addIdentifier(ctx, skuId, dto);
    return { data, success: true };
  }

  @Post('skus/identifiers/check')
  async checkIdentifier(
    @WmsCtx() ctx: WmsRequestContext,
    @Body() dto: AddWmsSkuIdentifierDto,
    @Query('excludeSkuId') excludeSkuId?: string,
  ) {
    const data = await this.skus.checkIdentifier(ctx, dto, excludeSkuId);
    return { data, success: true };
  }

  @Post('skus/:skuId/merge')
  async mergeSku(
    @WmsCtx() ctx: WmsRequestContext,
    @Param('skuId') skuId: string,
    @Body() dto: MergeWmsSkuDto,
  ) {
    const data = await this.skus.mergeSku(ctx, skuId, dto);
    return { data, success: true };
  }

  @Post('skus/:skuId/retire')
  async retireSku(
    @WmsCtx() ctx: WmsRequestContext,
    @Param('skuId') skuId: string,
    @Body() dto: RetireWmsSkuDto,
  ) {
    const data = await this.skus.retireSku(ctx, skuId, dto);
    return { data, success: true };
  }

  @Get('ledger/opening-balance-dry-run')
  async openingBalanceDryRun(@WmsCtx() ctx: WmsRequestContext) {
    const data = await this.ledgerDryRun.openingBalanceDryRun(ctx);
    return { data, success: true };
  }

  @Post('inventory/post')
  async postInventory(
    @WmsCtx() ctx: WmsRequestContext,
    @Body() dto: WmsPostInventoryDto,
  ) {
    const data = await this.inventoryPost.post(ctx, dto);
    return { data, success: true };
  }

  @Get('inventory/balances')
  async listBalances(
    @WmsCtx() ctx: WmsRequestContext,
    @Query('warehouseId') warehouseId?: string,
    @Query('ownerId') ownerId?: string,
    @Query('locationId') locationId?: string,
    @Query('skuId') skuId?: string,
    @Query('skip') skip?: string,
    @Query('limit') limit?: string,
  ) {
    const data = await this.inventoryRead.listBalances(ctx, {
      warehouseId,
      ownerId,
      locationId,
      skuId,
      skip: skip ? Number(skip) : undefined,
      limit: limit ? Number(limit) : undefined,
    });
    return { data, success: true };
  }

  @Get('inventory/transactions')
  async listTransactions(
    @WmsCtx() ctx: WmsRequestContext,
    @Query('warehouseId') warehouseId?: string,
    @Query('ownerId') ownerId?: string,
    @Query('skuId') skuId?: string,
    @Query('type') type?: string,
    @Query('skip') skip?: string,
    @Query('limit') limit?: string,
  ) {
    const data = await this.inventoryRead.listTransactions(ctx, {
      warehouseId,
      ownerId,
      skuId,
      type,
      skip: skip ? Number(skip) : undefined,
      limit: limit ? Number(limit) : undefined,
    });
    return { data, success: true };
  }

  @Get('inventory/atp')
  async atp(
    @WmsCtx() ctx: WmsRequestContext,
    @Query('ownerId') ownerId: string,
    @Query('warehouseId') warehouseId: string,
    @Query('skuId') skuId: string,
  ) {
    const data = await this.inventoryRead.computeAtpForSku(
      ctx,
      ownerId,
      warehouseId,
      skuId,
    );
    return { data, success: true };
  }

  @Get('erp/connectors')
  async listErpConnectors(@WmsCtx() ctx: WmsRequestContext) {
    const data = await this.erp.listConnectors(ctx);
    return { data: { items: data }, success: true };
  }

  @Post('erp/connectors')
  async createErpConnector(
    @WmsCtx() ctx: WmsRequestContext,
    @Body() dto: CreateWmsErpConnectorDto,
  ) {
    const data = await this.erp.createConnector(ctx, dto);
    return { data, success: true };
  }

  @Post('erp/inbound/stub')
  async inboundStub(
    @WmsCtx() ctx: WmsRequestContext,
    @Body()
    body: {
      erpConnectorId: string;
      idempotencyKey: string;
      messageType: string;
      externalDocumentId?: string;
    },
  ) {
    const data = await this.erp.registerInboundMessageStub(
      ctx,
      body.erpConnectorId,
      body.idempotencyKey,
      body.messageType,
      body.externalDocumentId,
    );
    return { data, success: true };
  }

  @Get('erp/sku-mappings')
  async listErpSkuMappings(
    @WmsCtx() ctx: WmsRequestContext,
    @Query('erpConnectorId') erpConnectorId?: string,
  ) {
    const items = await this.erp.listSkuMappings(ctx, erpConnectorId);
    return { data: { items }, success: true };
  }

  @Post('erp/sku-mappings')
  async upsertErpSkuMapping(
    @WmsCtx() ctx: WmsRequestContext,
    @Body() dto: WmsErpSkuMappingDto,
  ) {
    const data = await this.erp.upsertSkuMapping(ctx, dto);
    return { data, success: true };
  }

  @Get('erp/location-mappings')
  async listErpLocationMappings(
    @WmsCtx() ctx: WmsRequestContext,
    @Query('erpConnectorId') erpConnectorId?: string,
  ) {
    const items = await this.erp.listLocationMappings(ctx, erpConnectorId);
    return { data: { items }, success: true };
  }

  @Post('erp/location-mappings')
  async upsertErpLocationMapping(
    @WmsCtx() ctx: WmsRequestContext,
    @Body() dto: WmsErpLocationMappingDto,
  ) {
    const data = await this.erp.upsertLocationMapping(ctx, dto);
    return { data, success: true };
  }

  @Get('erp/outbox')
  async listErpOutbox(
    @WmsCtx() ctx: WmsRequestContext,
    @Query('status') status?: string,
  ) {
    const items = await this.erp.listOutbox(ctx, status);
    return { data: { items }, success: true };
  }
}