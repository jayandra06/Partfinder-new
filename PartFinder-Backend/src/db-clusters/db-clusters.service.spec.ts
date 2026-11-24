import { BadRequestException, InternalServerErrorException, NotFoundException } from '@nestjs/common';
import mongoose from 'mongoose';
import { createMongooseConnectionWithSrvDnsRetry } from '../mongo-srv-dns-retry';
import { prepareMongoUriForDriver } from '../mongo-uri';
import { DbClustersService } from './db-clusters.service';

jest.mock('../mongo-srv-dns-retry', () => ({ createMongooseConnectionWithSrvDnsRetry: jest.fn() }));
const connect = createMongooseConnectionWithSrvDnsRetry as jest.MockedFunction<typeof createMongooseConnectionWithSrvDnsRetry>;

const chain = (result: unknown) => {
  const q: any = {};
  for (const m of ['sort', 'select', 'lean']) q[m] = jest.fn(() => q);
  q.exec = jest.fn(async () => result);
  q.then = (res: any, rej: any) => Promise.resolve(result).then(res, rej);
  return q;
};

const URI = 'mongodb+srv://user:pw@cluster0.example.net/tenants';

function setup(clusters: any[] = [], usedByCluster: Record<string, number> = {}) {
  const clusterModel = {
    find: jest.fn(() => chain(clusters)),
    findById: jest.fn((id: mongoose.Types.ObjectId) => chain(clusters.find((c) => String(c._id) === String(id)) ?? null)),
    create: jest.fn(async (d: any) => ({ _id: new mongoose.Types.ObjectId(), ...d })),
  };
  const orgModel = {
    countDocuments: jest.fn(async (f: any) => usedByCluster[String(f.assignedDbClusterId)] ?? 0),
    find: jest.fn(() => chain([])),
  };
  return { service: new DbClustersService(clusterModel as any, orgModel as any), clusterModel, orgModel };
}

function fakeConnection(ping: () => Promise<unknown>, withDb = true) {
  return { db: withDb ? { admin: () => ({ command: ping }) } : undefined, close: jest.fn(async () => undefined) } as any;
}

