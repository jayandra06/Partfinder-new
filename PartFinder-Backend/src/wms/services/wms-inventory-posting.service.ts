import {
  BadRequestException,
  ConflictException,
  ForbiddenException,
  Injectable,
} from '@nestjs/common';
import { createHash } from 'crypto';
import { Types, type ClientSession } from 'mongoose';
import { ulid } from 'ulid';
import type { WmsRequestContext } from '../wms.types';
import { buildBalanceKey } from '../wms-balance-key.util';
import { isWmsStockPostingAllowed } from '../wms-staging-stock.util';
import type {
  WmsInventoryLine,
  WmsInventoryPostCommand,
  WmsStockTxnType,
} from '../wms-stock-transaction.types';
import {
  assertOwnerAccess,
  assertWarehouseAccess,
  assertWmsAdminWrite,
} from '../wms-scope.util';
import { WmsPostInventoryDto } from '../dto/wms-inventory.dto';
import { WmsTenantCollectionsService } from './wms-tenant-collections.service';
import { WmsTransactionGuardService } from './wms-transaction-guard.service';

const MAX_TXN_RETRIES = 5;

type BalanceDelta = {
  ownerId: string;
  warehouseId: string;
  locationId: string;
  skuKey: string;
  lotId?: string;
  serialId?: string;
  delta: number;
  blockedDelta?: number;
  primaryUom: string;
};

@Injectable()
export class WmsInventoryPostingService {
  constructor(
    private readonly collections: WmsTenantCollectionsService,
    private readonly txnGuard: WmsTransactionGuardService,
  ) {}

  async post(ctx: WmsRequestContext, dto: WmsPostInventoryDto) {
    if (!isWmsStockPostingAllowed()) {
      throw new ForbiddenException(
        'WMS stock posting is disabled. Set WMS_STAGING_STOCK_POSTING=true for controlled staging posts.',
      );
    }
    this.assertTypePermission(ctx, dto.type);
    assertWarehouseAccess(ctx, dto.warehouseId);
    assertOwnerAccess(ctx, dto.ownerId);

    const command: WmsInventoryPostCommand = {
      type: dto.type,
      idempotencyKey: dto.idempotencyKey.trim(),
      ownerId: dto.ownerId,
      warehouseId: dto.warehouseId,
      lines: dto.lines,
      refs: dto.refs,
      reversalOf: dto.reversalOf,
      approvalNote: dto.approvalNote,
      emitErpEvent: dto.emitErpEvent,
    };

    await this.txnGuard.assertTransactionsSupported(ctx.tenantUri);
    await this.collections.ensurePhase02B1Indexes(ctx.tenantUri);
    await this.validateMasters(ctx, command);

    const requestHash = this.hashCommand(command);
    const replay = await this.checkIdempotency(ctx, command.idempotencyKey, requestHash);
    if (replay) {
      return replay;
    }

    const deltas = await this.buildDeltas(ctx, command);
    const txnId = ulid();
    const now = new Date();

    for (let attempt = 0; attempt < MAX_TXN_RETRIES; attempt++) {
      try {
        const result = await this.runTransactionalPost(
          ctx,
          command,
          deltas,
          txnId,
          requestHash,
          now,
        );
        return result;
      } catch (e: unknown) {
        if (this.isVersionConflict(e) && attempt < MAX_TXN_RETRIES - 1) {
          continue;
        }
        throw e;
      }
    }
    throw new ConflictException('Could not post inventory after retries.');
  }

