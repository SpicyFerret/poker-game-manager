import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { environment } from '../../../../environments/environment';
import { AuthService } from '../../../core/auth/auth.service';
import { Login } from './login';

describe('Login', () => {
  let fixture: ComponentFixture<Login>;
  let http: HttpTestingController;

  beforeEach(async () => {
    localStorage.clear();

    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(Login);
    http = TestBed.inject(HttpTestingController);
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  function form() {
    return fixture.componentInstance as unknown as {
      form: { setValue: (v: { email: string; password: string }) => void; invalid: boolean };
      submit: () => void;
    };
  }

  it('should keep the form invalid while the email is malformed', () => {
    form().form.setValue({ email: 'not-an-email', password: 'Password123' });
    expect(form().form.invalid).toBe(true);
  });

  it('should sign in and navigate home', async () => {
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    form().form.setValue({ email: 'a@b.com', password: 'Password123' });
    form().submit();

    http
      .expectOne(`${environment.apiUrl}/users/login`)
      .flush({ accessToken: 'access', refreshToken: 'refresh' });

    expect(TestBed.inject(AuthService).isAuthenticated()).toBe(true);
    expect(navigate).toHaveBeenCalledWith('/');
  });

  it('should show the API message when the credentials are rejected', async () => {
    form().form.setValue({ email: 'a@b.com', password: 'wrong' });
    form().submit();

    http
      .expectOne(`${environment.apiUrl}/users/login`)
      .flush(
        { detail: 'The provided credentials were invalid' },
        { status: 401, statusText: 'Unauthorized' },
      );

    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('[role="alert"]')?.textContent).toContain(
      'The provided credentials were invalid',
    );
  });
});
