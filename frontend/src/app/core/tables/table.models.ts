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

/** A stack handed to you that you have not confirmed receiving yet. */
export interface PendingStack {
  ledgerEntryId: string;
  isRebuy: boolean;
  money: number;
  chips: StackPreviewChip[];
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

  /**
   * Your own unconfirmed stacks, oldest first. Carried on the table itself, so
   * the notice is already waiting when you open the screen.
   */
  pendingStacks: PendingStack[];
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

/**
 * The card colour for a table: green before anyone has sat down, blue once play
 * has started (through to the counting and settling that follow it), grey once
 * the table is closed for good. A `Cancelled` table is grey for the same
 * reason — nothing more will happen at it.
 */
export type TableMood = 'idle' | 'live' | 'done';

export function tableMood(status: TableStatus): TableMood {
  if (!isActive(status)) {
    return 'done';
  }

  return status === 'Draft' || status === 'Open' ? 'idle' : 'live';
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
  /**
   * How many identical stacks this mix is for: the players waiting when a table
   * starts, and 1 for a single buy-in or rebuy. The same list of chips means
   * something very different handed to one person or to five.
   */
  stackCount: number;
  /**
   * Whether all those stacks are the identical mix. False means the case would
   * not split evenly, so the stacks were worked out one after another — same
   * value each, different chips — and `chips` is only the first player's.
   */
  stacksAreEqual: boolean;
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

export interface BlindLevel {
  order: number;
  smallBlind: number;
  bigBlind: number;
  ante: number;
  durationSeconds: number;
}

/**
 * The clock as the server keeps it: time already played, never a countdown. Each
 * phone works the remainder out itself, so every device agrees and a poll that
 * arrives late does not make the clock wrong.
 */
export interface TableClock {
  currentLevel: number;
  isPaused: boolean;
  elapsedSeconds: number;
  serverTimeUtc: string;
}

export interface Blinds {
  levels: BlindLevel[];
  /** Null when the table has no clock, which is the common case. */
  clock: TableClock | null;
}

export interface BlindLevelInput {
  smallBlind: number;
  bigBlind: number;
  ante: number;
  durationSeconds: number;
}

export type ClockAction = 'Start' | 'Pause' | 'NextLevel' | 'PreviousLevel';

/**
 * Seconds still to play in the current level.
 *
 * Reads from the sample the server sent plus however long ago that arrived,
 * rather than counting down from a number, so the ticking stays honest between
 * polls and the level never jumps when one lands. A paused clock stands still.
 *
 * A level with no duration runs until the manager moves it on, and reports zero.
 */
export function secondsLeft(
  level: BlindLevel | undefined,
  clock: TableClock,
  sinceSampleSeconds: number,
): number {
  if (!level || level.durationSeconds <= 0) {
    return 0;
  }

  const elapsed = clock.elapsedSeconds + (clock.isPaused ? 0 : sinceSampleSeconds);

  return Math.max(level.durationSeconds - Math.floor(elapsed), 0);
}

/** mm:ss, and h:mm:ss once a level runs past an hour. */
export function formatDuration(totalSeconds: number): string {
  const seconds = Math.max(Math.floor(totalSeconds), 0);
  const minutes = Math.floor(seconds / 60);
  const pad = (value: number): string => String(value).padStart(2, '0');

  return minutes >= 60
    ? `${Math.floor(minutes / 60)}:${pad(minutes % 60)}:${pad(seconds % 60)}`
    : `${minutes}:${pad(seconds % 60)}`;
}

/**
 * A ladder that starts where the table can actually bet: the smallest chip in
 * the case is the least anyone can post, so anything below it is unplayable.
 * Each level roughly doubles, which is the shape home games settle on anyway.
 *
 * Only a starting point — every value stays editable.
 */
export function suggestLadder(smallestChip: number, levelCount = 8): BlindLevelInput[] {
  const start = Math.max(smallestChip, 1);
  const levels: BlindLevelInput[] = [];

  for (let index = 0; index < levelCount; index++) {
    // Doubling every level alone gets silly fast; every other level takes the
    // gentler step, which is what printed structures do.
    const smallBlind = start * Math.round(2 ** (index / 1.5));

    levels.push({
      smallBlind,
      bigBlind: smallBlind * 2,
      ante: 0,
      durationSeconds: 20 * 60,
    });
  }

  return levels;
}
