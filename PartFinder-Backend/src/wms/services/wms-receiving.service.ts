import {
  BadRequestException,
  ConflictException,
  ForbiddenException,
  Injectable,
  NotFoundException,
} from '@nestjs/common';
import { ulid } from 'ulid';
import type { WmsRequestContext } from '../wms.types';
import {
  assertOwnerAccess,
  assertWarehouseAccess,
} from '../wms-scope.util';
import {
  assertPutawayPermission,
  assertQcPermission,
  assertReceivingPermission,
} from '../wms-operator-permissions.util';
import {
  AddExpectedReceiptLinesDto,
  CompletePutawayDto,
  ConfirmPhysicalDispositionDto,
  CreateExpectedReceiptDto,
  CreateGoodsReceiptDto,
  QcDecisionDto,
} from '../dto/wms-receiving.dto';
import type { ClientSession } from 'mongoose';
import { WmsPostInventoryDto } from '../dto/wms-inventory.dto';
import { WmsInventoryPostingService } from './wms-inventory-posting.service';
import { WmsStagingGateService } from './wms-staging-gate.service';
import { WmsTenantCollectionsService } from './wms-tenant-collections.service';

@Injectable()
export class WmsReceivingService {
  constructor(
    private readonly collections: WmsTenantCollectionsService,
    private readonly posting: WmsInventoryPostingService,
    private readonly stagingGate: WmsStagingGateService,
  ) {}

  async createExpectedReceipt(ctx: WmsRequestContext, dto: CreateExpectedReceiptDto) {
    assertReceivingPermission(ctx);
    assertWarehouseAccess(ctx, dto.warehouseId);
    assertOwnerAccess(ctx, dto.ownerId);
    await this.collections.ensurePhase03AIndexes(ctx.tenantUri);
    const coll = await this.collections.expectedReceipts(ctx.tenantUri);
    const expectedReceiptId = ulid();
    const now = new Date();
    const doc = {
      expectedReceiptId,
      ownerId: dto.ownerId,
      warehouseId: dto.warehouseId,
      status: 'open',
      externalPoId: dto.externalPoId?.trim() || null,
      supplierDeliveryNote: dto.supplierDeliveryNote?.trim() || null,
      erpConnectorId: dto.erpConnectorId?.trim() || null,
      supplierId: dto.supplierId?.trim() || null,
      notes: dto.notes?.trim() || '',
      lines: [] as Array<Record<string, unknown>>,
      createdBy: ctx.email,
      createdAt: now,
      updatedAt: now,
    };
    await coll.insertOne(doc);
    await this.audit(ctx, 'expected.created', expectedReceiptId, {});
    return { expectedReceiptId, status: 'open' };
  }

  async addExpectedLines(
    ctx: WmsRequestContext,
    expectedReceiptId: string,
    dto: AddExpectedReceiptLinesDto,
  ) {
    assertReceivingPermission(ctx);
    const coll = await this.collections.expectedReceipts(ctx.tenantUri);
    const doc = await coll.findOne({ expectedReceiptId });
    if (!doc) throw new NotFoundException('Expected receipt not found.');
    assertWarehouseAccess(ctx, String(doc.warehouseId));
    assertOwnerAccess(ctx, String(doc.ownerId));
    const lines = [
      ...(Array.isArray(doc.lines) ? doc.lines : []),
      ...dto.lines.map((l) => ({
        lineKey: ulid(),
        skuId: l.skuId,
        qtyExpected: l.qtyExpected,
        qtyReceivedTotal: 0,
        qtyDamagedTotal: 0,
        qtyRejectedTotal: 0,
        primaryUom: l.primaryUom ?? 'EA',
      })),
    ];
    await coll.updateOne(
      { expectedReceiptId },
      { $set: { lines, updatedAt: new Date() } },
    );
    return { expectedReceiptId, lineCount: lines.length };
  }

  async listExpected(ctx: WmsRequestContext, warehouseId?: string) {
    await this.collections.ensurePhase03AIndexes(ctx.tenantUri);
    const coll = await this.collections.expectedReceipts(ctx.tenantUri);
    const filter: Record<string, unknown> = {};
    if (warehouseId) {
      assertWarehouseAccess(ctx, warehouseId);
      filter.warehouseId = warehouseId;
    }
    const docs = await coll.find(filter).sort({ createdAt: -1 }).limit(200).toArray();
    return { items: docs, total: docs.length };
  }

