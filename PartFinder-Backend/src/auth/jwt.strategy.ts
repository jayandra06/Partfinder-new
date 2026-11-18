import { Injectable } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { PassportStrategy } from '@nestjs/passport';
import { ExtractJwt, Strategy } from 'passport-jwt';

export type AccessJwtPayload = {
  sub: string;
  email: string;
  orgCode?: string;
  purpose?: string;
};

@Injectable()
export class JwtStrategy extends PassportStrategy(Strategy, 'jwt') {
  constructor(config: ConfigService) {
    super({
      jwtFromRequest: ExtractJwt.fromAuthHeaderAsBearerToken(),
      ignoreExpiration: false,
      secretOrKey: config.getOrThrow<string>('JWT_SECRET'),
    });
  }

  validate(payload: AccessJwtPayload) {
    const orgCode =
      typeof payload.orgCode === 'string' ? payload.orgCode.trim() : undefined;
    return {
      userId: payload.sub,
      email: payload.email,
      orgCode,
      purpose: payload.purpose,
    };
  }
}
