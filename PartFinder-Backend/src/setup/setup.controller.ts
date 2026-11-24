import { Body, Controller, Get, Post, Query, Res } from '@nestjs/common';
import type { Response } from 'express';
import { SetupCustomDatabaseDto } from './dto/setup-custom-database.dto';
import { SetupInviteLoginDto } from './dto/setup-invite-login.dto';
import { SetupInviteUserDto } from './dto/setup-invite-user.dto';
import { SetupOrgAdminDto } from './dto/setup-org-admin.dto';
import { SetupOrgCodeDto } from './dto/setup-org-code.dto';
import { SetupTestDatabaseDto } from './dto/setup-test-database.dto';
import { SetupChangePasswordDto } from './dto/setup-change-password.dto';
import { SetupForgotPasswordDto } from './dto/setup-forgot-password.dto';
import { SetupResetPasswordDto } from './dto/setup-reset-password.dto';
import {
  SetupRefreshSessionDto,
  SetupRevokeSessionDto,
} from './dto/setup-refresh-session.dto';
import { buildPasswordResetHtmlPage } from '../common/password-reset-page.util';
import { resolvePasswordResetApiUrl } from '../common/password-reset-link.util';
import { ConfigService } from '@nestjs/config';
import { SetupService } from './setup.service';

/** Public endpoints for PartFinder desktop setup wizard (org code is the shared secret). */
@Controller('public/setup')
export class SetupController {
  constructor(
    private readonly setupService: SetupService,
    private readonly config: ConfigService,
  ) {}

  @Post('status')
  status(@Body() dto: SetupOrgCodeDto) {
    return this.setupService.status(dto.orgCode);
  }

  @Post('database/provision-default')
  provisionDefault(@Body() dto: SetupOrgCodeDto) {
    return this.setupService.provisionDefaultDatabase(dto.orgCode);
  }

  @Post('database/save-custom')
  saveCustom(@Body() dto: SetupCustomDatabaseDto) {
    return this.setupService.saveCustomDatabase(dto);
  }

  @Post('database/test')
  testDatabase(@Body() dto: SetupTestDatabaseDto) {
    return this.setupService.testDatabase(dto.orgCode, dto.uri);
  }

  @Post('org-admin')
  createOrgAdmin(@Body() dto: SetupOrgAdminDto) {
    return this.setupService.createOrgAdmin(dto);
  }

  @Post('invite-user')
  inviteUser(@Body() dto: SetupInviteUserDto) {
    return this.setupService.inviteOrgUser(dto);
  }

  @Post(['list-users', 'list-org-users'])
  listOrgUsers(@Body() dto: SetupOrgCodeDto) {
    return this.setupService.listOrgUsers(dto.orgCode);
  }

  @Post('invite-login')
  inviteLogin(@Body() dto: SetupInviteLoginDto) {
    return this.setupService.validateInviteLogin(dto);
  }

  @Post('refresh-session')
  refreshSession(@Body() dto: SetupRefreshSessionDto) {
    return this.setupService.refreshDeviceSession(dto);
  }

  @Post('revoke-session')
  revokeSession(@Body() dto: SetupRevokeSessionDto) {
    return this.setupService.revokeDeviceSession(dto);
  }

  @Post('change-password')
  changePassword(@Body() dto: SetupChangePasswordDto) {
    return this.setupService.changePassword(dto);
  }

  @Post('forgot-password')
  forgotPassword(@Body() dto: SetupForgotPasswordDto) {
    return this.setupService.requestPasswordReset(dto);
  }

  @Post('reset-password')
  resetPassword(@Body() dto: SetupResetPasswordDto) {
    return this.setupService.resetPasswordWithToken(dto);
  }

  @Get('reset-password/form')
  resetPasswordForm(@Query('token') token: string, @Res() res: Response) {
    const apiUrl = resolvePasswordResetApiUrl(this.config);
    const html = buildPasswordResetHtmlPage(
      typeof token === 'string' ? token : '',
      apiUrl,
    );
    res.type('html').send(html);
  }
}
