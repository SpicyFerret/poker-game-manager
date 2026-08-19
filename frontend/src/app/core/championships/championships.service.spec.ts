import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
import { ChampionshipsService } from './championships.service';

describe('ChampionshipsService', () => {
  let service: ChampionshipsService;
  let http: HttpTestingController;

  const championshipId = '9a1f1d4c-1c58-4d8e-9a0b-0f2b3c4d5e6f';

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(ChampionshipsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should post the settings when creating', () => {
    service
      .create({
        name: 'Quinta-feira',
        description: null,
        defaultBuyIn: 50,
        defaultRebuy: 50,
        enforceDefaults: false,
        moneyPerUnit: 0.05,
        pointsByPosition: [10, 7, 5],
      })
      .subscribe();

    const request = http.expectOne(`${environment.apiUrl}/championships`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body.moneyPerUnit).toBe(0.05);
    request.flush('"id"');
  });

  it('should send the code as typed, leaving normalisation to the API', () => {
    service.join('  abc-d23 ').subscribe();

    const request = http.expectOne(`${environment.apiUrl}/championships/join`);
    expect(request.request.body).toEqual({ code: '  abc-d23 ' });
    request.flush({ championshipId, name: 'Quinta-feira' });
  });

  it('should target the member when changing a role', () => {
    const userId = '2b7e6f10-0a1b-4c2d-8e3f-4a5b6c7d8e9f';

    service.changeRole(championshipId, userId, 'TableManager').subscribe();

    const request = http.expectOne(
      `${environment.apiUrl}/championships/${championshipId}/members/${userId}/role`,
    );
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ role: 'TableManager' });
    request.flush(null);
  });

  it('should send null for an unlimited invite', () => {
    service.createInvite(championshipId, 'Player', null, null).subscribe();

    const request = http.expectOne(`${environment.apiUrl}/championships/${championshipId}/invites`);
    expect(request.request.body.maxUses).toBeNull();
    request.flush({
      id: 'x',
      code: 'ABCD23',
      role: 'Player',
      expiresAtUtc: null,
      maxUses: null,
      uses: 0,
      isRevoked: false,
    });
  });

  it('should carry the effective value override through to the API', () => {
    service
      .createChipSet(championshipId, 'Maleta 300', [
        { faceValue: 5, effectiveValue: 100, quantity: 100, colour: 'Vermelha' },
      ])
      .subscribe();

    const request = http.expectOne(
      `${environment.apiUrl}/championships/${championshipId}/chip-sets`,
    );
    expect(request.request.body.denominations[0]).toEqual({
      faceValue: 5,
      effectiveValue: 100,
      quantity: 100,
      colour: 'Vermelha',
    });
    request.flush('"id"');
  });
});
