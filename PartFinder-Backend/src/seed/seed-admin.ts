import 'reflect-metadata';
import { NestFactory } from '@nestjs/core';
import { AppModule } from '../app.module';
import { UsersService } from '../users/users.service';

/** Default admin for local seed; override with SEED_ADMIN_EMAIL / SEED_ADMIN_PASSWORD. Set SEED_ADMIN_UPDATE_PASSWORD=1 to reset password when admin exists. */
const DEFAULT_EMAIL = 'jayandraa5@gmail.com';
const DEFAULT_PASSWORD = 'J@yandra06';

async function run() {
  const app = await NestFactory.createApplicationContext(AppModule, {
    logger: ['error', 'warn', 'log'],
  });
  try {
    const users = app.get(UsersService);
    const email = (process.env.SEED_ADMIN_EMAIL ?? DEFAULT_EMAIL).trim();
    const password = process.env.SEED_ADMIN_PASSWORD ?? DEFAULT_PASSWORD;

    const existing = await users.findByEmail(email);
    if (existing) {
      const updatePw =
        process.env.SEED_ADMIN_UPDATE_PASSWORD === '1' ||
        process.env.SEED_ADMIN_UPDATE_PASSWORD === 'true';
      if (updatePw) {
        await users.setPassword(String(existing._id), password);
        console.log(`Updated admin password: ${email}`);
        return;
      }
      console.log(`Admin already exists: ${email}`);
      return;
    }

    await users.createAdmin(email, password);
    console.log(`Seeded admin: ${email}`);
  } finally {
    await app.close();
  }
}

run().catch((err) => {
  console.error(err);
  process.exit(1);
});
