import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { CasbinModule } from '../casbin/casbin.module';
import { RedisModule } from '../common/redis/redis.module';
import { OrganizationsModule } from '../organizations/organizations.module';
import { PartsModule } from '../parts/parts.module';
import { RelationsModule } from '../relations/relations.module';
import { TemplatesModule } from '../templates/templates.module';
import { ViewDataModule } from '../view-data/view-data.module';
import { WmsModule } from '../wms/wms.module';
import { AiController } from './ai.controller';
import { AiOrchestratorService } from './ai-orchestrator.service';
import { ConversationService } from './conversation.service';
import { LlmGatewayService } from './llm-gateway.service';
import {
  AiConversation,
  AiConversationSchema,
} from './schemas/ai-conversation.schema';
import { AiToolAuthService } from './tools/ai-tool-auth.service';
import { AiToolRegistryService } from './tools/ai-tool-registry.service';

@Module({
  imports: [
    MongooseModule.forFeature([
      { name: AiConversation.name, schema: AiConversationSchema },
    ]),
    RedisModule,
    CasbinModule,
    OrganizationsModule,
    TemplatesModule,
    ViewDataModule,
    RelationsModule,
    PartsModule,
    WmsModule,
  ],
  controllers: [AiController],
  providers: [
    LlmGatewayService,
    ConversationService,
    AiOrchestratorService,
    AiToolAuthService,
    AiToolRegistryService,
  ],
  exports: [
    AiOrchestratorService,
    ConversationService,
    LlmGatewayService,
    AiToolRegistryService,
  ],
})
export class AiModule {}
