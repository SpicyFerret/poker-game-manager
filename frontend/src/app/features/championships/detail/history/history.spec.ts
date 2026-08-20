import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { environment } from '../../../../../environments/environment';
import { HistoryRow } from '../../../../core/championships/championship.models';
import { HistoryTab } from './history';

describe('HistoryTab', () => {
  const championshipId = '9a1f1d4c-1c58-4d8e-9a0b-0f2b3c4d5e6f';

  let fixture: ComponentFixture<HistoryTab>;
  let http: HttpTestingController;

  async function load(rows: HistoryRow[]): Promise<void> {
    TestBed.resetTestingModule();

    await TestBed.configureTestingModule({
      imports: [HistoryTab],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);

    fixture = TestBed.createComponent(HistoryTab);
    fixture.componentRef.setInput('championshipId', championshipId);
    await fixture.whenStable();

    http.expectOne(`${environment.apiUrl}/championships/${championshipId}/history`).flush(rows);
    await fixture.whenStable();
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  afterEach(() => http.verify());

  /** The case the card exists for: the biggest stack was not the best night. */
  it('should show the chip leader as well as the winner', async () => {
    await load([
      {
        tableId: 't1',
        name: 'Quinta',
        closedAtUtc: '2026-08-19T23:00:00Z',
        playerCount: 4,
        winnerDisplayName: 'Amigo',
        winnerBalance: 20,
        moneyIn: 150,
        chipLeaderDisplayName: 'Dono',
        chipLeaderChips: 80,
      },
    ]);

    expect(text()).toContain('Amigo');
    expect(text()).toContain('Dono');
    expect(text()).toContain('80,00');
  });

  it('should cope with a table nobody won', async () => {
    await load([
      {
        tableId: 't1',
        name: 'Quinta',
        closedAtUtc: null,
        playerCount: 0,
        winnerDisplayName: null,
        winnerBalance: 0,
        moneyIn: 0,
        chipLeaderDisplayName: null,
        chipLeaderChips: 0,
      },
    ]);

    expect(text()).toContain('—');
  });

  it('should say so when nothing has been closed yet', async () => {
    await load([]);

    expect(text()).toContain('Nenhuma mesa fechada');
  });
});
