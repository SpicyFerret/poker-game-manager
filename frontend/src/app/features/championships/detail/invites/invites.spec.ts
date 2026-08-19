import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { environment } from '../../../../../environments/environment';
import { InvitesTab } from './invites';

describe('InvitesTab', () => {
  let fixture: ComponentFixture<InvitesTab>;
  let http: HttpTestingController;

  const championshipId = '9a1f1d4c-1c58-4d8e-9a0b-0f2b3c4d5e6f';
  const invitesUrl = `${environment.apiUrl}/championships/${championshipId}/invites`;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [InvitesTab],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(InvitesTab);
    fixture.componentRef.setInput('championshipId', championshipId);
    fixture.componentRef.setInput('callerRole', 'Owner');
    await fixture.whenStable();

    http = TestBed.inject(HttpTestingController);
    http.expectOne(invitesUrl).flush([]);
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  function component() {
    return fixture.componentInstance as unknown as { create: () => void };
  }

  /**
   * Types into the real input so Angular's value accessor converts the value the
   * way it does for a person. Setting the control directly would not: the bug
   * this covers only exists because <input type="number"> hands the control a
   * number, whatever it was initialised with.
   */
  async function typeMaxUses(value: string): Promise<void> {
    const input = (fixture.nativeElement as HTMLElement).querySelector(
      'input[type="number"]',
    ) as HTMLInputElement;

    input.value = value;
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();
  }

  it('should send null when no limit is typed', async () => {
    component().create();

    const request = http.expectOne(invitesUrl);
    expect(request.request.body.maxUses).toBeNull();
    request.flush({
      id: 'a',
      code: 'ABCD23',
      role: 'Player',
      expiresAtUtc: null,
      maxUses: null,
      uses: 0,
      isRevoked: false,
    });

    http.expectOne(invitesUrl).flush([]);
  });

  it('should send the typed limit as a number', async () => {
    // The regression: the control was declared as a string, so the number the
    // accessor wrote made create() throw before any request went out — the
    // button silently did nothing.
    await typeMaxUses('5');

    component().create();

    const request = http.expectOne(invitesUrl);
    expect(request.request.body.maxUses).toBe(5);
    request.flush({
      id: 'a',
      code: 'ABCD23',
      role: 'Player',
      expiresAtUtc: null,
      maxUses: 5,
      uses: 0,
      isRevoked: false,
    });

    http.expectOne(invitesUrl).flush([]);
  });

  it('should send null again after the limit is cleared', async () => {
    await typeMaxUses('5');
    await typeMaxUses('');

    component().create();

    const request = http.expectOne(invitesUrl);
    expect(request.request.body.maxUses).toBeNull();
    request.flush({
      id: 'a',
      code: 'ABCD23',
      role: 'Player',
      expiresAtUtc: null,
      maxUses: null,
      uses: 0,
      isRevoked: false,
    });

    http.expectOne(invitesUrl).flush([]);
  });

  it('should refuse a limit below one without calling the API', async () => {
    await typeMaxUses('0');

    component().create();

    http.expectNone(invitesUrl);

    await fixture.whenStable();

    const alert = (fixture.nativeElement as HTMLElement).querySelector('[role="alert"]');
    expect(alert?.textContent).toBeTruthy();
  });

  it('should only offer roles below the caller', () => {
    const options = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(options).not.toContain('Dono');
  });
});
