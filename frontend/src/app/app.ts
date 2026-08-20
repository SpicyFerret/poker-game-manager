import { Component, effect, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatMenuModule } from '@angular/material/menu';
import { Router, RouterLink, RouterOutlet } from '@angular/router';

import { AuthService } from './core/auth/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, MatCardModule, MatButtonModule, MatMenuModule],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);

  constructor() {
    // Otherwise the profile is only ever fetched from the profile screen
    // itself, and the account menu's name goes on showing nothing — as an
    // empty menu item, not just a blank space next to a caret — until someone
    // happens to visit it first.
    effect(() => {
      if (this.auth.isAuthenticated() && this.auth.user() === null) {
        try {
          this.auth.loadProfile().subscribe({ error: () => undefined });
        } catch {
          // No subject claim to load a profile for — a malformed or stale
          // token, which the auth interceptor's own 401 handling deals with.
        }
      }
    });
  }

  /**
   * The button shows a figure rather than a name, so the name has to reach a
   * screen reader some other way.
   */
  protected accountLabel(): string {
    const name = this.auth.user()?.displayName;

    return name
      ? $localize`:@@app.accountOf:Conta de ${name}:NAME:`
      : $localize`:@@app.account:Sua conta`;
  }

  protected logout(): void {
    this.auth.logout();
    void this.router.navigate(['/login']);
  }
}
