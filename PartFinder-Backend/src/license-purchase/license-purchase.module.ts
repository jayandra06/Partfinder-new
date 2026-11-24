import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { OrganizationsModule } from '../organizations/organizations.module';
import { LicensePurchaseController } from './license-purchase.controller';
import { LicensePurchaseService } from './license-purchase.service';
import {
  LicensePurchase,
  LicensePurchaseSchema,
} from './schemas/license-purchase.schema';

@Module({
  imports: [
    MongooseModule.forFeature([
      { name: LicensePurchase.name, schema: LicensePurchaseSchema },
    ]),
    OrganizationsModule,
  ],
  controllers: [LicensePurchaseController],
  providers: [LicensePurchaseService],
})
export class LicensePurchaseModule {}