  /**
   * Posts inventory commands inside an existing MongoDB session (no nested transaction).
   * Used by receiving to pair receive + qcBlock atomically with receipt documents.
   */
  async executeCommandsInSession(
    ctx: WmsRequestContext,
    session: ClientSession,
    commands: WmsPostInventoryDto[],
    now: Date = new Date(),
  ): Promise<Array<Record<string, unknown>>> {
    if (!isWmsStockPostingAllowed()) {
      throw new ForbiddenException(
        'WMS stock posting is disabled. Set WMS_STAGING_STOCK_POSTING=true for controlled staging posts.',
      );
    }
  const outputs: Array<Record<string, unknown>> = [];
    for (const dto of commands) {
      this.assertTypePermission(ctx, dto.type);
      assertWarehouseAccess(ctx, dto.warehouseId);
      assertOwnerAccess(ctx, dto.ownerId);

      const command: WmsInventoryPostCommand = {
        type: dto.type,
        idempotencyKey: dto.idempotencyKey.trim(),
        ownerId: dto.ownerId,
        warehouseId: dto.warehouseId,
        lines: dto.lines,
        refs: dto.refs,
        reversalOf: dto.reversalOf,
        approvalNote: dto.approvalNote,
        emitErpEvent: dto.emitErpEvent,
      };

      await this.validateMasters(ctx, command);
      const requestHash = this.hashCommand(command);
      const idemColl = await this.collections.idempotencyKeys(ctx.tenantUri);
      const existing = await idemColl.findOne({ key: command.idempotencyKey }, { session });
      if (existing) {
        const row = existing as Record<string, unknown>;
        if (String(row.requestHash) !== requestHash) {
          throw new ConflictException(
            'Idempotency key already used with a different payload.',
          );
        }
        outputs.push({
          txnId: String(row.txnId),
          duplicate: true,
          type: command.type,
        });
        continue;
      }

      const deltas = await this.buildDeltas(ctx, command);
      const txnId = ulid();
      await this.reserveIdempotency(
        ctx,
        session,
        command.idempotencyKey,
        requestHash,
        txnId,
        now,
      );

      const txnDoc = {
        txnId,
        type: command.type,
        ownerId: command.ownerId,
        warehouseId: command.warehouseId,
        lines: command.lines,
        refs: command.refs ?? {},
        idempotencyKey: command.idempotencyKey,
        reversalOf: command.reversalOf ?? null,
        approvalNote: command.approvalNote ?? '',
        createdBy: ctx.email,
        createdAt: now,
      };

      const txColl = await this.collections.stockTransactions(ctx.tenantUri);
      await txColl.insertOne(txnDoc, { session });

      for (const delta of deltas) {
        await this.applyDelta(ctx, session, delta, now);
      }

      if (command.emitErpEvent) {
        const outbox = await this.collections.outboxEvents(ctx.tenantUri);
        await outbox.insertOne(
          {
            eventId: ulid(),
            txnId,
            eventType: this.mapErpEventType(command.type),
            payload: {
              txnId,
              type: command.type,
              ownerId: command.ownerId,
              warehouseId: command.warehouseId,
              lineCount: command.lines.length,
            },
            status: 'pending',
            attemptCount: 0,
            lastError: null,
            createdAt: now,
            updatedAt: now,
          },
          { session },
        );
      }

      await this.finalizeIdempotency(ctx, session, command.idempotencyKey, txnId);
      outputs.push({
        txnId,
        type: command.type,
        ownerId: command.ownerId,
        warehouseId: command.warehouseId,
        lineCount: command.lines.length,
        duplicate: false,
      });
    }
    return outputs;
  }

