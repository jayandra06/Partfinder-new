import { Injectable, Logger, OnModuleDestroy } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { Client } from '@elastic/elasticsearch';

export interface EsSearchResult {
  total: number;
  ids: string[];
}

export interface EsPartDocument {
  partId: string;
  orgId: string;
  templateId: string;
  rowId: string;
  metadata: Record<string, string>;
  allText: string;
  createdAt?: string;
}

const PARTS_INDEX = 'partfinder_parts';

@Injectable()
export class ElasticsearchService implements OnModuleDestroy {
  private readonly logger = new Logger(ElasticsearchService.name);
  private client: Client | null = null;
  private ready = false;
  private attempted = false;

  constructor(private readonly config: ConfigService) {}

  /** Check if ES is available and connected */
  isAvailable(): boolean {
    return this.ready && this.client !== null;
  }

  /**
   * Index a single part document into Elasticsearch.
   */
  async indexPart(doc: EsPartDocument): Promise<void> {
    const client = await this.getClient();
    if (!client) return;

    try {
      await client.index({
        index: PARTS_INDEX,
        id: doc.partId,
        document: {
          orgId: doc.orgId,
          templateId: doc.templateId,
          rowId: doc.rowId,
          metadata: doc.metadata,
          allText: doc.allText,
          createdAt: doc.createdAt ?? new Date().toISOString(),
        },
      });
    } catch (err: any) {
      this.logger.warn(`ES index failed: ${err?.message ?? String(err)}`);
    }
  }

  /**
   * Bulk index multiple part documents.
   */
  async bulkIndexParts(docs: EsPartDocument[]): Promise<void> {
    const client = await this.getClient();
    if (!client || !docs.length) return;

    try {
      const operations: any[] = [];
      for (const doc of docs) {
        operations.push({ index: { _index: PARTS_INDEX, _id: doc.partId } });
        operations.push({
          orgId: doc.orgId,
          templateId: doc.templateId,
          rowId: doc.rowId,
          metadata: doc.metadata,
          allText: doc.allText,
          createdAt: doc.createdAt ?? new Date().toISOString(),
        });
      }

      const result = await this.client!.bulk({ operations, refresh: false });
      if (result.errors) {
        const failed = result.items.filter((i: any) => i.index?.error).length;
        this.logger.warn(`Bulk index: ${failed} of ${docs.length} parts failed`);
      }
    } catch (err: any) {
      this.logger.warn(`ES bulk index failed: ${err?.message ?? String(err)}`);
    }
  }

  /**
   * Remove a part from Elasticsearch index.
   */
  async removePart(partId: string): Promise<void> {
    const client = await this.getClient();
    if (!client) return;

    try {
      await client.delete({ index: PARTS_INDEX, id: partId });
    } catch (err: any) {
      // Ignore 404 (already deleted)
      if (err?.meta?.statusCode !== 404) {
        this.logger.warn(`ES delete failed: ${err?.message ?? String(err)}`);
      }
    }
  }

  /**
   * Search parts using full-text search on allText + metadata values.
   * Returns matching part IDs.
   */
  async searchParts(
    orgId: string,
    query: string,
    templateId?: string,
    page = 1,
    limit = 100,
  ): Promise<EsSearchResult> {
    const client = await this.getClient();
    if (!client) {
      return { total: 0, ids: [] };
    }

    try {
      const must: any[] = [
        { term: { orgId } },
      ];

      if (templateId?.trim()) {
        must.push({ term: { templateId: templateId.trim() } });
      }

      if (query?.trim()) {
        must.push({
          multi_match: {
            query: query.trim(),
            fields: ['allText', 'rowId', 'metadata.*'],
            type: 'best_fields',
            fuzziness: 'AUTO',
          },
        });
      }

      const from = (Math.max(1, page) - 1) * limit;
      const result = await client.search({
        index: PARTS_INDEX,
        size: limit,
        from,
        query: { bool: { must } },
        sort: [{ createdAt: { order: 'desc' } }],
        _source: false,
      });

      const total = typeof result.hits.total === 'number'
        ? result.hits.total
        : (result.hits.total?.value ?? 0);

      const ids = result.hits.hits.map((h: any) => h._id as string);
      return { total, ids };
    } catch (err: any) {
      this.logger.warn(`ES search failed: ${err?.message ?? String(err)}`);
      return { total: 0, ids: [] };
    }
  }

  /**
   * Ensure the parts index exists with correct mappings.
   */
  async ensureIndex(): Promise<void> {
    const client = await this.getClient();
    if (!client) return;

    try {
      const exists = await client.indices.exists({ index: PARTS_INDEX });
      if (exists) return;

      await client.indices.create({
        index: PARTS_INDEX,
        mappings: {
          properties: {
            orgId: { type: 'keyword' },
            templateId: { type: 'keyword' },
            rowId: { type: 'text', fields: { keyword: { type: 'keyword' } } },
            metadata: { type: 'object', dynamic: true },
            allText: { type: 'text', analyzer: 'standard' },
            createdAt: { type: 'date' },
          },
        },
        settings: {
          number_of_shards: 1,
          number_of_replicas: 0,
        },
      });

      this.logger.log(`Created ES index: ${PARTS_INDEX}`);
    } catch (err: any) {
      this.logger.warn(`ES ensureIndex failed: ${err?.message ?? String(err)}`);
    }
  }

  /**
   * Reindex all parts for an org from provided data.
   */
  async reindexOrg(orgId: string, parts: EsPartDocument[]): Promise<void> {
    const client = await this.getClient();
    if (!client) return;

    // Delete all docs for this org first
    try {
      await client.deleteByQuery({
        index: PARTS_INDEX,
        query: { term: { orgId } },
        refresh: true,
      });
    } catch (err: any) {
      this.logger.warn(`ES reindex cleanup failed: ${err?.message ?? String(err)}`);
    }

    // Re-bulk index
    await this.bulkIndexParts(parts);
  }

  async onModuleDestroy(): Promise<void> {
    if (this.client) {
      await this.client.close();
    }
    this.client = null;
    this.ready = false;
  }

  private async getClient(): Promise<Client | null> {
    if (this.ready && this.client) {
      return this.client;
    }

    if (this.attempted) {
      return null;
    }

    this.attempted = true;
    const esUrl = this.config.get<string>('ELASTICSEARCH_URL')?.trim();
    if (!esUrl) {
      this.logger.warn('ELASTICSEARCH_URL is not set. Elasticsearch features disabled.');
      return null;
    }

    try {
      this.client = new Client({ node: esUrl });
      // Ping to verify connection
      await this.client.ping();
      this.ready = true;
      this.logger.log(`Connected to Elasticsearch at ${esUrl}`);
      await this.ensureIndex();
      return this.client;
    } catch (error: any) {
      this.logger.warn(
        `Failed to connect Elasticsearch. Search features disabled. ${error?.message ?? String(error)}`,
      );
      this.client = null;
      this.ready = false;
      return null;
    }
  }
}
