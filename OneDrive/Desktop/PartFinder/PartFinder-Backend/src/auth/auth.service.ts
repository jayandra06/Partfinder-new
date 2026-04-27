import {
  BadRequestException,
  ConflictException,
  Injectable,
  UnauthorizedException,
} from '@nestjs/common';
import { JwtService } from '@nestjs/jwt';
import { UsersService } from '../users/users.service';
import { AdminBootstrapDto } from './dto/bootstrap.dto';
import { ChangeAdminPasswordDto } from './dto/change-admin-password.dto';
import { CreateAdminUserDto } from './dto/create-admin-user.dto';
import { AdminLoginDto } from './dto/login.dto';
import { AdminResetPasswordWithTotpDto } from './dto/admin-reset-password-with-totp.dto';
import { AdminSyncTotpDto } from './dto/admin-sync-totp.dto';
import { isPlausibleTotpSecretBase32, verifyTotp } from './totp.util';

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

  async changeAdminPassword(userId: string, dto: ChangeAdminPasswordDto) {
    const user = await this.usersService.findById(userId);
    if (!user) {
      throw new UnauthorizedException();
    }
    const ok = await this.usersService.validatePassword(
      user,
      dto.currentPassword,
    );
    if (!ok) {
      throw new BadRequestException('Current password is incorrect');
    }
    await this.usersService.setPassword(userId, dto.newPassword);
    return { ok: true as const };
  }

  async createAdminUser(dto: CreateAdminUserDto) {
    const existing = await this.usersService.findByEmail(dto.email);
    if (existing) {
      throw new ConflictException('An admin with this email already exists');
    }
    const user = await this.usersService.createAdmin(dto.email, dto.password);
    return { user: { id: user.id, email: user.email } };
  }

  async syncAdminTotp(userId: string, dto: AdminSyncTotpDto) {
    if (!isPlausibleTotpSecretBase32(dto.secretBase32)) {
      throw new BadRequestException('Invalid authenticator secret');
    }
    await this.usersService.setTotpSecret(userId, dto.secretBase32.trim());
    return { ok: true as const };
  }

  async clearAdminTotp(userId: string) {
    await this.usersService.clearTotp(userId);
    return { ok: true as const };
  }

  async resetPasswordWithTotp(dto: AdminResetPasswordWithTotpDto) {
    const user = await this.usersService.findByEmail(dto.email);
    if (
      !user ||
      !user.totpEnabled ||
      !user.totpSecretBase32 ||
      !verifyTotp(user.totpSecretBase32, dto.totpCode)
    ) {
      throw new UnauthorizedException('Recovery failed');
    }
    this.assertStrongPassword(dto.newPassword);
    await this.usersService.setPassword(user.id, dto.newPassword);
    return { ok: true as const };
  }

  private assertStrongPassword(password: string) {
    if (password.length < 8) {
      throw new BadRequestException(
        'New password must be at least 8 characters',
      );
    }
    const hasUpper = /[A-Z]/.test(password);
    const hasLower = /[a-z]/.test(password);
    const hasDigit = /\d/.test(password);
    if (!hasUpper || !hasLower || !hasDigit) {
      throw new BadRequestException(
        'New password must include upper, lower, and a number',
      );
    }
  }
}