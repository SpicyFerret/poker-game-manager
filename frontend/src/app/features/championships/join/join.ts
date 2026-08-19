import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router } from '@angular/router';

import { describeError } from '../../../core/api/problem-details';
import { ChampionshipsService } from '../../../core/championships/championships.service';

@Component({
  selector: 'app-championship-join',
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressBarModule,
  ],
  templateUrl: './join.html',
})
export class ChampionshipJoin {
  private readonly championships = inject(ChampionshipsService);
  private readonly router = inject(Router);

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    code: ['', [Validators.required]],
  });

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    // Sent as typed. The API normalises case, spaces and dashes, so someone
    // reading a code out at the table doesn't have to be careful.
    this.championships.join(this.form.getRawValue().code).subscribe({
      next: (result) => void this.router.navigate(['/championships', result.championshipId]),
      error: (err: unknown) => {
        this.submitting.set(false);
        this.error.set(
          describeError(err, $localize`:@@join.failed:Código inválido ou já expirado.`),
        );
      },
    });
  }
}