  private async runTransactionalPost(
    ctx: WmsRequestContext,
    command: WmsInventoryPostCommand,
    deltas: BalanceDelta[],
    txnId: string,
    requestHash: string,
    now: Date,
  ) {
    const conn = await this.collections.connection(ctx.tenantUri);
    const session = await conn.startSession();
    try {
      let output: Record<string, unknown> = {};
      await session.withTransaction(async () => {
        await this.reserveIdempotency(ctx, session, command.idempotencyKey, requestHash, txnId, now);

        const txnDoc = {
          txnId,
          type: command.type,
          ownerId: command.ownerId,
          warehouseId: command.warehouseId,
          lines: command.lines,
          refs: command.refs ?? {},
          idempotencyKey: command.idempotencyKey,
          reversalOf: command.reversalOf ?? null,
          approvalNote: command.approvalNote ?? '',
          createdBy: ctx.email,
          createdAt: now,
        };

        const txColl = await this.collections.stockTransactions(ctx.tenantUri);
        await txColl.insertOne(txnDoc, { session });

        for (const delta of deltas) {
          await this.applyDelta(ctx, session, delta, now);
        }

        if (command.emitErpEvent) {
          const outbox = await this.collections.outboxEvents(ctx.tenantUri);
          await outbox.insertOne(
            {
              eventId: ulid(),
              txnId,
              eventType: this.mapErpEventType(command.type),
              payload: {
                txnId,
                type: command.type,
                ownerId: command.ownerId,
                warehouseId: command.warehouseId,
                lineCount: command.lines.length,
              },
              status: 'pending',
              attemptCount: 0,
              lastError: null,
              createdAt: now,
              updatedAt: now,
            },
            { session },
          );
        }

        await this.finalizeIdempotency(ctx, session, command.idempotencyKey, txnId);

        output = {
          txnId,
          type: command.type,
          ownerId: command.ownerId,
          warehouseId: command.warehouseId,
          lineCount: command.lines.length,
          duplicate: false,
        };
      });
      return output;
    } finally {
      await session.endSession();
    }
  }

  private async applyDelta(
    ctx: WmsRequestContext,
    session: ClientSession,
    delta: BalanceDelta,
    now: Date,
  ) {
    const coll = await this.collections.stockBalances(ctx.tenantUri);
    const balanceKey = buildBalanceKey({
      ownerId: delta.ownerId,
      warehouseId: delta.warehouseId,
      locationId: delta.locationId,
      skuKey: delta.skuKey,
      lotId: delta.lotId,
      serialId: delta.serialId,
    });

    const existing = await coll.findOne({ balanceKey }, { session });
    if (!existing) {
      if (delta.delta < 0) {
        throw new BadRequestException(
          `Insufficient stock at location for SKU ${delta.skuKey}.`,
        );
      }
      const blocked0 = Math.max(0, delta.blockedDelta ?? 0);
      const onHand0 = delta.delta;
      if (blocked0 > onHand0) {
        throw new BadRequestException('Blocked quantity cannot exceed on-hand.');
      }
      await coll.insertOne(
        {
          balanceKey,
          ownerId: delta.ownerId,
          warehouseId: delta.warehouseId,
          locationId: delta.locationId,
          skuKey: delta.skuKey,
          lotId: delta.lotId ?? null,
          serialId: delta.serialId ?? null,
          primaryUom: delta.primaryUom,
          onHand: onHand0,
          reserved: 0,
          blockedQty: blocked0,
          version: 1,
          updatedAt: now,
        },
        { session },
      );
      return;
    }

    const row = existing as Record<string, unknown>;
    const version = Number(row.version ?? 0);
    const onHand = Number(row.onHand ?? 0);
    const reserved = Number(row.reserved ?? 0);
    const blockedQty = Number(row.blockedQty ?? 0);
    const next = onHand + delta.delta;
    const nextBlocked = blockedQty + (delta.blockedDelta ?? 0);
    if (next < 0) {
      throw new BadRequestException('Negative on-hand balance rejected.');
    }
    if (nextBlocked < 0) {
      throw new BadRequestException('Negative blocked quantity rejected.');
    }
    if (next < reserved) {
      throw new BadRequestException(
        'On-hand cannot fall below reserved quantity.',
      );
    }
    if (nextBlocked > next) {
      throw new BadRequestException('Blocked quantity cannot exceed on-hand.');
    }

    const res = await coll.updateOne(
      { balanceKey, version },
      {
        $set: { onHand: next, blockedQty: nextBlocked, updatedAt: now },
        $inc: { version: 1 },
      },
      { session },
    );
    if (res.matchedCount !== 1) {
      const err = new Error('VERSION_CONFLICT');
      (err as Error & { code?: string }).code = 'VERSION_CONFLICT';
      throw err;
    }
  }

