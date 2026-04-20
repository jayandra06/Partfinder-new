import {
  BadRequestException,
  Injectable,
} from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { UpdateMaintenanceDto } from './dto/update-maintenance.dto';
import {
  PlatformSettings,
  PlatformSettingsDocument,
} from './schemas/platform-settings.schema';

export type EffectiveMaintenance = {
  active: boolean;
  maintenanceUntil: string | null;
};

@Injectable()
export class PlatformSettingsService {
  constructor(
    @InjectModel(PlatformSettings.name)
    private readonly model: Model<PlatformSettingsDocument>,
  ) {}

  getEffectiveMaintenance(): Promise<EffectiveMaintenance> {
    return this.computeEffective();
  }

  private async computeEffective(): Promise<EffectiveMaintenance> {
    const doc = await this.model.findOne({ key: 'singleton' }).exec();
    if (!doc?.maintenanceEnabled || !doc.maintenanceUntil) {
      return { active: false, maintenanceUntil: null };
    }
    const until =
      doc.maintenanceUntil instanceof Date
        ? doc.maintenanceUntil
        : new Date(doc.maintenanceUntil);
    if (Number.isNaN(until.getTime())) {
      return { active: false, maintenanceUntil: null };
    }
    if (Date.now() >= until.getTime()) {
      return { active: false, maintenanceUntil: null };
    }
    return { active: true, maintenanceUntil: until.toISOString() };
  }

  async getRawForAdmin() {
    const doc = await this.model.findOne({ key: 'singleton' }).exec();
    const enabled = doc?.maintenanceEnabled ?? false;
    const raw = doc?.maintenanceUntil;
    const until =
      raw == null
        ? null
        : raw instanceof Date
          ? raw
          : new Date(raw);
    const untilIso =
      until && !Number.isNaN(until.getTime()) ? until.toISOString() : null;
    return {
      maintenanceEnabled: enabled,
      maintenanceUntil: untilIso,
    };
  }

  async updateMaintenance(dto: UpdateMaintenanceDto) {
    if (dto.maintenanceEnabled) {
      if (!dto.maintenanceUntil?.trim()) {
        throw new BadRequestException(
          'maintenanceUntil is required when enabling maintenance.',
        );
      }
      const until = new Date(dto.maintenanceUntil);
      if (Number.isNaN(until.getTime())) {
        throw new BadRequestException('maintenanceUntil is not a valid date.');
      }
      if (until.getTime() <= Date.now()) {
        throw new BadRequestException(
          'maintenanceUntil must be in the future.',
        );
      }
      await this.model
        .findOneAndUpdate(
          { key: 'singleton' },
          {
            $set: {
              maintenanceEnabled: true,
              maintenanceUntil: until,
            },
            $setOnInsert: { key: 'singleton' },
          },
          { upsert: true, new: true },
        )
        .exec();
    } else {
      await this.model
        .findOneAndUpdate(
          { key: 'singleton' },
          {
            $set: {
              maintenanceEnabled: false,
              maintenanceUntil: null,
            },
            $setOnInsert: { key: 'singleton' },
          },
          { upsert: true, new: true },
        )
        .exec();
    }
    return this.getRawForAdmin();
  }
}
