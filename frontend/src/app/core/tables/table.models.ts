export type TableStatus =
  'Draft' | 'Open' | 'Running' | 'Counting' | 'Reconciled' | 'Settled' | 'Closed' | 'Cancelled';

export type JoinPolicy = 'AnyMember' | 'InviteOnly' | 'Code';

export type TablePlayerStatus = 'Standby' | 'Playing' | 'Left';

export interface TableSummary {
  id: string;
  name: string;
  status: TableStatus;
  buyIn: number;
  playerCount: number;
  createdAtUtc: string;
  startedAtUtc: string | null;
}

export interface TablePlayer {
  tablePlayerId: string;
  userId: string;
  displayName: string;
  status: TablePlayerStatus;
  seatOrder: number;
  /** Buy-ins + rebuys + chips bought, less anything credited for chips sold. */
  paidIn: number;
  rebuyCount: number;
  hasPaymentHandle: boolean;
}

export interface ChipStock {
  denominationId: string;
  faceValue: number;
  effectiveValue: number;
  /** Palette token. At the table people ask for the reds, not for the 25s. */
  colour: string | null;
  remaining: number;
  issued: number;
}

export interface TableDetail {
  id: string;
  championshipId: string;
  name: string;
  status: TableStatus;
  buyIn: number;
  rebuy: number;
  moneyPerUnit: number;
  buyInUnits: number;
  joinPolicy: JoinPolicy;
  allowLateEntry: boolean;
  /** Only present for someone who can manage the table. */
  joinCode: string | null;
  smallChipReserve: number;
  startedAtUtc: string | null;
  players: TablePlayer[];
  stock: ChipStock[];
  totalPaidIn: number;
  canManage: boolean;
  myPlayerId: string | null;
}

export interface CreateTableRequest {
  name: string;
  chipSetId: string;
  buyIn: number | null;
  rebuy: number | null;
  joinPolicy: JoinPolicy;
  allowLateEntry: boolean;
  smallChipReserve: number;
}

/**
 * A table still worth looking at: anything that has not finished. Counting and
 * settling are as much part of the night as the play itself, so only Closed and
 * Cancelled drop out.
 */
export function isActive(status: TableStatus): boolean {
  return status !== 'Closed' && status !== 'Cancelled';
}

/**
 * Active tables first, then newest first within each group. On a game night the
 * table in progress is the only one anyone wants, and it should never be buried
 * under months of finished ones.
 */
export function sortForDisplay(tables: readonly TableSummary[]): TableSummary[] {
  return [...tables].sort((a, b) => {
    const activeDelta = Number(isActive(b.status)) - Number(isActive(a.status));

    return activeDelta !== 0 ? activeDelta : b.createdAtUtc.localeCompare(a.createdAtUtc);
  });
}

/** Chips still in the case, counted in play units. */
export function remainingUnits(stock: readonly ChipStock[]): number {
  return stock.reduce((total, s) => total + s.remaining * s.effectiveValue, 0);
}

/** Chips handed out at this table, counted in play units. */
export function issuedUnits(stock: readonly ChipStock[]): number {
  return stock.reduce((total, s) => total + s.issued * s.effectiveValue, 0);
}

/**
 * How many more stacks the case can still cover, at best. Upper bound rather
 * than a promise: the chips left may not be able to make a stack exactly, which
 * is a question only the API's calculator can answer.
 */
export function stacksLeft(stock: readonly ChipStock[], buyInUnits: number): number {
  return buyInUnits <= 0 ? 0 : Math.floor(remainingUnits(stock) / buyInUnits);
}

export interface StackPreviewChip {
  denominationId: string;
  faceValue: number;
  effectiveValue: number;
  colour: string | null;
  quantity: number;
}

/** What a buy-in or rebuy would hand over, worked out before committing. */
export interface StackPreview {
  chips: StackPreviewChip[];
  money: number;
  units: number;
  /** Non-zero means the case cannot make this stack; the API would refuse it. */
  shortfallUnits: number;
  isPossible: boolean;
}

export interface ReconciliationLine {
  denominationId: string;
  faceValue: number;
  effectiveValue: number;
  /** Chips that left the case at this table. */
  issued: number;
  /** Chips the players have reported holding. */
  counted: number;
  /** counted − issued. Negative means chips are missing from the count. */
  difference: number;
}

export interface AwaitingPlayer {
  tablePlayerId: string;
  displayName: string;
}

export interface Reconciliation {
  lines: ReconciliationLine[];
  awaitingCountFrom: AwaitingPlayer[];
  everyoneHasCounted: boolean;
  chipsBalance: boolean;
  /** Both of the above. The API refuses to settle unless this is true. */
  canSettle: boolean;
}

export type PaymentHandleType = 'Pix' | 'Other';

export interface Transfer {
  fromDisplayName: string;
  toDisplayName: string;
  amount: number;
  toPaymentType: PaymentHandleType | null;
  toPaymentHandle: string | null;
}

export interface TableResultRow {
  tablePlayerId: string;
  displayName: string;
  position: number;
  points: number;
  balance: number;
}

export interface Settlement {
  transfers: Transfer[];
  results: TableResultRow[];
}

/** What one player's reported stack is worth, in play units. */
export function countUnits(
  lines: readonly ReconciliationLine[],
  quantities: Readonly<Record<string, number | null>>,
): number {
  return lines.reduce(
    (total, line) => total + (quantities[line.denominationId] ?? 0) * line.effectiveValue,
    0,
  );
}

/**
 * Only the chips that do not tally. During counting this is the whole question —
 * showing twelve balanced denominations alongside the one that is off just makes
 * it harder to find.
 */
export function offBy(lines: readonly ReconciliationLine[]): ReconciliationLine[] {
  return lines.filter((line) => line.difference !== 0);
}

export interface ChipCountEntry {
  denominationId: string;
  quantity: number;
}