  async confirmGoodsReceipt(ctx: WmsRequestContext, dto: CreateGoodsReceiptDto) {
    assertReceivingPermission(ctx);
    assertWarehouseAccess(ctx, dto.warehouseId);
    assertOwnerAccess(ctx, dto.ownerId);
    await this.collections.ensurePhase03AIndexes(ctx.tenantUri);

    const receipts = await this.collections.goodsReceipts(ctx.tenantUri);
    const existing = await receipts.findOne({ idempotencyKey: dto.idempotencyKey.trim() });
    if (existing) {
      return {
        goodsReceiptId: String(existing.goodsReceiptId),
        duplicate: true,
        status: String(existing.status),
      };
    }

    if (dto.expectedReceiptId) {
      await this.validatePartialReceiptAgainstExpected(ctx, dto);
    }

    const linesToPost = dto.lines.filter((l) => l.qtyReceived > 0);
    if (linesToPost.length === 0) {
      throw new BadRequestException('At least one line with positive quantity is required.');
    }

    await this.stagingGate.assertReceivingStockMutationsAllowed(ctx);

    const goodsReceiptId = ulid();
    const now = new Date();
    const lineDocs = dto.lines.map((l) => ({
      receiptLineId: l.receiptLineId?.trim() || ulid(),
      goodsReceiptId,
      expectedReceiptId: dto.expectedReceiptId ?? null,
      skuId: l.skuId,
      qtyReceived: l.qtyReceived,
      qtyAccepted: 0,
      qtyRejected: 0,
      lotId: l.lotId ?? null,
      serialId: l.serialId ?? null,
      expiryDate: l.expiryDate ?? null,
      damaged: l.damaged === true,
      shortage: l.shortage === true,
      overDelivery: l.overDelivery === true,
      qcStatus: 'pending',
      receivingLocationId: dto.receivingLocationId,
      putawayStatus: 'pending',
      createdAt: now,
    }));

    const conn = await this.collections.connection(ctx.tenantUri);
    const session = await conn.startSession();
    try {
      await session.withTransaction(async () => {
        await receipts.insertOne(
          {
            goodsReceiptId,
            idempotencyKey: dto.idempotencyKey.trim(),
            expectedReceiptId: dto.expectedReceiptId ?? null,
            ownerId: dto.ownerId,
            warehouseId: dto.warehouseId,
            receivingLocationId: dto.receivingLocationId,
            externalPoId: dto.externalPoId ?? null,
            supplierDeliveryNote: dto.supplierDeliveryNote ?? null,
            status: 'posted',
            createdBy: ctx.email,
            createdAt: now,
            updatedAt: now,
          },
          { session },
        );

        const lineColl = await this.collections.receiptLines(ctx.tenantUri);
        if (lineDocs.length) {
          await lineColl.insertMany(lineDocs, { session });
        }

        const qcColl = await this.collections.qcInspections(ctx.tenantUri);
        for (const l of linesToPost) {
          const postReceive: WmsPostInventoryDto = {
            type: 'receive',
            idempotencyKey: `${dto.idempotencyKey}:rcv:${l.skuId}:${l.lotId ?? ''}:${l.serialId ?? ''}`,
            ownerId: dto.ownerId,
            warehouseId: dto.warehouseId,
            lines: [
              {
                skuId: l.skuId,
                quantity: l.qtyReceived,
                toLocationId: dto.receivingLocationId,
                lotId: l.lotId,
                serialId: l.serialId,
              },
            ],
            refs: {
              goodsReceiptId,
              externalPoId: dto.externalPoId ?? '',
              supplierDeliveryNote: dto.supplierDeliveryNote ?? '',
            },
            emitErpEvent: true,
          };
          const postBlock: WmsPostInventoryDto = {
            type: 'qcBlock',
            idempotencyKey: `${dto.idempotencyKey}:blk:${l.skuId}:${l.lotId ?? ''}:${l.serialId ?? ''}`,
            ownerId: dto.ownerId,
            warehouseId: dto.warehouseId,
            lines: [
              {
                skuId: l.skuId,
                quantity: l.qtyReceived,
                toLocationId: dto.receivingLocationId,
                lotId: l.lotId,
                serialId: l.serialId,
              },
            ],
            refs: { goodsReceiptId, qc: 'pending' },
          };
          await this.posting.executeCommandsInSession(ctx, session, [postReceive, postBlock], now);

          const lineId =
            lineDocs.find(
              (x) =>
                x.skuId === l.skuId &&
                x.lotId === (l.lotId ?? null) &&
                x.serialId === (l.serialId ?? null),
            )?.receiptLineId ?? ulid();
          await qcColl.insertOne(
            {
              qcId: ulid(),
              receiptLineId: lineId,
              goodsReceiptId,
              status: 'pending',
              createdAt: now,
              updatedAt: now,
            },
            { session },
          );
        }

        if (dto.expectedReceiptId) {
          await this.applyExpectedReceiptProgress(
            ctx,
            session,
            dto.expectedReceiptId,
            dto.lines,
            now,
          );
        }

        await this.auditInSession(ctx, session, 'receipt.confirmed', goodsReceiptId, {
          lineCount: dto.lines.length,
        });
      });
    } finally {
      await session.endSession();
    }

    return { goodsReceiptId, duplicate: false, status: 'posted' };
  }

