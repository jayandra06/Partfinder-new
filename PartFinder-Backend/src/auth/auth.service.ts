import {
  ConflictException,
  Injectable,
  UnauthorizedException,
} from '@nestjs/common';
import { JwtService } from '@nestjs/jwt';
import { UsersService } from '../users/users.service';
import { AdminBootstrapDto } from './dto/bootstrap.dto';
import { AdminLoginDto } from './dto/login.dto';

export type JwtPayload = { sub: string; email: string };

@Injectable()
export class AuthService {
  constructor(
    private readonly usersService: UsersService,
    private readonly jwtService: JwtService,
  ) {}

  async login(dto: AdminLoginDto) {
    const user = await this.usersService.findByEmail(dto.email);
    if (!user) {
      throw new UnauthorizedException('Invalid email or password');
    }
    const ok = await this.usersService.validatePassword(user, dto.password);
    if (!ok) {
      throw new UnauthorizedException('Invalid email or password');
    }
    const payload: JwtPayload = { sub: user.id, email: user.email };
    return {
      accessToken: await this.jwtService.signAsync(payload),
      user: { id: user.id, email: user.email },
    };
  }

  async bootstrapFirstAdmin(dto: AdminBootstrapDto) {
    const count = await this.usersService.countAdmins();
    if (count > 0) {
      throw new ConflictException(
        'An admin user already exists. Use login instead.',
      );
    }
    const user = await this.usersService.createAdmin(dto.email, dto.password);
    const payload: JwtPayload = { sub: user.id, email: user.email };
    return {
      accessToken: await this.jwtService.signAsync(payload),
      user: { id: user.id, email: user.email },
    };
  }
}