import { Body, Controller, Post, Req, UseGuards } from '@nestjs/common';
import { AuthGuard } from '@nestjs/passport';
import { Request } from 'express';
import { AuthService } from './auth.service';
import { AdminBootstrapDto } from './dto/bootstrap.dto';
import { ChangeAdminPasswordDto } from './dto/change-admin-password.dto';
import { CreateAdminUserDto } from './dto/create-admin-user.dto';
import { AdminLoginDto } from './dto/login.dto';

type AuthedRequest = Request & { user: { userId: string; email: string } };

@Controller('auth')
export class AuthController {
  constructor(private readonly authService: AuthService) {}

  @Post('admin/login')
  login(@Body() dto: AdminLoginDto) {
    return this.authService.login(dto);
  }

  @Post('admin/bootstrap')
  bootstrap(@Body() dto: AdminBootstrapDto) {
    return this.authService.bootstrapFirstAdmin(dto);
  }

  @Post('admin/change-password')
  @UseGuards(AuthGuard('jwt'))
  changePassword(
    @Req() req: AuthedRequest,
    @Body() dto: ChangeAdminPasswordDto,
  ) {
    return this.authService.changeAdminPassword(req.user.userId, dto);
  }

  @Post('admin/admins')
  @UseGuards(AuthGuard('jwt'))
  createAdmin(@Body() dto: CreateAdminUserDto) {
    return this.authService.createAdminUser(dto);
  }
}