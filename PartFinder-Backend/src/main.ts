import './test-utils/mongodb-atlas-dns';
import { Logger, ValidationPipe } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { NestFactory } from '@nestjs/core';
import { ExpressAdapter } from '@nestjs/platform-express';
import express, { type Express } from 'express';
import { AppModule } from './app.module';
import { configureApplication } from './configure-application';

let cachedExpressServer: Express | undefined;

export async function createExpressServer(): Promise<Express> {
  if (cachedExpressServer) {
    return cachedExpressServer;
  }

  const expressApp = express();
  const app = await NestFactory.create(
    AppModule,
    new ExpressAdapter(expressApp),
    { bufferLogs: true },
  );
  await configureApplication(app);
  await app.init();
  cachedExpressServer = expressApp;
  return expressApp;
}

async function bootstrap(): Promise<void> {
  const app = await NestFactory.create(AppModule, { bufferLogs: true });
  await configureApplication(app);
  const config = app.get(ConfigService);
  const port = config.get<number>('PORT') ?? 3000;
  await app.listen(port);
  new Logger('Bootstrap').log(`Listening on http://127.0.0.1:${port}`);
}

if (!process.env.VERCEL) {
  void bootstrap();
}

/** Vercel serverless entry — do not call listen(). */
export default async function handler(
  req: express.Request,
  res: express.Response,
): Promise<void> {
  const server = await createExpressServer();
  server(req, res);
}