import { ValidationPipe } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { HttpAdapterHost, NestFactory } from '@nestjs/core';
import { AppModule } from './app.module';
import { MongoConnectivityExceptionFilter } from './common/filters/mongo-connectivity-exception.filter';
import { AggregatingLogger } from './debug/aggregating-logger';
import { DebugLogsService } from './debug/debug-logs.service';

async function bootstrap() {
  const app = await NestFactory.create(AppModule, { bufferLogs: true });
  const debugLogs = app.get(DebugLogsService);
  app.useLogger(new AggregatingLogger(debugLogs));
  const config = app.get(ConfigService);

  const corsOrigin = config.get<string>('CORS_ORIGIN') ?? 'http://localhost:5173';
  const origins = corsOrigin.split(',').map((o) => o.trim()).filter(Boolean);

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

  const port = config.get<number>('PORT') ?? 3000;
  await app.listen(port);
}
bootstrap();
