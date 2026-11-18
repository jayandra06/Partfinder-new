import { Type } from 'class-transformer';
import {
  ArrayMinSize,
  IsArray,
  IsBoolean,
  IsDateString,
  IsIn,
  IsNumber,
  IsOptional,
  IsString,
  MaxLength,
  Min,
  MinLength,
  ValidateNested,
} from 'class-validator';

export class CreateExpectedReceiptDto {
  @IsString()
  ownerId!: string;

  @IsString()
  warehouseId!: string;

  @IsOptional()
  @IsString()
  externalPoId?: string;

  @IsOptional()
  @IsString()
  supplierDeliveryNote?: string;

  @IsOptional()
  @IsString()
  erpConnectorId?: string;

  @IsOptional()
  @IsString()
  supplierId?: string;

  @IsOptional()
  @IsString()
  @MaxLength(500)
  notes?: string;
}

export class ExpectedReceiptLineDto {
  @IsString()
  skuId!: string;

  @IsNumber()
  @Min(0.000001)
  qtyExpected!: number;

  @IsOptional()
  @IsString()
  primaryUom?: string;
}

export class AddExpectedReceiptLinesDto {
  @IsArray()
  @ArrayMinSize(1)
  @ValidateNested({ each: true })
  @Type(() => ExpectedReceiptLineDto)
  lines!: ExpectedReceiptLineDto[];
}

export class ReceiptLineInputDto {
  @IsString()
  skuId!: string;

  @IsNumber()
  @Min(0)
  qtyReceived!: number;

  @IsOptional()
  @IsString()
  lotId?: string;

  @IsOptional()
  @IsString()
  serialId?: string;

  @IsOptional()
  @IsDateString()
  expiryDate?: string;

  @IsOptional()
  @IsBoolean()
  damaged?: boolean;

  @IsOptional()
  @IsBoolean()
  shortage?: boolean;

  @IsOptional()
  @IsBoolean()
  overDelivery?: boolean;

  @IsOptional()
  @IsString()
  receiptLineId?: string;
}

export class CreateGoodsReceiptDto {
  @IsString()
  @MinLength(8)
  idempotencyKey!: string;

  @IsString()
  ownerId!: string;

  @IsString()
  warehouseId!: string;

  @IsString()
  receivingLocationId!: string;

  @IsOptional()
  @IsString()
  expectedReceiptId?: string;

  @IsOptional()
  @IsString()
  externalPoId?: string;

  @IsOptional()
  @IsString()
  supplierDeliveryNote?: string;

  @IsArray()
  @ArrayMinSize(1)
  @ValidateNested({ each: true })
  @Type(() => ReceiptLineInputDto)
  lines!: ReceiptLineInputDto[];
}

export class QcDecisionDto {
  @IsString()
  @IsIn(['accepted', 'rejected', 'quarantined', 'released', 'partial'])
  status!: 'accepted' | 'rejected' | 'quarantined' | 'released' | 'partial';

  @IsOptional()
  @IsNumber()
  @Min(0)
  qtyAccepted?: number;

  @IsOptional()
  @IsNumber()
  @Min(0)
  qtyRejected?: number;

  @IsOptional()
  @IsNumber()
  @Min(0)
  qtyQuarantined?: number;

  @IsOptional()
  @IsString()
  @MaxLength(500)
  note?: string;

  @IsOptional()
  @IsString()
  approvalNote?: string;
}

export class ConfirmPhysicalDispositionDto {
  @IsString()
  @MinLength(8)
  idempotencyKey!: string;

  @IsNumber()
  @Min(0.000001)
  quantity!: number;

  @IsString()
  @MinLength(3)
  dispositionReason!: string;

  @IsString()
  @IsIn(['return_to_supplier', 'scrap', 'other'])
  dispositionType!: 'return_to_supplier' | 'scrap' | 'other';
}

export class CompletePutawayDto {
  @IsString()
  @MinLength(8)
  idempotencyKey!: string;

  @IsString()
  toLocationId!: string;
}

export class RecordStagingPreflightDto {
  @IsOptional()
  @IsString()
  tenantLabel?: string;
}