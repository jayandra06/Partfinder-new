import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { AuthModule } from '../auth/auth.module';
import { RelationDisplayColumn, RelationDisplayColumnSchema } from '../relations/schemas/relation-display-column.schema';
import { RelationMatchKey, RelationMatchKeySchema } from '../relations/schemas/relation-match-key.schema';
import { WorksheetRelation, WorksheetRelationSchema } from '../relations/schemas/worksheet-relation.schema';
import { TemplateColumn, TemplateColumnSchema } from '../templates/schemas/template-column.schema';
import { TemplateCell, TemplateCellSchema } from '../templates/schemas/template-cell.schema';
import { TemplateRow, TemplateRowSchema } from '../templates/schemas/template-row.schema';
import { Template, TemplateSchema } from '../templates/schemas/template.schema';
import { ViewDataController } from './view-data.controller';
import { ViewDataService } from './view-data.service';

@Module({
  imports: [
    AuthModule,
    MongooseModule.forFeature([
      { name: Template.name, schema: TemplateSchema },
      { name: TemplateColumn.name, schema: TemplateColumnSchema },
      { name: TemplateRow.name, schema: TemplateRowSchema },
      { name: TemplateCell.name, schema: TemplateCellSchema },
      { name: WorksheetRelation.name, schema: WorksheetRelationSchema },
      { name: RelationMatchKey.name, schema: RelationMatchKeySchema },
      { name: RelationDisplayColumn.name, schema: RelationDisplayColumnSchema },
    ]),
  ],
  controllers: [ViewDataController],
  providers: [ViewDataService],
})
export class ViewDataModule {}
