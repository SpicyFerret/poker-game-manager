import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Observable, of } from 'rxjs';
import { vi } from 'vitest';

import { environment } from '../../../../../environments/environment';
import { Invite } from '../../../../core/championships/championship.models';
import { Confirm } from '../../../../shared/confirm/confirm.service';
import { InvitesTab } from './invites';

describe('InvitesTab', () => {
  const championshipId = '9a1f1d4c-1c58-4d8e-9a0b-0f2b3c4d5e6f';

  const invites: Invite[] = [
    {
      id: 'i1',
      code: 'ABC123',
      role: 'Player',
      expiresAtUtc: null,
      maxUses: null,
      uses: 0,
      isRevoked: false,
    },
  ];

  let fixture: ComponentFixture<InvitesTab>;
  let http: HttpTestingController;

  async function load(data: Invite[]): Promise<void> {
    TestBed.resetTestingModule();

    await TestBed.configureTestingModule({
      imports: [InvitesTab],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Confirm, useValue: { ask: (): Observable<void> => of(undefined) } },
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);

    fixture = TestBed.createComponent(InvitesTab);
    fixture.componentRef.setInput('championshipId', championshipId);
    fixture.componentRef.setInput('callerRole', 'Owner');
    await fixture.whenStable();

    http.expectOne(`${environment.apiUrl}/championships/${championshipId}/invites`).flush(data);
    await fixture.whenStable();
  }

  afterEach(() => http.verify());

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  beforeEach(async () => {
    await load(invites);
  });

  it('should stack copy above revoke for a live invite', () => {
    expect(el().querySelectorAll('.invite-row__actions button').length).toBe(2);
  });

  it('should hide revoke, keeping only copy, for a revoked invite', async () => {
    await load([{ ...invites[0], isRevoked: true }]);

    expect(el().querySelectorAll('.invite-row__actions button').length).toBe(1);
  });

  it('should copy the code without asking for confirmation', () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal('navigator', { ...navigator, clipboard: { writeText } });

    const button = el().querySelector('.invite-row__actions button') as HTMLElement;
    button.click();

    expect(writeText).toHaveBeenCalledWith('ABC123');

    vi.unstubAllGlobals();
  });

  it('should revoke the code once confirmed', () => {
    const buttons = el().querySelectorAll('.invite-row__actions button');
    (buttons[1] as HTMLElement).click();

    const request = http.expectOne(
      `${environment.apiUrl}/championships/${championshipId}/invites/i1`,
    );
    expect(request.request.method).toBe('DELETE');
    request.flush(null);

    http.expectOne(`${environment.apiUrl}/championships/${championshipId}/invites`).flush([]);
  });
});
