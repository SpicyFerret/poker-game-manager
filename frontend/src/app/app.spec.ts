import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { environment } from '../environments/environment';
import { App } from './app';
import { AuthService } from './core/auth/auth.service';

/** Unsigned token carrying only the `sub` claim the client reads. */
function tokenFor(userId: string): string {
  return `header.${btoa(JSON.stringify({ sub: userId }))}.signature`;
}

describe('App', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    localStorage.clear();

    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should not show the account menu when signed out', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('button')).toBeNull();
  });

  it('should show the account menu once signed in', async () => {
    const auth = TestBed.inject(AuthService);
    auth.setTokens({ accessToken: 'access', refreshToken: 'refresh' });

    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('button')).not.toBeNull();
  });

  /**
   * The name left the bar when the button became a figure, so the only thing
   * naming the account is the label a screen reader reads.
   */
  it('should name the account on the avatar button', async () => {
    const auth = TestBed.inject(AuthService);
    auth.setTokens({ accessToken: 'access', refreshToken: 'refresh' });

    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const button = (fixture.nativeElement as HTMLElement).querySelector('.app-bar__avatar');

    expect(button?.getAttribute('aria-label')).toBeTruthy();
  });

  /**
   * Otherwise the profile is only ever fetched from the profile screen, and
   * the account menu's name goes on showing nothing until someone happens to
   * visit it first.
   */
  it('should ask for the profile as soon as the account is authenticated', () => {
    const auth = TestBed.inject(AuthService);
    auth.setTokens({ accessToken: tokenFor('u1'), refreshToken: 'refresh' });

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    http.expectOne(`${environment.apiUrl}/users/u1`).flush({
      id: 'u1',
      email: 'pedro@example.com',
      firstName: 'Pedro',
      lastName: 'Borin',
      displayName: 'Pedro',
      paymentType: null,
      paymentHandle: null,
    });

    expect(auth.user()?.displayName).toBe('Pedro');
  });

  /**
   * An empty menu item is not an empty state, it is a gap with nothing in it —
   * the row has to be genuinely absent until the name is known, not present
   * and blank.
   */
  it('should not show an empty name row before the profile is known, and should show it once loaded', async () => {
    const auth = TestBed.inject(AuthService);
    auth.setTokens({ accessToken: tokenFor('u1'), refreshToken: 'refresh' });

    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const trigger = (fixture.nativeElement as HTMLElement).querySelector(
      '.app-bar__avatar',
    ) as HTMLElement;
    trigger.click();
    await fixture.whenStable();

    expect(document.querySelector('.account-menu__who')).toBeNull();

    http.expectOne(`${environment.apiUrl}/users/u1`).flush({
      id: 'u1',
      email: 'pedro@example.com',
      firstName: 'Pedro',
      lastName: 'Borin',
      displayName: 'Pedro',
      paymentType: null,
      paymentHandle: null,
    });
    await fixture.whenStable();

    expect(document.querySelector('.account-menu__who')?.textContent?.trim()).toBe('Pedro');
  });
});
