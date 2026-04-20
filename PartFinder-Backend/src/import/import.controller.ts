import { Body, Controller, Get, Headers, Param, Post, UploadedFile, UseGuards, UseInterceptors } from '@nestjs/common';
import { AuthGuard } from '@nestjs/passport';
import { FileInterceptor } from '@nestjs/platform-express';
import { ImportService } from './import.service';

@Controller('templates')
@UseGuards(AuthGuard('jwt'))
export class ImportController {
  constructor(private readonly importService: ImportService) {}

  @Post(':id/import')
  @UseInterceptors(FileInterceptor('file'))
  async importCsv(
    @Headers('x-org-id') orgId: string,
    @Param('id') templateId: string,
    @UploadedFile() file: { buffer?: Buffer },
    @Body('headerMap') headerMapRaw?: string,
  ) {
    if (!file?.buffer || !file.buffer.length) {
      return { data: null, success: false, message: 'CSV file is required.' };
    }

    const headerMap = this.parseHeaderMap(headerMapRaw);
    const data = await this.importService.enqueueImport(orgId, templateId, file.buffer, headerMap);
    return { data, success: true, message: 'Import job started' };
  }

  @Get(':id/import/status')
  async getStatus(@Headers('x-org-id') orgId: string, @Param('id') templateId: string) {
    const data = await this.importService.getStatus(orgId, templateId);
    return { data, success: true, message: 'Import status fetched' };
  }

  private parseHeaderMap(raw?: string): Record<string, string> {
    if (!raw?.trim()) {
      return {};
    }

    try {
      const parsed = JSON.parse(raw);
      if (parsed && typeof parsed === 'object') {
        return parsed as Record<string, string>;
      }
    } catch {
      return {};
    }

    return {};
  }
}
