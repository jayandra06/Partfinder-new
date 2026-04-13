import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import * as bcrypt from 'bcrypt';
import { Model } from 'mongoose';
import { AdminUser, AdminUserDocument } from './schemas/admin-user.schema';

const BCRYPT_ROUNDS = 10;

@Injectable()
export class UsersService {
  constructor(
    @InjectModel(AdminUser.name)
    private readonly adminUserModel: Model<AdminUserDocument>,
  ) {}

  async countAdmins(): Promise<number> {
    return this.adminUserModel.countDocuments().exec();
  }

  async findByEmail(email: string): Promise<AdminUserDocument | null> {
    return this.adminUserModel
      .findOne({ email: email.toLowerCase().trim() })
      .exec();
  }

  async createAdmin(
    email: string,
    plainPassword: string,
  ): Promise<AdminUserDocument> {
    const passwordHash = await bcrypt.hash(plainPassword, BCRYPT_ROUNDS);
    const created = new this.adminUserModel({
      email: email.toLowerCase().trim(),
      passwordHash,
    });
    return created.save();
  }

  async validatePassword(
    user: AdminUserDocument,
    plainPassword: string,
  ): Promise<boolean> {
    return bcrypt.compare(plainPassword, user.passwordHash);
  }

  async findById(id: string): Promise<AdminUserDocument | null> {
    return this.adminUserModel.findById(id).exec();
  }

  async setPassword(userId: string, plainPassword: string): Promise<void> {
    const passwordHash = await bcrypt.hash(plainPassword, BCRYPT_ROUNDS);
    await this.adminUserModel
      .findByIdAndUpdate(userId, { passwordHash })
      .exec();
  }

  async setTotpSecret(userId: string, secretBase32: string): Promise<void> {
    await this.adminUserModel
      .findByIdAndUpdate(userId, {
        totpSecretBase32: secretBase32.trim(),
        totpEnabled: true,
      })
      .exec();
  }

  async clearTotp(userId: string): Promise<void> {
    await this.adminUserModel
      .findByIdAndUpdate(userId, {
        $set: { totpEnabled: false },
        $unset: { totpSecretBase32: 1 },
      })
      .exec();
  }
}