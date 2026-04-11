import { ConflictException, Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { CreateOrganizationDto } from './dto/create-organization.dto';
import { Organization, OrganizationDocument } from './schemas/organization.schema';

function computeValidity(plan: string): Date {
  if (plan === 'lifetime') {
    return new Date('2126-01-01T00:00:00.000Z');
  }
  const d = new Date();
  d.setFullYear(d.getFullYear() + 1);
  return d;
}

@Injectable()
export class OrganizationsService {
  constructor(
    @InjectModel(Organization.name)
    private readonly orgModel: Model<OrganizationDocument>,
  ) {}

  async findAll() {
    const list = await this.orgModel.find().sort({ createdAt: -1 }).exec();
    return list.map((doc) => {
      const row = doc.toJSON() as unknown as Record<string, unknown> & {
        _id: { toString(): string };
      };
      return {
        id: row._id.toString(),
        orgCode: String(row.orgCode),
        name: String(row.name),
        type: String(row.type),
        plan: String(row.plan),
        validity: row.validity as Date,
        status: String(row.status),
        createdAt: row.createdAt as Date,
      };
    });
  }

  async create(dto: CreateOrganizationDto) {
    const existing = await this.orgModel.findOne({ orgCode: dto.orgCode }).exec();
    if (existing) {
      throw new ConflictException('Organization code already exists');
    }
    const created = new this.orgModel({
      orgCode: dto.orgCode,
      name: dto.name.trim(),
      type: dto.type,
      plan: dto.plan,
      validity: computeValidity(dto.plan),
      status: 'Active',
    });
    await created.save();
    return this.findAll();
  }
}