  private async buildDeltas(
    ctx: WmsRequestContext,
    command: WmsInventoryPostCommand,
  ): Promise<BalanceDelta[]> {
    if (command.type === 'reversal') {
      return this.buildReversalDeltas(ctx, command);
    }

    const skuUom = await this.loadSkuUoms(ctx, command.lines);
    const deltas: BalanceDelta[] = [];

    for (const line of command.lines) {
      const uom = skuUom.get(line.skuId) ?? line.uom ?? 'EA';
      const qty = line.quantity;

      switch (command.type) {
        case 'openingBalance':
        case 'receive':
          if (qty <= 0) {
            throw new BadRequestException('Line quantity must be positive.');
          }
          if (!line.toLocationId) {
            throw new BadRequestException('toLocationId required.');
          }
          deltas.push(
            this.positiveDelta(command, line, line.toLocationId, qty, uom),
          );
          break;
        case 'move':
          if (qty <= 0) {
            throw new BadRequestException('Line quantity must be positive.');
          }
          if (!line.fromLocationId || !line.toLocationId) {
            throw new BadRequestException('fromLocationId and toLocationId required for move.');
          }
          deltas.push(
            this.negativeDelta(command, line, line.fromLocationId, qty, uom),
          );
          deltas.push(
            this.positiveDelta(command, line, line.toLocationId, qty, uom),
          );
          break;
        case 'adjust':
          if (!line.toLocationId) {
            throw new BadRequestException('toLocationId required for adjust.');
          }
          if (!command.approvalNote?.trim()) {
            throw new BadRequestException('approvalNote required for adjust.');
          }
          {
            const signed =
              line.qtyDelta !== undefined ? line.qtyDelta : line.quantity;
            if (signed === 0) {
              throw new BadRequestException('Adjust delta cannot be zero.');
            }
            deltas.push({
              ...this.baseParts(command, line),
              locationId: line.toLocationId,
              delta: signed,
              primaryUom: uom,
            });
          }
          break;
        case 'writeOff':
          if (qty <= 0) {
            throw new BadRequestException('Line quantity must be positive.');
          }
          if (!line.fromLocationId) {
            throw new BadRequestException('fromLocationId required for writeOff.');
          }
          if (!command.approvalNote?.trim()) {
            throw new BadRequestException('approvalNote required for writeOff.');
          }
          deltas.push(
            this.negativeDelta(command, line, line.fromLocationId, qty, uom),
          );
          break;
        case 'qcBlock':
          if (qty <= 0) {
            throw new BadRequestException('Line quantity must be positive.');
          }
          if (!line.toLocationId) {
            throw new BadRequestException('toLocationId required for qcBlock.');
          }
          deltas.push({
            ...this.baseParts(command, line),
            locationId: line.toLocationId,
            delta: 0,
            blockedDelta: qty,
            primaryUom: uom,
          });
          break;
        case 'qcRelease':
          if (qty <= 0) {
            throw new BadRequestException('Line quantity must be positive.');
          }
          if (!line.toLocationId) {
            throw new BadRequestException('toLocationId required for qcRelease.');
          }
          if (!command.approvalNote?.trim()) {
            throw new BadRequestException('approvalNote required for qcRelease.');
          }
          deltas.push({
            ...this.baseParts(command, line),
            locationId: line.toLocationId,
            delta: 0,
            blockedDelta: -qty,
            primaryUom: uom,
          });
          break;
        case 'ownerTransfer':
          if (qty <= 0) {
            throw new BadRequestException('Line quantity must be positive.');
          }
          if (!line.fromLocationId || !line.targetOwnerId) {
            throw new BadRequestException(
              'fromLocationId and targetOwnerId required for ownerTransfer.',
            );
          }
          assertOwnerAccess(ctx, line.targetOwnerId);
          deltas.push(
            this.negativeDelta(command, line, line.fromLocationId, qty, uom),
          );
          deltas.push({
            ownerId: line.targetOwnerId,
            warehouseId: command.warehouseId,
            locationId: line.fromLocationId,
            skuKey: line.skuId,
            lotId: line.lotId,
            serialId: line.serialId,
            delta: qty,
            primaryUom: uom,
          });
          break;
        default:
          throw new BadRequestException(`Unsupported transaction type ${command.type}.`);
      }
    }
    return deltas;
  }

