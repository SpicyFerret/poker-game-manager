import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { EMPTY } from 'rxjs';

import { environment } from '../../../../../environments/environment';
import { RealtimeService } from '../../../../core/realtime/realtime.service';
import { TableStatus, TableSummary } from '../../../../core/tables/table.models';
import { TablesTab } from './tables';

describe('TablesTab', () => {
  const championshipId = '9a1f1d4c-1c58-4d8e-9a0b-0f2b3c4d5e6f';

  let fixture: ComponentFixture<TablesTab>;
  let http: HttpTestingController;

  function summary(name: string, status: TableStatus): TableSummary {
    return {
      id: name,
      name,
      status,
      buyIn: 50,
      playerCount: 0,
      createdAtUtc: '2026-08-19T00:00:00Z',
      startedAtUtc: null,
    };
  }

  async function load(tables: TableSummary[]): Promise<void> {
    TestBed.resetTestingModule();

    await TestBed.configureTestingModule({
      imports: [TablesTab],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: RealtimeService, useValue: { watch: () => EMPTY } },
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);

    fixture = TestBed.createComponent(TablesTab);
    fixture.componentRef.setInput('championshipId', championshipId);
    fixture.componentRef.setInput('callerRole', 'Player');
    await fixture.whenStable();

    http.expectOne(`${environment.apiUrl}/championships/${championshipId}/tables`).flush(tables);
    http.expectOne(`${environment.apiUrl}/championships/${championshipId}/chip-sets`).flush([]);
    await fixture.whenStable();
  }

  function items(): TableSummary[] {
    return (fixture.componentInstance as unknown as { items: () => TableSummary[] }).items();
  }

  /**
   * A finished table's result lives in the history tab now — showing it here
   * too would bury the tables still worth acting on under old ones.
   */
  it('should drop a settled table from the list', async () => {
    await load([summary('mesa-antiga', 'Settled'), summary('mesa-hoje', 'Running')]);

    expect(items().map((t) => t.name)).toEqual(['mesa-hoje']);
  });

  it('should drop a closed table from the list', async () => {
    await load([summary('mesa-fechada', 'Closed'), summary('mesa-hoje', 'Open')]);

    expect(items().map((t) => t.name)).toEqual(['mesa-hoje']);
  });

  /** Cancelled has nowhere else to go, so it stays — just at the bottom, done and grey. */
  it('should keep a cancelled table in the list', async () => {
    await load([summary('mesa-cancelada', 'Cancelled'), summary('mesa-hoje', 'Open')]);

    expect(items().map((t) => t.name)).toEqual(['mesa-hoje', 'mesa-cancelada']);
  });
});
