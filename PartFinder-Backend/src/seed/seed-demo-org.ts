import 'reflect-metadata';
import { NestFactory } from '@nestjs/core';
import { getModelToken } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { AppModule } from '../app.module';
import { computeValidity } from '../organizations/org.constants';
import { Organization } from '../organizations/schemas/organization.schema';

/** Fixed 6-digit code for prospects; override with SEED_DEMO_ORG_CODE */
const DEFAULT_DEMO_CODE = '900001';

async function run() {
  const app = await NestFactory.createApplicationContext(AppModule, {
    logger: ['error', 'warn', 'log'],
  });
  try {
    const orgCode = (process.env.SEED_DEMO_ORG_CODE ?? DEFAULT_DEMO_CODE).trim();
    if (!/^\d{6}$/.test(orgCode)) {
      throw new Error('SEED_DEMO_ORG_CODE must be exactly 6 digits');
    }
    const model = app.get<Model<Organization>>(getModelToken(Organization.name));
    const existing = await model.findOne({ orgCode }).exec();
    if (existing) {
      console.log(`Demo org already exists: ${orgCode}`);
      return;
    }
    await model.create({
      orgCode,
      name: 'Demo (share this code with prospects - 1 day)',
      type: 'standard',
      plan: 'demo',
      validity: computeValidity('demo'),
      status: 'Active',
    });
    console.log(`Seeded demo organization. Code: ${orgCode} (demo plan, ~1 day validity)`);
  } finally {
    await app.close();
  }
}

run().catch((err) => {
  console.error(err);
  process.exit(1);
});