  private async buildReversalDeltas(
    ctx: WmsRequestContext,
    command: WmsInventoryPostCommand,
  ): Promise<BalanceDelta[]> {
    if (!command.reversalOf?.trim()) {
      throw new BadRequestException('reversalOf txnId required.');
    }
    if (!command.approvalNote?.trim()) {
      throw new BadRequestException('approvalNote required for reversal.');
    }
    const txColl = await this.collections.stockTransactions(ctx.tenantUri);
    const original = await txColl.findOne({ txnId: command.reversalOf.trim() });
    if (!original) {
      throw new BadRequestException('Original transaction not found.');
    }
    const priorType = String((original as Record<string, unknown>).type);
    if (priorType === 'reversal') {
      throw new BadRequestException('Cannot reverse a reversal transaction.');
    }

    const skuUom = await this.loadSkuUoms(ctx, command.lines.length ? command.lines : (original as { lines: WmsInventoryLine[] }).lines);
    const lines = (original as { lines: WmsInventoryLine[] }).lines ?? [];
    const invertType = priorType as WmsStockTxnType;
    const fakeCmd: WmsInventoryPostCommand = {
      ...command,
      type: invertType,
      lines,
      approvalNote: command.approvalNote ?? 'reversal',
    };
    const forward = await this.buildDeltas(ctx, fakeCmd);
    return forward.map((d) => ({ ...d, delta: -d.delta }));
  }

  private positiveDelta(
    command: WmsInventoryPostCommand,
    line: WmsInventoryLine,
    locationId: string,
    qty: number,
    uom: string,
  ): BalanceDelta {
    return {
      ...this.baseParts(command, line),
      locationId,
      delta: qty,
      primaryUom: uom,
    };
  }

  private negativeDelta(
    command: WmsInventoryPostCommand,
    line: WmsInventoryLine,
    locationId: string,
    qty: number,
    uom: string,
  ): BalanceDelta {
    return {
      ...this.baseParts(command, line),
      locationId,
      delta: -qty,
      primaryUom: uom,
    };
  }

  private baseParts(command: WmsInventoryPostCommand, line: WmsInventoryLine) {
    return {
      ownerId: command.ownerId,
      warehouseId: command.warehouseId,
      skuKey: line.skuId,
      lotId: line.lotId,
      serialId: line.serialId,
    };
  }

  private async validateMasters(
    ctx: WmsRequestContext,
    command: WmsInventoryPostCommand,
  ) {
    const wh = await this.collections.warehouses(ctx.tenantUri);
    const warehouse = await wh.findOne({ _id: this.toObjectId(command.warehouseId) });
    if (!warehouse || warehouse.isActive === false) {
      throw new BadRequestException('Warehouse not found or inactive.');
    }
    const owners = await this.collections.stockOwners(ctx.tenantUri);
    const owner = await owners.findOne({ _id: this.toObjectId(command.ownerId) });
    if (!owner || owner.isActive === false) {
      throw new BadRequestException('Stock owner not found or inactive.');
    }

    const locIds = new Set<string>();
    for (const line of command.lines) {
      if (line.fromLocationId) locIds.add(line.fromLocationId);
      if (line.toLocationId) locIds.add(line.toLocationId);
    }
    if (locIds.size) {
      const locs = await this.collections.locations(ctx.tenantUri);
      for (const id of locIds) {
        const loc = await locs.findOne({ _id: this.toObjectId(id) });
        if (!loc || String(loc.warehouseId) !== command.warehouseId) {
          throw new BadRequestException(`Invalid location ${id} for warehouse.`);
        }
      }
    }

    const skuColl = await this.collections.skus(ctx.tenantUri);
    for (const line of command.lines) {
      const sku = await skuColl.findOne({ skuId: line.skuId, status: 'active' });
      if (!sku) {
        throw new BadRequestException(`SKU ${line.skuId} not active.`);
      }
    }
  }

