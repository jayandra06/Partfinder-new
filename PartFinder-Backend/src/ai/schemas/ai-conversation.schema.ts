import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';

export type AiConversationDocument = HydratedDocument<AiConversation>;

export const ALLOWED_AI_UI_ACTION_PAGES = new Set([
  'Dashboard',
  'MasterData',
  'Parts',
  'Inventory',
  'WmsWorkbook',
  'Templates',
  'TemplateData',
  'TemplateDataEdit',
  'WorksheetRelations',
  'LinkedTemplates',
  'ViewData',
  'UserManagement',
  'Settings',
  'QrCodeManager',
  'Alerts',
  'Audit',
]);

export type AiUiAction =
  | {
      type: 'navigate';
      page: string;
      templateId?: string;
    }
  | {
      type: 'export';
      exportPayload: Record<string, unknown>;
    };

export interface AiToolExecutionMeta {
  tool: string;
  ok: boolean;
  returnedCount: number;
  durationMs: number;
}

@Schema({ _id: false })
export class AiMessageSubdoc {
  @Prop({ required: true, enum: ['user', 'assistant'] })
  role!: 'user' | 'assistant';

  @Prop({ required: true })
  content!: string;

  @Prop({ required: true, default: () => new Date() })
  timestampUtc!: Date;

  @Prop({ type: [Object], default: undefined })
  uiActions?: AiUiAction[];

  @Prop({ type: [Object], default: undefined })
  toolExecutions?: AiToolExecutionMeta[];
}

export const AiMessageSubdocSchema =
  SchemaFactory.createForClass(AiMessageSubdoc);

@Schema({ collection: 'ai_conversations', timestamps: true })
export class AiConversation {
  @Prop({ required: true, index: true })
  orgId!: string;

  @Prop({ required: true, index: true })
  userId!: string;

  @Prop({ required: true })
  userEmail!: string;

  @Prop({ required: true, default: 'New conversation' })
  title!: string;

  @Prop({ type: [AiMessageSubdocSchema], default: [] })
  messages!: AiMessageSubdoc[];

  @Prop({ type: String, default: '' })
  contextSummary?: string;

  @Prop({ type: Number, default: 0 })
  summarizedMessageCount?: number;

  @Prop({ type: Number, default: 0 })
  summaryVersion?: number;

  createdAt?: Date;
  updatedAt?: Date;
}

export const AiConversationSchema =
  SchemaFactory.createForClass(AiConversation);

AiConversationSchema.index({ orgId: 1, userId: 1, updatedAt: -1 });
