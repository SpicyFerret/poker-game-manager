import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import {
  BlindLevelInput,
  Blinds,
  ChipCountEntry,
  ClockAction,
  CreateTableRequest,
  Reconciliation,
  Settlement,
  StackHistoryEntry,
  StackPreview,
  TableDetail,
  TableSummary,
} from './table.models';

@Injectable({ providedIn: 'root' })
export class TablesService {
  private readonly http = inject(HttpClient);

  private base(championshipId: string): string {
    return `${environment.apiUrl}/championships/${championshipId}/tables`;
  }

  list(championshipId: string): Observable<TableSummary[]> {
    return this.http.get<TableSummary[]>(this.base(championshipId));
  }

  get(championshipId: string, tableId: string): Observable<TableDetail> {
    return this.http.get<TableDetail>(`${this.base(championshipId)}/${tableId}`);
  }

  create(championshipId: string, request: CreateTableRequest): Observable<string> {
    return this.http.post<string>(this.base(championshipId), request);
  }

  join(championshipId: string, tableId: string, code: string | null): Observable<void> {
    return this.http.post<void>(`${this.base(championshipId)}/${tableId}/join`, { code });
  }

  /** A manager seating someone else — the only way onto an InviteOnly table. */
  addPlayer(championshipId: string, tableId: string, userId: string): Observable<void> {
    return this.http.post<void>(`${this.base(championshipId)}/${tableId}/players`, { userId });
  }

  /** A manager answering someone who asked to join a table already in play. */
  decideJoinRequest(
    championshipId: string,
    tableId: string,
    tablePlayerId: string,
    approved: boolean,
  ): Observable<void> {
    return this.http.post<void>(
      `${this.base(championshipId)}/${tableId}/players/${tablePlayerId}/decision`,
      { approved },
    );
  }

  /**
   * Takes someone back off the table. The API refuses once chips have left the
   * case for them — at that point they are part of the night's books.
   */
  removePlayer(championshipId: string, tableId: string, tablePlayerId: string): Observable<void> {
    return this.http.delete<void>(
      `${this.base(championshipId)}/${tableId}/players/${tablePlayerId}`,
    );
  }

  /** Every buy-in and rebuy this player was actually dealt, newest first. */
  playerStacks(
    championshipId: string,
    tableId: string,
    tablePlayerId: string,
  ): Observable<StackHistoryEntry[]> {
    return this.http.get<StackHistoryEntry[]>(
      `${this.base(championshipId)}/${tableId}/players/${tablePlayerId}/stacks`,
    );
  }

  /** What chips a buy-in or rebuy would hand over, without handing them over. */
  stackPreview(
    championshipId: string,
    tableId: string,
    isRebuy: boolean,
  ): Observable<StackPreview> {
    return this.http.get<StackPreview>(
      `${this.base(championshipId)}/${tableId}/stack-preview?isRebuy=${isRebuy}`,
    );
  }

  /** The name must match the table's exactly; the API refuses otherwise. */
  delete(championshipId: string, tableId: string, confirmName: string): Observable<void> {
    return this.http.delete<void>(`${this.base(championshipId)}/${tableId}`, {
      body: { confirmName },
    });
  }

  start(championshipId: string, tableId: string): Observable<void> {
    return this.http.post<void>(`${this.base(championshipId)}/${tableId}/start`, {});
  }

  /** isRebuy false deals in a late entrant still waiting in standby. */
  issueStack(
    championshipId: string,
    tableId: string,
    tablePlayerId: string,
    isRebuy: boolean,
  ): Observable<void> {
    return this.http.post<void>(`${this.base(championshipId)}/${tableId}/stacks`, {
      tablePlayerId,
      isRebuy,
    });
  }

  buyChipsFromPlayer(
    championshipId: string,
    tableId: string,
    buyerPlayerId: string,
    sellerPlayerId: string,
    amount: number,
  ): Observable<void> {
    return this.http.post<void>(`${this.base(championshipId)}/${tableId}/chip-trades`, {
      buyerPlayerId,
      sellerPlayerId,
      amount,
    });
  }

  /** Play is over. Everyone starts counting what they are holding. */
  startCounting(championshipId: string, tableId: string): Observable<void> {
    return this.http.post<void>(`${this.base(championshipId)}/${tableId}/counting`, {});
  }

  /**
   * One player's whole stack, replacing whatever they reported before. Sent
   * whole rather than chip by chip so a correction overwrites the mistake
   * instead of adding to it.
   */
  reportCount(
    championshipId: string,
    tableId: string,
    tablePlayerId: string,
    counts: readonly ChipCountEntry[],
  ): Observable<void> {
    return this.http.post<void>(`${this.base(championshipId)}/${tableId}/counts`, {
      tablePlayerId,
      counts,
    });
  }

  /**
   * Going home early: hand the chips back, take the money for them now. Counted
   * per denomination like the end-of-night count, because those chips return to
   * the case and the reconciliation has to know which ones.
   */
  cashOut(
    championshipId: string,
    tableId: string,
    tablePlayerId: string,
    counts: readonly ChipCountEntry[],
  ): Observable<void> {
    return this.http.post<void>(`${this.base(championshipId)}/${tableId}/cash-outs`, {
      tablePlayerId,
      counts,
    });
  }

  reconciliation(championshipId: string, tableId: string): Observable<Reconciliation> {
    return this.http.get<Reconciliation>(`${this.base(championshipId)}/${tableId}/reconciliation`);
  }

  /**
   * Works out who pays whom and where everyone finished. Once only, and only
   * once the count balances against what left the case.
   */
  settle(championshipId: string, tableId: string): Observable<void> {
    return this.http.post<void>(`${this.base(championshipId)}/${tableId}/settlement`, {});
  }

  settlement(championshipId: string, tableId: string): Observable<Settlement> {
    return this.http.get<Settlement>(`${this.base(championshipId)}/${tableId}/settlement`);
  }

  blinds(championshipId: string, tableId: string): Observable<Blinds> {
    return this.http.get<Blinds>(`${this.base(championshipId)}/${tableId}/blinds`);
  }

  /** Replaces the whole ladder. An empty list removes it, and with it the clock. */
  setBlindLevels(
    championshipId: string,
    tableId: string,
    levels: readonly BlindLevelInput[],
  ): Observable<void> {
    return this.http.put<void>(`${this.base(championshipId)}/${tableId}/blinds`, { levels });
  }

  controlClock(championshipId: string, tableId: string, action: ClockAction): Observable<void> {
    return this.http.post<void>(`${this.base(championshipId)}/${tableId}/clock`, { action });
  }

  /** "I have these chips in front of me." Only the player themselves may say it. */
  acknowledgeStack(
    championshipId: string,
    tableId: string,
    ledgerEntryId: string,
  ): Observable<void> {
    return this.http.post<void>(
      `${this.base(championshipId)}/${tableId}/stacks/${ledgerEntryId}/acknowledge`,
      {},
    );
  }
}
