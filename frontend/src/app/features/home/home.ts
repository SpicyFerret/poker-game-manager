import { Component, OnInit, inject } from '@angular/core';
import { MatCardModule } from '@angular/material/card';

import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-home',
  imports: [MatCardModule],
  templateUrl: './home.html',
})
export class Home implements OnInit {
  protected readonly auth = inject(AuthService);

  ngOnInit(): void {
    if (!this.auth.user()) {
      // Failure is not fatal here: the toolbar just falls back to no name.
      this.auth.loadProfile().subscribe({ error: () => undefined });
    }
  }
}