  async listReceipts(ctx: WmsRequestContext, warehouseId?: string) {
    const coll = await this.collections.goodsReceipts(ctx.tenantUri);
    const filter: Record<string, unknown> = {};
    if (warehouseId) {
      assertWarehouseAccess(ctx, warehouseId);
      filter.warehouseId = warehouseId;
    }
    const docs = await coll.find(filter).sort({ createdAt: -1 }).limit(200).toArray();
    return { items: docs, total: docs.length };
  }

  async listReceiptLines(ctx: WmsRequestContext, goodsReceiptId: string) {
    const coll = await this.collections.receiptLines(ctx.tenantUri);
    const docs = await coll.find({ goodsReceiptId }).toArray();
    return { items: docs, total: docs.length };
  }

  async applyQc(ctx: WmsRequestContext, receiptLineId: string, dto: QcDecisionDto) {
    assertQcPermission(ctx);
    const lineColl = await this.collections.receiptLines(ctx.tenantUri);
    const line = await lineColl.findOne({ receiptLineId });
    if (!line) throw new NotFoundException('Receipt line not found.');

    const qcColl = await this.collections.qcInspections(ctx.tenantUri);
    const ownerId = await this.ownerForReceipt(String(line.goodsReceiptId), ctx);
    const whId = await this.warehouseForReceipt(String(line.goodsReceiptId), ctx);
    assertOwnerAccess(ctx, ownerId);
    assertWarehouseAccess(ctx, whId);

    const qtyReceived = Number(line.qtyReceived ?? 0);
    const loc = String(line.receivingLocationId);
    const skuId = String(line.skuId);
    const now = new Date();

    const { qtyAccepted, qtyRejected, resolvedStatus } = this.resolveQcQuantities(
      dto,
      qtyReceived,
    );

    if (qtyAccepted + qtyRejected > qtyReceived) {
      throw new BadRequestException('QC quantities cannot exceed received quantity.');
    }

    const conn = await this.collections.connection(ctx.tenantUri);
    const session = await conn.startSession();
    try {
      await session.withTransaction(async () => {
        await qcColl.updateOne(
          { receiptLineId },
          {
            $set: {
              status: resolvedStatus,
              note: dto.note ?? '',
              qtyAccepted,
              qtyRejected,
              updatedAt: now,
              decidedBy: ctx.email,
            },
          },
          { session },
        );
        await lineColl.updateOne(
          { receiptLineId },
          {
            $set: {
              qcStatus: resolvedStatus,
              qtyAccepted,
              qtyRejected,
              updatedAt: now,
            },
          },
          { session },
        );

        if (qtyAccepted > 0) {
          await this.stagingGate.assertReceivingStockMutationsAllowed(ctx);
          const releasePost: WmsPostInventoryDto = {
            type: 'qcRelease',
            idempotencyKey: `qc:rel:${receiptLineId}:${qtyAccepted}:${qtyRejected}`,
            ownerId,
            warehouseId: whId,
            approvalNote: dto.approvalNote ?? dto.note ?? 'QC release',
            lines: [
              {
                skuId,
                quantity: qtyAccepted,
                toLocationId: loc,
                lotId: line.lotId ? String(line.lotId) : undefined,
                serialId: line.serialId ? String(line.serialId) : undefined,
              },
            ],
            emitErpEvent: true,
            refs: {
              receiptLineId,
              qcStatus: resolvedStatus,
              qtyAccepted: String(qtyAccepted),
              qtyRejected: String(qtyRejected),
            },
          };
          await this.posting.executeCommandsInSession(ctx, session, [releasePost], now);

          const putColl = await this.collections.putawayTasks(ctx.tenantUri);
          await putColl.insertOne(
            {
              putawayTaskId: ulid(),
              receiptLineId,
              goodsReceiptId: line.goodsReceiptId,
              warehouseId: whId,
              ownerId,
              fromLocationId: loc,
              status: 'open',
              skuId,
              quantity: qtyAccepted,
              createdAt: now,
              updatedAt: now,
            },
            { session },
          );
        }

        await this.auditInSession(ctx, session, 'qc.decision', receiptLineId, {
          status: resolvedStatus,
          qtyAccepted,
          qtyRejected,
        });
      });
    } finally {
      await session.endSession();
    }

    return { receiptLineId, status: resolvedStatus, qtyAccepted, qtyRejected };
  }

