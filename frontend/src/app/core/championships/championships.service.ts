import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import {
  Championship,
  ChampionshipRole,
  ChampionshipSettings,
  ChampionshipSummary,
  ChipDenominationInput,
  ChipSet,
  HistoryRow,
  Invite,
  Member,
  Rankings,
  Statement,
  Statistics,
} from './championship.models';

@Injectable({ providedIn: 'root' })
export class ChampionshipsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/championships`;

  list(): Observable<ChampionshipSummary[]> {
    return this.http.get<ChampionshipSummary[]>(this.base);
  }

  /** The caller's own arrangement of the list, top to bottom. */
  reorder(championshipIds: readonly string[]): Observable<void> {
    return this.http.put<void>(`${this.base}/order`, { championshipIds });
  }

  get(championshipId: string): Observable<Championship> {
    return this.http.get<Championship>(`${this.base}/${championshipId}`);
  }

  create(settings: ChampionshipSettings): Observable<string> {
    return this.http.post<string>(this.base, settings);
  }

  updateSettings(championshipId: string, settings: ChampionshipSettings): Observable<void> {
    return this.http.put<void>(`${this.base}/${championshipId}`, settings);
  }

  /** The name must match the championship's exactly; the API refuses otherwise. */
  delete(championshipId: string, confirmName: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${championshipId}`, { body: { confirmName } });
  }

  transferOwnership(championshipId: string, newOwnerId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${championshipId}/transfer-ownership`, {
      newOwnerId,
    });
  }

  // --- Members -------------------------------------------------------------

  members(championshipId: string): Observable<Member[]> {
    return this.http.get<Member[]>(`${this.base}/${championshipId}/members`);
  }

  addMember(championshipId: string, email: string, role: ChampionshipRole): Observable<void> {
    return this.http.post<void>(`${this.base}/${championshipId}/members`, { email, role });
  }

  changeRole(championshipId: string, userId: string, role: ChampionshipRole): Observable<void> {
    return this.http.put<void>(`${this.base}/${championshipId}/members/${userId}/role`, {
      role,
    });
  }

  removeMember(championshipId: string, userId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${championshipId}/members/${userId}`);
  }

  // --- Invites -------------------------------------------------------------

  invites(championshipId: string): Observable<Invite[]> {
    return this.http.get<Invite[]>(`${this.base}/${championshipId}/invites`);
  }

  createInvite(
    championshipId: string,
    role: ChampionshipRole,
    maxUses: number | null,
    expiresAtUtc: string | null,
  ): Observable<Invite> {
    return this.http.post<Invite>(`${this.base}/${championshipId}/invites`, {
      role,
      maxUses,
      expiresAtUtc,
    });
  }

  revokeInvite(championshipId: string, inviteId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${championshipId}/invites/${inviteId}`);
  }

  join(code: string): Observable<{ championshipId: string; name: string }> {
    return this.http.post<{ championshipId: string; name: string }>(`${this.base}/join`, {
      code,
    });
  }

  // --- Chip sets -----------------------------------------------------------

  chipSets(championshipId: string): Observable<ChipSet[]> {
    return this.http.get<ChipSet[]>(`${this.base}/${championshipId}/chip-sets`);
  }

  createChipSet(
    championshipId: string,
    name: string,
    denominations: ChipDenominationInput[],
  ): Observable<string> {
    return this.http.post<string>(`${this.base}/${championshipId}/chip-sets`, {
      name,
      denominations,
    });
  }

  updateChipSet(
    championshipId: string,
    chipSetId: string,
    name: string,
    denominations: ChipDenominationInput[],
  ): Observable<void> {
    return this.http.put<void>(`${this.base}/${championshipId}/chip-sets/${chipSetId}`, {
      name,
      denominations,
    });
  }

  deleteChipSet(championshipId: string, chipSetId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${championshipId}/chip-sets/${chipSetId}`);
  }

  // --- Read-back: rankings, history, statement, statistics ------------------

  rankings(championshipId: string): Observable<Rankings> {
    return this.http.get<Rankings>(`${this.base}/${championshipId}/rankings`);
  }

  history(championshipId: string): Observable<HistoryRow[]> {
    return this.http.get<HistoryRow[]>(`${this.base}/${championshipId}/history`);
  }

  /** The caller's own nights. The API never returns anyone else's. */
  statement(championshipId: string): Observable<Statement> {
    return this.http.get<Statement>(`${this.base}/${championshipId}/statement`);
  }

  statistics(championshipId: string): Observable<Statistics> {
    return this.http.get<Statistics>(`${this.base}/${championshipId}/statistics`);
  }
}