describe('DbClustersService', () => {
  beforeEach(() => connect.mockReset());

  describe('pickClusterForProvisioning', () => {
    it('returns null when there are no clusters with spare capacity', async () => {
      const full = { _id: new mongoose.Types.ObjectId(), connectionUri: URI, maxDatabases: 2 };
      const { service } = setup([full], { [String(full._id)]: 2 });

      await expect(service.pickClusterForProvisioning()).resolves.toBeNull();
    });

    it('picks the least loaded cluster, defaulting max to 250', async () => {
      const a = { _id: new mongoose.Types.ObjectId(), connectionUri: 'a' };
      const b = { _id: new mongoose.Types.ObjectId(), connectionUri: 'b' };
      const { service } = setup([a, b], { [String(a._id)]: 249, [String(b._id)]: 10 });

      await expect(service.pickClusterForProvisioning()).resolves.toEqual({ connectionUri: 'b', clusterId: b._id });
    });
  });

  describe('probeConnection', () => {
    it('rejects non-mongodb URIs without connecting', async () => {
      const { service } = setup();

      await expect(service.probeConnection('http://nope')).resolves.toEqual({
        ok: false,
        message: 'Invalid MongoDB URI. Use mongodb:// or mongodb+srv://',
      });
      expect(connect).not.toHaveBeenCalled();
    });

    it('pings the admin database and closes the connection', async () => {
      const ping = jest.fn().mockResolvedValue({ ok: 1 });
      const conn = fakeConnection(ping);
      connect.mockResolvedValue(conn);
      const { service } = setup();

      await expect(service.probeConnection(URI)).resolves.toEqual({ ok: true });
      expect(ping).toHaveBeenCalledWith({ ping: 1 });
      expect(conn.close).toHaveBeenCalled();
    });

    it('reports a missing database handle', async () => {
      connect.mockResolvedValue(fakeConnection(jest.fn(), false));
      const { service } = setup();

      const r = await service.probeConnection(URI);
      expect(r.ok).toBe(false);
      expect((r as any).message).toMatch(/could not access the database handle/);
    });

    it('adds DNS guidance for SRV lookup failures', async () => {
      connect.mockRejectedValue(new Error('querySrv ENOTFOUND _mongodb._tcp.cluster0'));
      const { service } = setup();

      const r: any = await service.probeConnection(URI);
      expect(r.message).toMatch(/^Could not connect: querySrv ENOTFOUND/);
      expect(r.message).toContain('SRV DNS failed');
    });

    it('does not add DNS guidance for other errors', async () => {
      connect.mockRejectedValue(new Error('bad auth'));
      const { service } = setup();

      await expect(service.probeConnection(URI)).resolves.toEqual({ ok: false, message: 'Could not connect: bad auth' });
    });
  });

  describe('create', () => {
    it('validates the URI and probe result before saving', async () => {
      const { service, clusterModel } = setup();
      await expect(service.create('ftp://x')).rejects.toBeInstanceOf(BadRequestException);
      connect.mockRejectedValue(new Error('bad auth'));
      await expect(service.create(URI)).rejects.toThrow('Could not connect: bad auth');
      expect(clusterModel.create).not.toHaveBeenCalled();
    });

    it('saves the prepared URI and returns a masked form', async () => {
      connect.mockResolvedValue(fakeConnection(jest.fn().mockResolvedValue({})));
      const { service, clusterModel } = setup();
      const prepared = prepareMongoUriForDriver(URI);

      const r = await service.create(URI);

      expect(clusterModel.create).toHaveBeenCalledWith({ connectionUri: prepared });
      expect(r.uriMasked).toBe(`${prepared.slice(0, 16)}...${prepared.slice(-8)}`);
      expect(r.uriMasked).not.toContain('pw@');
    });

    it('wraps persistence failures', async () => {
      connect.mockResolvedValue(fakeConnection(jest.fn().mockResolvedValue({})));
      const { service, clusterModel } = setup();
      clusterModel.create.mockRejectedValue(new Error('dup'));

      await expect(service.create(URI)).rejects.toBeInstanceOf(InternalServerErrorException);
    });
  });

  it('findAllForAdmin reports usage, capacity and fully masks short URIs', async () => {
    const created = new Date('2026-01-02T03:04:05Z');
    const c = { _id: new mongoose.Types.ObjectId(), connectionUri: 'mongodb://h/db', maxDatabases: 5, createdAt: created };
    const { service } = setup([c], { [String(c._id)]: 7 });

    await expect(service.findAllForAdmin()).resolves.toEqual([
      { id: String(c._id), uriMasked: '****', createdAt: created.toISOString(), databasesUsed: 7, maxDatabases: 5, databasesFree: 0 },
    ]);
  });

  describe('findOrganizationsForCluster', () => {
    it('validates the id and existence of the cluster', async () => {
      const { service } = setup();
      await expect(service.findOrganizationsForCluster('x')).rejects.toBeInstanceOf(BadRequestException);
      await expect(service.findOrganizationsForCluster(new mongoose.Types.ObjectId().toString())).rejects.toBeInstanceOf(NotFoundException);
    });

    it('maps organisations on the cluster', async () => {
      const c = { _id: new mongoose.Types.ObjectId(), connectionUri: URI };
      const { service, orgModel } = setup([c]);
      const orgId = new mongoose.Types.ObjectId();
      const created = new Date('2026-03-01T00:00:00Z');
      orgModel.find.mockReturnValue(chain([{ _id: orgId, orgCode: 'ACME', name: 'Acme', createdAt: created }]));

      await expect(service.findOrganizationsForCluster(String(c._id))).resolves.toEqual([
        { id: String(orgId), orgCode: 'ACME', name: 'Acme', status: '', createdAt: created.toISOString() },
      ]);
    });
  });
});
