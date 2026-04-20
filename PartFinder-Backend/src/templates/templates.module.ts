import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { AuthModule } from '../auth/auth.module';
import { TemplateColumn, TemplateColumnSchema } from './schemas/template-column.schema';
import { TemplateCell, TemplateCellSchema } from './schemas/template-cell.schema';
import { TemplateRow, TemplateRowSchema } from './schemas/template-row.schema';
import { Template, TemplateSchema } from './schemas/template.schema';
import { TemplatesController } from './templates.controller';
import { TemplatesService } from './templates.service';

@Module({
  imports: [
    AuthModule,
    MongooseModule.forFeature([
      { name: Template.name, schema: TemplateSchema },
      { name: TemplateColumn.name, schema: TemplateColumnSchema },
      { name: TemplateRow.name, schema: TemplateRowSchema },
      { name: TemplateCell.name, schema: TemplateCellSchema },
    ]),
  ],
  controllers: [TemplatesController],
  providers: [TemplatesService],
  exports: [TemplatesService],
})
export class TemplatesModule {}
