import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';

import { describeError } from '../../core/api/problem-details';
import { PaymentHandleType } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-profile',
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatProgressBarModule,
  ],
  templateUrl: './profile.html',
})
export class Profile implements OnInit {
  private readonly auth = inject(AuthService);

  protected readonly loading = signal(true);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly saved = signal(false);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    displayName: ['', [Validators.required, Validators.maxLength(40)]],
    paymentType: [null as PaymentHandleType | null],
    paymentHandle: [''],
  });

  ngOnInit(): void {
    this.auth.loadProfile().subscribe({
      next: (profile) => {
        this.form.patchValue({
          displayName: profile.displayName,
          paymentType: profile.paymentType,
          paymentHandle: profile.paymentHandle ?? '',
        });
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(
          describeError(err, $localize`:@@profile.loadFailed:Não foi possível carregar o perfil.`),
        );
      },
    });
  }

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    this.saved.set(false);

    const value = this.form.getRawValue();
    const handle = value.paymentHandle.trim();

    this.auth
      .updateProfile({
        displayName: value.displayName.trim(),
        // The API rejects one without the other, so send the pair or neither.
        paymentType: handle === '' ? null : value.paymentType,
        paymentHandle: value.paymentType === null || handle === '' ? null : handle,
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.saved.set(true);
        },
        error: (err: unknown) => {
          this.submitting.set(false);
          this.error.set(
            describeError(err, $localize`:@@profile.saveFailed:Não foi possível salvar o perfil.`),
          );
        },
      });
  }
}
