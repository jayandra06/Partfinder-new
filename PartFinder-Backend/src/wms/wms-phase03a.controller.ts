import {
  Body,
  Controller,
  Get,
  Param,
  Post,
  Query,
  UseGuards,
} from '@nestjs/common';
import { AuthGuard } from '@nestjs/passport';
import { WmsCtx } from './decorators/wms-context.decorator';
import { WmsSessionGuard } from './guards/wms-session.guard';
import type { WmsRequestContext } from './wms.types';
import {
  AddExpectedReceiptLinesDto,
  CompletePutawayDto,
  ConfirmPhysicalDispositionDto,
  CreateExpectedReceiptDto,
  CreateGoodsReceiptDto,
  QcDecisionDto,
  RecordStagingPreflightDto,
} from './dto/wms-receiving.dto';
import { WmsReceivingService } from './services/wms-receiving.service';
import { WmsStagingGateService } from './services/wms-staging-gate.service';
import { assertWmsAdminWrite } from './wms-scope.util';

@Controller('wms/v1')
@UseGuards(AuthGuard('jwt'), WmsSessionGuard)
export class WmsPhase03aController {
  constructor(
    private readonly receiving: WmsReceivingService,
    private readonly stagingGate: WmsStagingGateService,
  ) {}

  @Post('staging-gate/preflight')
  async recordPreflight(
    @WmsCtx() ctx: WmsRequestContext,
    @Body() dto: RecordStagingPreflightDto,
  ) {
    assertWmsAdminWrite(ctx);
    const data = await this.stagingGate.recordPreflightEvidence(
      ctx,
      dto.tenantLabel,
    );
    return { data, success: true };
  }

  @Get('staging-gate/latest')
  async latestPreflight(@WmsCtx() ctx: WmsRequestContext) {
    const data = await this.stagingGate.latestEvidence(ctx);
    return { data, success: true };
  }

  @Post('receiving/expected')
  async createExpected(
    @WmsCtx() ctx: WmsRequestContext,
    @Body() dto: CreateExpectedReceiptDto,
  ) {
    const data = await this.receiving.createExpectedReceipt(ctx, dto);
    return { data, success: true };
  }

  @Get('receiving/expected')
  async listExpected(
    @WmsCtx() ctx: WmsRequestContext,
    @Query('warehouseId') warehouseId?: string,
  ) {
    const data = await this.receiving.listExpected(ctx, warehouseId);
    return { data, success: true };
  }

  @Post('receiving/expected/:expectedReceiptId/lines')
  async addExpectedLines(
    @WmsCtx() ctx: WmsRequestContext,
    @Param('expectedReceiptId') expectedReceiptId: string,
    @Body() dto: AddExpectedReceiptLinesDto,
  ) {
    const data = await this.receiving.addExpectedLines(ctx, expectedReceiptId, dto);
    return { data, success: true };
  }

  @Post('receiving/goods-receipts')
  async confirmReceipt(
    @WmsCtx() ctx: WmsRequestContext,
    @Body() dto: CreateGoodsReceiptDto,
  ) {
    const data = await this.receiving.confirmGoodsReceipt(ctx, dto);
    return { data, success: true };
  }

  @Get('receiving/goods-receipts')
  async listReceipts(
    @WmsCtx() ctx: WmsRequestContext,
    @Query('warehouseId') warehouseId?: string,
  ) {
    const data = await this.receiving.listReceipts(ctx, warehouseId);
    return { data, success: true };
  }

  @Get('receiving/goods-receipts/:goodsReceiptId/lines')
  async listLines(
    @WmsCtx() ctx: WmsRequestContext,
    @Param('goodsReceiptId') goodsReceiptId: string,
  ) {
    const data = await this.receiving.listReceiptLines(ctx, goodsReceiptId);
    return { data, success: true };
  }

  @Post('receiving/disposition/:receiptLineId')
  async confirmDisposition(
    @WmsCtx() ctx: WmsRequestContext,
    @Param('receiptLineId') receiptLineId: string,
    @Body() dto: ConfirmPhysicalDispositionDto,
  ) {
    const data = await this.receiving.confirmPhysicalDisposition(ctx, receiptLineId, dto);
    return { data, success: true };
  }

  @Post('receiving/qc/:receiptLineId')
  async qcDecision(
    @WmsCtx() ctx: WmsRequestContext,
    @Param('receiptLineId') receiptLineId: string,
    @Body() dto: QcDecisionDto,
  ) {
    const data = await this.receiving.applyQc(ctx, receiptLineId, dto);
    return { data, success: true };
  }

  @Get('receiving/qc')
  async listQc(@WmsCtx() ctx: WmsRequestContext, @Query('status') status?: string) {
    const data = await this.receiving.listQc(ctx, status);
    return { data, success: true };
  }

  @Get('receiving/putaway')
  async listPutaway(
    @WmsCtx() ctx: WmsRequestContext,
    @Query('warehouseId') warehouseId?: string,
  ) {
    const data = await this.receiving.listPutaway(ctx, warehouseId);
    return { data, success: true };
  }

  @Post('receiving/putaway/:putawayTaskId/complete')
  async completePutaway(
    @WmsCtx() ctx: WmsRequestContext,
    @Param('putawayTaskId') putawayTaskId: string,
    @Body() dto: CompletePutawayDto,
  ) {
    const data = await this.receiving.completePutaway(ctx, putawayTaskId, dto);
    return { data, success: true };
  }

  @Get('receiving/discrepancies')
  async discrepancies(@WmsCtx() ctx: WmsRequestContext) {
    const data = await this.receiving.listDiscrepancies(ctx);
    return { data, success: true };
  }

  @Get('receiving/history')
  async history(@WmsCtx() ctx: WmsRequestContext) {
    const data = await this.receiving.listReceivingHistory(ctx);
    return { data, success: true };
  }
}