import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { environment } from '../../../../../environments/environment';
import { RankingRow } from '../../../../core/championships/championship.models';
import { RankingsTab } from './rankings';

describe('RankingsTab', () => {
  const championshipId = '9a1f1d4c-1c58-4d8e-9a0b-0f2b3c4d5e6f';

  /**
   * The same two people, ordered differently by each ranking: someone can grind
   * out points across many nights while another wins the money in two.
   */
  const byPoints: RankingRow[] = [
    {
      userId: 'a',
      displayName: 'Constante',
      position: 1,
      points: 30,
      balance: -20,
      tablesPlayed: 6,
      wins: 0,
      bestPosition: 2,
    },
    {
      userId: 'b',
      displayName: 'Sortudo',
      position: 2,
      points: 20,
      balance: 180,
      tablesPlayed: 2,
      wins: 2,
      bestPosition: 1,
    },
  ];

  const byBalance: RankingRow[] = [
    { ...byPoints[1], position: 1 },
    { ...byPoints[0], position: 2 },
  ];

  let fixture: ComponentFixture<RankingsTab>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RankingsTab],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);

    fixture = TestBed.createComponent(RankingsTab);
    fixture.componentRef.setInput('championshipId', championshipId);
    await fixture.whenStable();

    http
      .expectOne(`${environment.apiUrl}/championships/${championshipId}/rankings`)
      .flush({ byPoints, byBalance, tablesCounted: 6 });

    http.expectOne(`${environment.apiUrl}/championships/${championshipId}/statistics`).flush({
      tablesPlayed: 6,
      distinctPlayers: 2,
      moneyIn: 600,
      rebuys: 4,
      averageMoneyPerTable: 100,
      biggestWin: { displayName: 'Sortudo', tableName: 'Mesa', balance: 180 },
      biggestLoss: { displayName: 'Constante', tableName: 'Mesa', balance: -90 },
    });

    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  function names(): string[] {
    return [...(fixture.nativeElement as HTMLElement).querySelectorAll('.rank__name')].map(
      (element) => element.textContent?.trim().split(/\s+/)[0] ?? '',
    );
  }

  function show(basis: string): void {
    (fixture.componentInstance as unknown as { show: (basis: string) => void }).show(basis);
    fixture.detectChanges();
  }

  it('should open on the points ranking', () => {
    expect(names()).toEqual(['Constante', 'Sortudo']);
  });

  it('should reorder when switched to balance', () => {
    show('balance');

    expect(names()).toEqual(['Sortudo', 'Constante']);
  });

  it('should switch back', () => {
    show('balance');
    show('points');

    expect(names()).toEqual(['Constante', 'Sortudo']);
  });

  it('should show the statistics alongside the ranking', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('Sortudo');
    expect(text).toContain('180');
  });
});
