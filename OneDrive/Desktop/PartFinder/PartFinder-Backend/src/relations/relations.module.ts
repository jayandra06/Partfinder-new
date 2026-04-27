import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { AuthModule } from '../auth/auth.module';
import { RelationDisplayColumn, RelationDisplayColumnSchema } from './schemas/relation-display-column.schema';
import { RelationMatchKey, RelationMatchKeySchema } from './schemas/relation-match-key.schema';
import { WorksheetRelation, WorksheetRelationSchema } from './schemas/worksheet-relation.schema';
import { RelationsController } from './relations.controller';
import { RelationsService } from './relations.service';

@Module({
  imports: [
    AuthModule,
    MongooseModule.forFeature([
      { name: WorksheetRelation.name, schema: WorksheetRelationSchema },
      { name: RelationMatchKey.name, schema: RelationMatchKeySchema },
      { name: RelationDisplayColumn.name, schema: RelationDisplayColumnSchema },
    ]),
  ],
  controllers: [RelationsController],
  providers: [RelationsService],
})
export class RelationsModule {}
