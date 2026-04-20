import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { AuthModule } from '../auth/auth.module';
import { Part, PartSchema } from './schemas/part.schema';
import { PartsController } from './parts.controller';
import { PartsService } from './parts.service';

@Module({
  imports: [
    AuthModule,
    MongooseModule.forFeature([{ name: Part.name, schema: PartSchema }]),
  ],
  controllers: [PartsController],
  providers: [PartsService],
  exports: [PartsService],
})
export class PartsModule {}
