import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { describeError } from '../../../../core/api/problem-details';
import { Championship, Member } from '../../../../core/championships/championship.models';
import { ChampionshipsService } from '../../../../core/championships/championships.service';
import { formatPointsTable, parsePointsTable } from '../../points-table';

@Component({
  selector: 'app-settings-tab',
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatCheckboxModule,
    MatSelectModule,
    MatButtonModule,
  ],
  templateUrl: './settings.html',
  styleUrl: './settings.scss',
})
export class SettingsTab implements OnInit {
  private readonly championships = inject(ChampionshipsService);

  readonly championship = input.required<Championship>();
  readonly saved = output<void>();

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly savedMessage = signal(false);

  /** Only Admins can receive ownership, so only they are offered. */
  protected readonly admins = signal<Member[]>([]);
  protected readonly successorId = signal<string | null>(null);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(80)]],
    description: ['', [Validators.maxLength(500)]],
    defaultBuyIn: [0, [Validators.required, Validators.min(0.01)]],
    defaultRebuy: [0, [Validators.required, Validators.min(0)]],
    enforceDefaults: [false],
    moneyPerUnit: [0, [Validators.required, Validators.min(0.000001)]],
    pointsByPosition: [''],
  });

  ngOnInit(): void {
    const championship = this.championship();

    this.form.setValue({
      name: championship.name,
      description: championship.description ?? '',
      defaultBuyIn: championship.defaultBuyIn,
      defaultRebuy: championship.defaultRebuy,
      enforceDefaults: championship.enforceDefaults,
      moneyPerUnit: championship.moneyPerUnit,
      pointsByPosition: formatPointsTable(championship.pointsByPosition),
    });

    if (championship.role === 'Owner') {
      this.championships.members(championship.id).subscribe({
        next: (members) => this.admins.set(members.filter((m) => m.role === 'Admin')),
        error: () => this.admins.set([]),
      });
    }
  }

  protected isOwner(): boolean {
    return this.championship().role === 'Owner';
  }

  protected save(): void {
    if (this.form.invalid || this.busy()) {
      return;
    }

    const value = this.form.getRawValue();
    const points = parsePointsTable(value.pointsByPosition);

    if (points === null || points.length === 0) {
      this.error.set(
        $localize`:@@championships.pointsInvalid:A tabela de pontos deve ser uma lista de números separados por vírgula.`,
      );
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    this.savedMessage.set(false);

    this.championships
      .updateSettings(this.championship().id, {
        name: value.name.trim(),
        description: value.description.trim() === '' ? null : value.description.trim(),
        defaultBuyIn: value.defaultBuyIn,
        defaultRebuy: value.defaultRebuy,
        enforceDefaults: value.enforceDefaults,
        moneyPerUnit: value.moneyPerUnit,
        pointsByPosition: points,
      })
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.savedMessage.set(true);
          this.saved.emit();
        },
        error: (err: unknown) => {
          this.busy.set(false);
          this.error.set(
            describeError(err, $localize`:@@settings.saveFailed:Não foi possível salvar.`),
          );
        },
      });
  }

  protected transfer(): void {
    const successorId = this.successorId();

    if (successorId === null || this.busy()) {
      return;
    }

    this.busy.set(true);
    this.error.set(null);

    this.championships.transferOwnership(this.championship().id, successorId).subscribe({
      next: () => {
        this.busy.set(false);
        // The caller is now an Admin, so the parent has to reload: this whole
        // tab is about to disappear from under them.
        this.saved.emit();
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.error.set(
          describeError(
            err,
            $localize`:@@settings.transferFailed:Não foi possível transferir a propriedade.`,
          ),
        );
      },
    });
  }
}