  private async loadSkuUoms(
    ctx: WmsRequestContext,
    lines: WmsInventoryLine[],
  ): Promise<Map<string, string>> {
    const skuColl = await this.collections.skus(ctx.tenantUri);
    const map = new Map<string, string>();
    for (const line of lines) {
      if (map.has(line.skuId)) continue;
      const sku = await skuColl.findOne({ skuId: line.skuId });
      map.set(line.skuId, String(sku?.primaryUom ?? 'EA'));
    }
    return map;
  }

  private hashCommand(command: WmsInventoryPostCommand): string {
    return createHash('sha256')
      .update(JSON.stringify(command))
      .digest('hex');
  }

  private async checkIdempotency(
    ctx: WmsRequestContext,
    key: string,
    requestHash: string,
  ) {
    const coll = await this.collections.idempotencyKeys(ctx.tenantUri);
    const existing = await coll.findOne({ key });
    if (!existing) return null;
    const row = existing as Record<string, unknown>;
    if (String(row.requestHash) !== requestHash) {
      throw new ConflictException(
        'Idempotency key already used with a different payload.',
      );
    }
    return {
      txnId: String(row.txnId),
      duplicate: true,
      type: row.type ?? null,
    };
  }

  private async reserveIdempotency(
    ctx: WmsRequestContext,
    session: ClientSession,
    key: string,
    requestHash: string,
    txnId: string,
    now: Date,
  ) {
    const coll = await this.collections.idempotencyKeys(ctx.tenantUri);
    try {
      await coll.insertOne(
        {
          key,
          requestHash,
          txnId,
          status: 'pending',
          createdAt: now,
          expiresAt: new Date(now.getTime() + 7 * 24 * 60 * 60 * 1000),
        },
        { session },
      );
    } catch (e: unknown) {
      if (this.isDup(e)) {
        const existing = await coll.findOne({ key }, { session });
        if (existing && String((existing as Record<string, unknown>).requestHash) !== requestHash) {
          throw new ConflictException(
            'Idempotency key already used with a different payload.',
          );
        }
        throw new ConflictException('Duplicate idempotency key.');
      }
      throw e;
    }
  }

  private async finalizeIdempotency(
    ctx: WmsRequestContext,
    session: ClientSession,
    key: string,
    txnId: string,
  ) {
    const coll = await this.collections.idempotencyKeys(ctx.tenantUri);
    await coll.updateOne({ key }, { $set: { status: 'completed', txnId } }, { session });
  }

  private assertTypePermission(ctx: WmsRequestContext, type: WmsStockTxnType) {
    const adminOnly: WmsStockTxnType[] = [
      'openingBalance',
      'adjust',
      'reversal',
      'ownerTransfer',
    ];
    if (type === 'writeOff') {
      if (ctx.isWmsAdmin || ctx.operatorPermissions.supervise) return;
      assertWmsAdminWrite(ctx);
      return;
    }
    // receive, move, qcBlock, qcRelease allowed for operators with domain permissions (checked in Phase 03 services).
    if (adminOnly.includes(type)) {
      assertWmsAdminWrite(ctx);
    }
  }

  private mapErpEventType(type: WmsStockTxnType): string {
    switch (type) {
      case 'receive':
        return 'receiptPosted';
      case 'adjust':
      case 'writeOff':
        return 'stockAdjusted';
      case 'ownerTransfer':
        return 'ownerTransferPosted';
      default:
        return 'inventoryTxnPosted';
    }
  }

  private toObjectId(id: string) {
    if (!Types.ObjectId.isValid(id)) {
      throw new BadRequestException('Invalid object id.');
    }
    return new Types.ObjectId(id);
  }

  private isDup(e: unknown): boolean {
    return (
      typeof e === 'object' &&
      e !== null &&
      'code' in e &&
      (e as { code?: number }).code === 11000
    );
  }

  private isVersionConflict(e: unknown): boolean {
    if (e instanceof Error && e.message === 'VERSION_CONFLICT') return true;
    return (
      typeof e === 'object' &&
      e !== null &&
      'code' in e &&
      (e as { code?: string }).code === 'VERSION_CONFLICT'
    );
  }
}