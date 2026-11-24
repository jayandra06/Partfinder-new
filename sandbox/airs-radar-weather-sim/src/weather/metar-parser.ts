export type MetarSnapshot = {
  station: string;
  windKt: number | null;
  visibilitySm: number | null;
  altimeterInHg: number | null;
  raw: string;
};

/** Minimal METAR tokenizer for sandbox weather fusion demos. */
export function parseMetarStub(raw: string): MetarSnapshot {
  const tokens = raw.trim().split(/\s+/);
  const station = tokens[0] ?? 'UNKN';
  let windKt: number | null = null;
  let visibilitySm: number | null = null;
  let altimeterInHg: number | null = null;

  for (const t of tokens) {
    if (/^\d{3}\d{2,3}KT$/.test(t)) {
      windKt = Number.parseInt(t.slice(0, 3), 10);
    } else if (/^\d{4}$/.test(t) && visibilitySm === null) {
      visibilitySm = Number.parseInt(t, 10) / 100;
    } else if (t.startsWith('A') && t.length === 5) {
      altimeterInHg = Number.parseInt(t.slice(1), 10) / 100;
    }
  }

  return { station, windKt, visibilitySm, altimeterInHg, raw };
}
