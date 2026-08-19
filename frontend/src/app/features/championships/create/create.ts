import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router } from '@angular/router';

import { describeError } from '../../../core/api/problem-details';
import { ChampionshipsService } from '../../../core/championships/championships.service';
import { parsePointsTable } from '../points-table';

@Component({
  selector: 'app-championship-create',
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatCheckboxModule,
    MatButtonModule,
    MatProgressBarModule,
  ],
  templateUrl: './create.html',
})
export class ChampionshipCreate {
  private readonly championships = inject(ChampionshipsService);
  private readonly router = inject(Router);

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(80)]],
    description: ['', [Validators.maxLength(500)]],
    defaultBuyIn: [50, [Validators.required, Validators.min(0.01)]],
    defaultRebuy: [50, [Validators.required, Validators.min(0)]],
    enforceDefaults: [false],
    // A 1000-unit stack for a R$ 50 buy-in — the arrangement most home games
    // land on anyway.
    moneyPerUnit: [0.05, [Validators.required, Validators.min(0.000001)]],
    pointsByPosition: ['10, 7, 5, 3, 2, 1'],
  });

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      return;
    }

    const points = parsePointsTable(this.form.getRawValue().pointsByPosition);

    if (points === null) {
      this.error.set(
        $localize`:@@championships.pointsInvalid:A tabela de pontos deve ser uma lista de números separados por vírgula.`,
      );
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    const value = this.form.getRawValue();

    this.championships
      .create({
        name: value.name.trim(),
        description: value.description.trim() === '' ? null : value.description.trim(),
        defaultBuyIn: value.defaultBuyIn,
        defaultRebuy: value.defaultRebuy,
        enforceDefaults: value.enforceDefaults,
        moneyPerUnit: value.moneyPerUnit,
        pointsByPosition: points,
      })
      .subscribe({
        next: (id) => void this.router.navigate(['/championships', id]),
        error: (err: unknown) => {
          this.submitting.set(false);
          this.error.set(
            describeError(
              err,
              $localize`:@@championships.createFailed:Não foi possível criar o campeonato.`,
            ),
          );
        },
      });
  }
}