  async confirmPhysicalDisposition(
    ctx: WmsRequestContext,
    receiptLineId: string,
    dto: ConfirmPhysicalDispositionDto,
  ) {
    if (!ctx.isWmsAdmin && !ctx.operatorPermissions.supervise) {
      throw new ForbiddenException(
        'Physical disposition requires warehouse supervisor or administrator.',
      );
    }
    assertQcPermission(ctx);
    const lineColl = await this.collections.receiptLines(ctx.tenantUri);
    const line = await lineColl.findOne({ receiptLineId });
    if (!line) throw new NotFoundException('Receipt line not found.');

    const ownerId = await this.ownerForReceipt(String(line.goodsReceiptId), ctx);
    const whId = await this.warehouseForReceipt(String(line.goodsReceiptId), ctx);
    assertOwnerAccess(ctx, ownerId);
    assertWarehouseAccess(ctx, whId);

    const loc = String(line.receivingLocationId);
    const skuId = String(line.skuId);
    await this.stagingGate.assertReceivingStockMutationsAllowed(ctx);

    const conn = await this.collections.connection(ctx.tenantUri);
    const session = await conn.startSession();
    const now = new Date();
    try {
      await session.withTransaction(async () => {
        await this.posting.executeCommandsInSession(
          ctx,
          session,
          [
            {
              type: 'writeOff',
              idempotencyKey: dto.idempotencyKey,
              ownerId,
              warehouseId: whId,
              approvalNote: `${dto.dispositionType}: ${dto.dispositionReason}`,
              lines: [
                {
                  skuId,
                  quantity: dto.quantity,
                  fromLocationId: loc,
                  lotId: line.lotId ? String(line.lotId) : undefined,
                  serialId: line.serialId ? String(line.serialId) : undefined,
                },
              ],
              refs: {
                receiptLineId,
                dispositionType: dto.dispositionType,
                dispositionReason: dto.dispositionReason,
              },
              emitErpEvent: true,
            },
          ],
          now,
        );
        await lineColl.updateOne(
          { receiptLineId },
          {
            $set: {
              dispositionConfirmed: true,
              dispositionType: dto.dispositionType,
              dispositionAt: now,
              updatedAt: now,
            },
          },
          { session },
        );
        await this.auditInSession(ctx, session, 'disposition.confirmed', receiptLineId, {
          quantity: dto.quantity,
          dispositionType: dto.dispositionType,
        });
      });
    } finally {
      await session.endSession();
    }

    return { receiptLineId, quantity: dto.quantity, dispositionType: dto.dispositionType };
  }

  async listQc(ctx: WmsRequestContext, status?: string) {
    const coll = await this.collections.qcInspections(ctx.tenantUri);
    const filter = status ? { status } : {};
    const docs = await coll.find(filter).sort({ updatedAt: -1 }).limit(200).toArray();
    return { items: docs, total: docs.length };
  }

  async listPutaway(ctx: WmsRequestContext, warehouseId?: string) {
    const coll = await this.collections.putawayTasks(ctx.tenantUri);
    const filter: Record<string, unknown> = { status: 'open' };
    if (warehouseId) {
      assertWarehouseAccess(ctx, warehouseId);
      filter.warehouseId = warehouseId;
    }
    const docs = await coll.find(filter).sort({ createdAt: 1 }).limit(200).toArray();
    return { items: docs, total: docs.length };
  }

