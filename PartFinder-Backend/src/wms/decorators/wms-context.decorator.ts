import { createParamDecorator, ExecutionContext } from '@nestjs/common';
import type { WmsRequestContext } from '../wms.types';

export const WmsCtx = createParamDecorator(
  (_data: unknown, ctx: ExecutionContext): WmsRequestContext => {
    const req = ctx.switchToHttp().getRequest<{ wmsContext?: WmsRequestContext }>();
    if (!req.wmsContext) {
      throw new Error('WmsSessionGuard must run before WmsCtx');
    }
    return req.wmsContext;
  },
);