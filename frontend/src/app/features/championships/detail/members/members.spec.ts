import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Observable, of } from 'rxjs';

import { environment } from '../../../../../environments/environment';
import { ChampionshipRole, Member } from '../../../../core/championships/championship.models';
import { Confirm } from '../../../../shared/confirm/confirm.service';
import { MembersTab } from './members';

describe('MembersTab', () => {
  const championshipId = '9a1f1d4c-1c58-4d8e-9a0b-0f2b3c4d5e6f';

  const members: Member[] = [
    {
      userId: 'u1',
      displayName: 'Pedro',
      role: 'Player',
      joinedAtUtc: '2026-01-01T00:00:00Z',
      hasPaymentHandle: true,
    },
  ];

  let fixture: ComponentFixture<MembersTab>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MembersTab],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        // Auto-confirms, so a click drives the request straight through
        // without a real dialog in the way.
        { provide: Confirm, useValue: { ask: (): Observable<void> => of(undefined) } },
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);

    fixture = TestBed.createComponent(MembersTab);
    fixture.componentRef.setInput('championshipId', championshipId);
    fixture.componentRef.setInput('callerRole', 'Owner');
    await fixture.whenStable();

    http.expectOne(`${environment.apiUrl}/championships/${championshipId}/members`).flush(members);
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  it('should show the role and remove icons for a member the caller can act on', () => {
    expect(el().querySelectorAll('.member-row__actions button').length).toBe(2);
  });

  it('should not offer actions on a member the caller cannot act on', async () => {
    fixture.componentRef.setInput('callerRole', 'Player');
    await fixture.whenStable();

    expect(el().querySelector('.member-row__actions')).toBeNull();
  });

  /**
   * The menu itself is a CDK overlay Material already tests; what is ours to
   * verify is that picking an item there drives the right request, which is
   * exactly what the component method does when the menu calls it.
   */
  it('should change the role once a menu item is picked', () => {
    (
      fixture.componentInstance as unknown as {
        changeRole: (m: Member, r: ChampionshipRole) => void;
      }
    ).changeRole(members[0], 'Admin');

    const request = http.expectOne(
      `${environment.apiUrl}/championships/${championshipId}/members/u1/role`,
    );
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ role: 'Admin' });
    request.flush(null);

    // Reloads after a confirmed change — once as the confirmation itself
    // completes, once more once the change succeeds.
    for (const reload of http.match(
      `${environment.apiUrl}/championships/${championshipId}/members`,
    )) {
      reload.flush(members);
    }
  });

  it('should remove the member once the trash icon is confirmed', () => {
    const buttons = el().querySelectorAll('.member-row__actions button');
    (buttons[1] as HTMLElement).click();

    const request = http.expectOne(
      `${environment.apiUrl}/championships/${championshipId}/members/u1`,
    );
    expect(request.request.method).toBe('DELETE');
    request.flush(null);

    http.expectOne(`${environment.apiUrl}/championships/${championshipId}/members`).flush([]);
  });
});
