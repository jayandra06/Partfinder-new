import 'reflect-metadata';
import { NestFactory } from '@nestjs/core';
import { AppModule } from '../app.module';
import { UsersService } from '../users/users.service';

const SEED_ADMINS = [
  { email: 'jayandraa5@gmail.com', password: 'J@yandra06' },
  { email: 'technical@euroasianngroup.com', password: 'J@yandra06' },
];

async function run() {
  const app = await NestFactory.createApplicationContext(AppModule, {
    logger: ['error', 'warn', 'log'],
  });
  try {
    const users = app.get(UsersService);

    for (const admin of SEED_ADMINS) {
      const email = admin.email.trim().toLowerCase();
      const password = admin.password;

      const existing = await users.findByEmail(email);
      if (existing) {
        await users.setPassword(String(existing._id), password);
        console.log(`Updated admin password: ${email}`);
      } else {
        await users.createAdmin(email, password);
        console.log(`Seeded admin: ${email}`);
      }
    }
  } finally {
    await app.close();
  }
}

run().catch((err) => {
  console.error(err);
  process.exit(1);
});
