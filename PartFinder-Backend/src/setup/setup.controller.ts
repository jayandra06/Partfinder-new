import { Body, Controller, Post } from '@nestjs/common';
import { SetupCustomDatabaseDto } from './dto/setup-custom-database.dto';
import { SetupInviteLoginDto } from './dto/setup-invite-login.dto';
import { SetupInviteUserDto } from './dto/setup-invite-user.dto';
import { SetupOrgAdminDto } from './dto/setup-org-admin.dto';
import { SetupOrgCodeDto } from './dto/setup-org-code.dto';
import { SetupTestDatabaseDto } from './dto/setup-test-database.dto';
import { SetupService } from './setup.service';

/** Public endpoints for PartFinder desktop setup wizard (org code is the shared secret). */
@Controller('public/setup')
export class SetupController {
  constructor(private readonly setupService: SetupService) {}

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

  @Post('invite-login')
  inviteLogin(@Body() dto: SetupInviteLoginDto) {
    return this.setupService.validateInviteLogin(dto);
  }
}