  async completePutaway(
    ctx: WmsRequestContext,
    putawayTaskId: string,
    dto: CompletePutawayDto,
  ) {
    assertPutawayPermission(ctx);
    const coll = await this.collections.putawayTasks(ctx.tenantUri);
    const task = await coll.findOne({ putawayTaskId });
    if (!task) throw new NotFoundException('Put-away task not found.');
    if (String(task.status) !== 'open') {
      throw new ConflictException('Put-away task is not open.');
    }

    const ownerId = String(task.ownerId);
    const warehouseId = String(task.warehouseId);
    assertOwnerAccess(ctx, ownerId);
    assertWarehouseAccess(ctx, warehouseId);

    await this.stagingGate.assertReceivingStockMutationsAllowed(ctx);
    await this.posting.post(ctx, {
      type: 'move',
      idempotencyKey: dto.idempotencyKey,
      ownerId,
      warehouseId,
      lines: [
        {
          skuId: String(task.skuId),
          quantity: Number(task.quantity ?? 0),
          fromLocationId: String(task.fromLocationId),
          toLocationId: dto.toLocationId,
        },
      ],
      refs: { putawayTaskId, receiptLineId: String(task.receiptLineId) },
      emitErpEvent: true,
    });

    await coll.updateOne(
      { putawayTaskId },
      {
        $set: {
          status: 'completed',
          toLocationId: dto.toLocationId,
          completedBy: ctx.email,
          updatedAt: new Date(),
        },
      },
    );

    const lineColl = await this.collections.receiptLines(ctx.tenantUri);
    await lineColl.updateOne(
      { receiptLineId: task.receiptLineId },
      { $set: { putawayStatus: 'completed', updatedAt: new Date() } },
    );

    await this.audit(ctx, 'putaway.completed', putawayTaskId, {
      toLocationId: dto.toLocationId,
    });
    return { putawayTaskId, status: 'completed' };
  }

  async listDiscrepancies(ctx: WmsRequestContext) {
    const lineColl = await this.collections.receiptLines(ctx.tenantUri);
    const docs = await lineColl
      .find({
        $or: [{ damaged: true }, { shortage: true }, { overDelivery: true }],
      })
      .limit(500)
      .toArray();
    return { items: docs, total: docs.length };
  }

  async listReceivingHistory(ctx: WmsRequestContext) {
    const audit = await this.collections.receivingAuditLog(ctx.tenantUri);
    const docs = await audit.find({}).sort({ createdAt: -1 }).limit(200).toArray();
    return { items: docs, total: docs.length };
  }

  private async ownerForReceipt(goodsReceiptId: string, ctx: WmsRequestContext) {
    const coll = await this.collections.goodsReceipts(ctx.tenantUri);
    const doc = await coll.findOne({ goodsReceiptId });
    if (!doc) throw new NotFoundException('Receipt not found.');
    return String(doc.ownerId);
  }

  private async warehouseForReceipt(goodsReceiptId: string, ctx: WmsRequestContext) {
    const coll = await this.collections.goodsReceipts(ctx.tenantUri);
    const doc = await coll.findOne({ goodsReceiptId });
    if (!doc) throw new NotFoundException('Receipt not found.');
    return String(doc.warehouseId);
  }

  private async audit(
    ctx: WmsRequestContext,
    action: string,
    entityId: string,
    detail: Record<string, unknown>,
  ) {
    const coll = await this.collections.receivingAuditLog(ctx.tenantUri);
    await coll.insertOne({
      action,
      entityId,
      detail,
      actorEmail: ctx.email,
      createdAt: new Date(),
    });
  }

  private async auditInSession(
    ctx: WmsRequestContext,
    session: ClientSession,
    action: string,
    entityId: string,
    detail: Record<string, unknown>,
  ) {
    const coll = await this.collections.receivingAuditLog(ctx.tenantUri);
    await coll.insertOne(
      {
        action,
        entityId,
        detail,
        actorEmail: ctx.email,
        createdAt: new Date(),
      },
      { session },
    );
  }

