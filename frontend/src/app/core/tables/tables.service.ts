import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { CreateTableRequest, TableDetail, TableSummary } from './table.models';

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
}
