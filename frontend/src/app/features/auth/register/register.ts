import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import { switchMap } from 'rxjs';

import { describeError } from '../../../core/api/problem-details';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-register',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressBarModule,
  ],
  templateUrl: './register.html',
})
export class Register {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    firstName: ['', [Validators.required]],
    lastName: ['', [Validators.required]],
    displayName: ['', [Validators.maxLength(40)]],
    email: ['', [Validators.required, Validators.email]],
    // Mirrors the API's RegisterUserCommandValidator so the player finds out
    // here rather than after a round trip.
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    const value = this.form.getRawValue();
    const displayName = value.displayName.trim();

    this.auth
      .register({
        email: value.email,
        firstName: value.firstName,
        lastName: value.lastName,
        password: value.password,
        displayName: displayName === '' ? undefined : displayName,
      })
      // Registering does not return tokens, so sign in straight away — asking
      // someone to type their password twice in a row is pointless friction.
      .pipe(switchMap(() => this.auth.login(value.email, value.password)))
      .subscribe({
        next: () => void this.router.navigateByUrl('/'),
        error: (err: unknown) => {
          this.submitting.set(false);
          this.error.set(
            describeError(err, $localize`:@@register.failed:Não foi possível criar a conta.`),
          );
        },
      });
  }
}