  private resolveQcQuantities(dto: QcDecisionDto, qtyReceived: number) {
    let qtyAccepted = dto.qtyAccepted ?? 0;
    let qtyRejected = dto.qtyRejected ?? 0;

    if (dto.status === 'partial') {
      if (qtyAccepted <= 0 && qtyRejected <= 0) {
        throw new BadRequestException('Partial QC requires qtyAccepted or qtyRejected.');
      }
      const resolvedStatus =
        qtyAccepted > 0 && qtyRejected > 0
          ? 'partial'
          : qtyAccepted > 0
            ? 'accepted'
            : 'rejected';
      return { qtyAccepted, qtyRejected, resolvedStatus };
    }

    if (dto.status === 'accepted' || dto.status === 'released') {
      qtyAccepted = dto.qtyAccepted ?? qtyReceived;
      qtyRejected = Math.max(0, qtyReceived - qtyAccepted);
      return { qtyAccepted, qtyRejected, resolvedStatus: dto.status };
    }

    if (dto.status === 'rejected') {
      qtyRejected = dto.qtyRejected ?? qtyReceived;
      qtyAccepted = 0;
      return { qtyAccepted, qtyRejected, resolvedStatus: 'rejected' };
    }

    if (dto.status === 'quarantined') {
      return { qtyAccepted: 0, qtyRejected: 0, resolvedStatus: 'quarantined' };
    }

    return { qtyAccepted: 0, qtyRejected: 0, resolvedStatus: dto.status };
  }

  private async validatePartialReceiptAgainstExpected(
    ctx: WmsRequestContext,
    dto: CreateGoodsReceiptDto,
  ) {
    const coll = await this.collections.expectedReceipts(ctx.tenantUri);
    const expected = await coll.findOne({ expectedReceiptId: dto.expectedReceiptId });
    if (!expected) throw new NotFoundException('Expected receipt not found.');

    assertWarehouseAccess(ctx, String(expected.warehouseId));
    assertOwnerAccess(ctx, String(expected.ownerId));

    const lines = Array.isArray(expected.lines) ? expected.lines : [];
    for (const incoming of dto.lines) {
      if (incoming.qtyReceived <= 0) continue;
      const match = lines.find((x: Record<string, unknown>) => x.skuId === incoming.skuId);
      if (!match) {
        throw new BadRequestException(`SKU ${incoming.skuId} is not on the expected receipt.`);
      }
      const expectedQty = Number(match.qtyExpected ?? 0);
      const prior = Number(match.qtyReceivedTotal ?? 0);
      const next = prior + incoming.qtyReceived;
      if (next > expectedQty && incoming.overDelivery !== true) {
        throw new BadRequestException(
          `Over-delivery for SKU ${incoming.skuId}. Mark overDelivery or obtain supervisor approval.`,
        );
      }
    }
  }

  private async applyExpectedReceiptProgress(
    ctx: WmsRequestContext,
    session: ClientSession,
    expectedReceiptId: string,
    receiptLines: CreateGoodsReceiptDto['lines'],
    now: Date,
  ) {
    const coll = await this.collections.expectedReceipts(ctx.tenantUri);
    const doc = await coll.findOne({ expectedReceiptId }, { session });
    if (!doc) return;

    const lines = Array.isArray(doc.lines) ? [...doc.lines] : [];
    for (const incoming of receiptLines) {
      if (incoming.qtyReceived <= 0) continue;
      const idx = lines.findIndex((x) => (x as Record<string, unknown>).skuId === incoming.skuId);
      if (idx < 0) continue;
      const row = lines[idx] as Record<string, unknown>;
      row.qtyReceivedTotal =
        Number(row.qtyReceivedTotal ?? 0) + incoming.qtyReceived;
      if (incoming.damaged === true) {
        row.qtyDamagedTotal = Number(row.qtyDamagedTotal ?? 0) + incoming.qtyReceived;
      }
      lines[idx] = row;
    }

    const status = this.computeExpectedStatus(lines as Array<Record<string, unknown>>);
    await coll.updateOne(
      { expectedReceiptId },
      { $set: { lines, status, updatedAt: now } },
      { session },
    );
  }

  private computeExpectedStatus(lines: Array<Record<string, unknown>>) {
    if (!lines.length) return 'open';
    let anyReceived = false;
    let allComplete = true;
    let anyException = false;
    for (const line of lines) {
      const expected = Number(line.qtyExpected ?? 0);
      const received = Number(line.qtyReceivedTotal ?? 0);
      if (received > 0) anyReceived = true;
      if (received < expected) allComplete = false;
      if (received > expected) anyException = true;
    }
    if (anyException) return 'closed_with_exception';
    if (allComplete && anyReceived) return 'complete';
    if (anyReceived) return 'partially_received';
    return 'open';
  }
}