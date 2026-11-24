import { INestApplication, Logger, ValidationPipe } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { HttpAdapterHost } from '@nestjs/core';
import { MongoConnectivityExceptionFilter } from './common/filters/mongo-connectivity-exception.filter';
import { backendEnvFilePaths } from './config/backend-env';
import { AggregatingLogger } from './debug/aggregating-logger';
import { DebugLogsService } from './debug/debug-logs.service';
import {
  isLicensePurchaseEnabled,
  isRazorpayConfigured,
  isRazorpayTestKeyId,
  razorpayConfigurationMessage,
  readRazorpayKeys,
} from './license-purchase/license-purchase.config';
import { normalizeMongoUri } from './mongo-uri';

export async function configureApplication(app: INestApplication): Promise<void> {
  const debugLogs = app.get(DebugLogsService);
  app.useLogger(new AggregatingLogger(debugLogs));
  const config = app.get(ConfigService);

  assertRequiredEnvironment(config);

  const corsOrigin = config.get<string>('CORS_ORIGIN') ?? '';
  const origins = corsOrigin
    .split(',')
    .map((o: string) => o.trim())
    .filter((o: string) => Boolean(o) && o !== '*');

  app.enableCors({
    origin: origins.length ? origins : true,
    credentials: true,
  });

  app.setGlobalPrefix('api');

  const httpAdapterHost = app.get(HttpAdapterHost);
  app.useGlobalFilters(
    new MongoConnectivityExceptionFilter(httpAdapterHost.httpAdapter),
  );

  app.useGlobalPipes(
    new ValidationPipe({
      whitelist: true,
      forbidNonWhitelisted: true,
      transform: true,
    }),
  );

  const boot = new Logger('Bootstrap');
  boot.log(`Env files: ${backendEnvFilePaths().join(', ')}`);
  const { keyId } = readRazorpayKeys(config);
  boot.log(
    `Licence checkout: enabled=${isLicensePurchaseEnabled(config)} razorpayConfigured=${isRazorpayConfigured(config)} keyId=${keyId ? (isRazorpayTestKeyId(keyId) ? 'test' : 'live') : 'missing'}`,
  );
  const razorpayNote = razorpayConfigurationMessage(config);
  if (razorpayNote) {
    boot.warn(razorpayNote);
  }
}

function assertRequiredEnvironment(config: ConfigService): void {
  const missing: string[] = [];
  const mongo = normalizeMongoUri(config.get<string>('MONGODB_URI'));
  if (!mongo || (!mongo.startsWith('mongodb://') && !mongo.startsWith('mongodb+srv://'))) {
    missing.push('MONGODB_URI');
  }

  if (!config.get<string>('JWT_SECRET')?.trim()) {
    missing.push('JWT_SECRET');
  }

  if (missing.length === 0) {
    return;
  }

  const where = process.env.VERCEL
    ? 'Vercel → Project Settings → Environment Variables (apply to Production), then redeploy.'
    : 'PartFinder-Backend/.env (see .env.example).';

  throw new Error(
    `PartFinder-Backend cannot start. Missing or invalid: ${missing.join(', ')}. Configure ${where}`,
  );
}