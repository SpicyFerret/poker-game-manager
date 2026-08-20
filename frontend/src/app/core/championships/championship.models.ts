/** Mirrors Domain.Championships.ChampionshipRole. Ordered: higher can do more. */
export type ChampionshipRole = 'Player' | 'TableManager' | 'Admin' | 'Owner';

const ROLE_RANK: Record<ChampionshipRole, number> = {
  Player: 0,
  TableManager: 1,
  Admin: 2,
  Owner: 3,
};

export function rankOf(role: ChampionshipRole): number {
  return ROLE_RANK[role];
}

/** The same "at least this role" test the API applies, for showing or hiding controls. */
export function atLeast(role: ChampionshipRole, minimum: ChampionshipRole): boolean {
  return rankOf(role) >= rankOf(minimum);
}

/** Roles a caller may hand out: strictly below their own, and never Owner. */
export function assignableRoles(callerRole: ChampionshipRole): ChampionshipRole[] {
  return (['Player', 'TableManager', 'Admin'] as ChampionshipRole[]).filter(
    (role) => rankOf(role) < rankOf(callerRole),
  );
}

export interface ChampionshipSummary {
  id: string;
  name: string;
  description: string | null;
  role: ChampionshipRole;
  memberCount: number;

  /**
   * Top of the points ranking. Null until a table has been settled — a card that
   * claims a leader before anyone has played would be a lie, not an empty state.
   */
  leaderDisplayName: string | null;
  leaderPoints: number;
}

export interface Championship {
  id: string;
  name: string;
  description: string | null;
  ownerId: string;
  defaultBuyIn: number;
  defaultRebuy: number;
  enforceDefaults: boolean;
  moneyPerUnit: number;
  pointsByPosition: number[];
  role: ChampionshipRole;

  /**
   * Top of the points ranking. Null until a table has been settled — a card that
   * claims a leader before anyone has played would be a lie, not an empty state.
   */
  leaderDisplayName: string | null;
  leaderPoints: number;
}

export interface ChampionshipSettings {
  name: string;
  description: string | null;
  defaultBuyIn: number;
  defaultRebuy: number;
  enforceDefaults: boolean;
  moneyPerUnit: number;
  pointsByPosition: number[];
}

export interface Member {
  userId: string;
  displayName: string;
  role: ChampionshipRole;
  joinedAtUtc: string;
  hasPaymentHandle: boolean;
}

export interface Invite {
  id: string;
  code: string;
  role: ChampionshipRole;
  expiresAtUtc: string | null;
  maxUses: number | null;
  uses: number;
  isRevoked: boolean;
}

export interface ChipDenomination {
  id: string;
  faceValue: number;
  effectiveValue: number;
  quantity: number;
  colour: string | null;
}

export interface ChipSet {
  id: string;
  name: string;
  totalUnits: number;
  denominations: ChipDenomination[];
}

export interface ChipDenominationInput {
  faceValue: number;
  effectiveValue: number;
  quantity: number;
  colour: string | null;
}

export interface RankingRow {
  userId: string;
  displayName: string;
  /** Place in this ranking. The two rankings order the same people differently. */
  position: number;
  points: number;
  balance: number;
  tablesPlayed: number;
  wins: number;
  bestPosition: number;
}

export interface Rankings {
  byPoints: RankingRow[];
  byBalance: RankingRow[];
  tablesCounted: number;
}

export interface HistoryRow {
  tableId: string;
  name: string;
  closedAtUtc: string | null;
  playerCount: number;
  winnerDisplayName: string | null;
  winnerBalance: number;
  /** Buy-ins and rebuys across the whole table. */
  moneyIn: number;
}

export interface StatementRow {
  tableId: string;
  tableName: string;
  closedAtUtc: string | null;
  position: number;
  points: number;
  balance: number;
  paidIn: number;
  rebuys: number;
}

export interface Statement {
  rows: StatementRow[];
  totalBalance: number;
  totalPaidIn: number;
  totalPoints: number;
  wins: number;
}

export interface NightRecord {
  displayName: string;
  tableName: string;
  balance: number;
}

export interface Statistics {
  tablesPlayed: number;
  distinctPlayers: number;
  moneyIn: number;
  rebuys: number;
  averageMoneyPerTable: number;
  /** Null until something has been settled. */
  biggestWin: NightRecord | null;
  biggestLoss: NightRecord | null;
}
