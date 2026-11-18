import 'reflect-metadata';
import { NestFactory } from '@nestjs/core';
import { getModelToken } from '@nestjs/mongoose';
import type { Model } from 'mongoose';
import { AppModule } from '../src/app.module';
import { Organization } from '../src/organizations/schemas/organization.schema';

async function run() {
  const app = await NestFactory.createApplicationContext(AppModule, {
    logger: ['error'],
  });
  try {
    const model = app.get<Model<Organization>>(getModelToken(Organization.name));
    const rows = await model
      .find({ orgDatabaseUri: { $exists: true, $ne: null } })
      .select('orgCode name wmsEnabled status plan')
      .limit(30)
      .exec();
    for (const o of rows) {
      const hasUri = Boolean(o.orgDatabaseUri?.trim());
      console.log(
        `${o.orgCode}  wms=${o.wmsEnabled === true}  hasTenantUri=${hasUri}  ${o.name ?? ''}  [${o.status}]`,
      );
    }
  } finally {
    await app.close();
  }
}

run();