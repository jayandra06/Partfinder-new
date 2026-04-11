import { Body, Controller, Post } from '@nestjs/common';
import { AuthService } from './auth.service';
import { AdminBootstrapDto } from './dto/bootstrap.dto';
import { AdminLoginDto } from './dto/login.dto';

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
}