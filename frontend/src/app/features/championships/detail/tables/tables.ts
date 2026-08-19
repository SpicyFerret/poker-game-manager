import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatListModule } from '@angular/material/list';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';

import { describeError } from '../../../../core/api/problem-details';
import {
  ChampionshipRole,
  ChipSet,
  atLeast,
} from '../../../../core/championships/championship.models';
import { ChampionshipsService } from '../../../../core/championships/championships.service';
import { TableStatusLabelPipe } from '../../../../core/tables/table-status-label.pipe';
import {
  JoinPolicy,
  TableSummary,
  isActive,
  sortForDisplay,
} from '../../../../core/tables/table.models';
import { TablesService } from '../../../../core/tables/tables.service';

@Component({
  selector: 'app-tables-tab',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatListModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    TableStatusLabelPipe,
  ],
  templateUrl: './tables.html',
  styleUrl: './tables.scss',
})
export class TablesTab implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly tables = inject(TablesService);
  private readonly championships = inject(ChampionshipsService);
  private readonly router = inject(Router);

  readonly championshipId = input.required<string>();
  readonly callerRole = input.required<ChampionshipRole>();

  protected readonly items = signal<TableSummary[]>([]);
  protected readonly chipSets = signal<ChipSet[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly creating = signal(false);

  protected readonly form = this.formBuilder.group({
    name: this.formBuilder.nonNullable.control('', [Validators.required, Validators.maxLength(80)]),
    chipSetId: this.formBuilder.nonNullable.control('', [Validators.required]),
    // number | null throughout: <input type="number"> hands the control a number,
    // or null when cleared, whatever it was initialised with.
    buyIn: this.formBuilder.control<number | null>(null),
    rebuy: this.formBuilder.control<number | null>(null),
    joinPolicy: this.formBuilder.nonNullable.control<JoinPolicy>('AnyMember'),
    allowLateEntry: this.formBuilder.nonNullable.control(true),
    smallChipReserve: this.formBuilder.control<number | null>(0),
  });

  ngOnInit(): void {
    this.load();
  }

  protected canManage(): boolean {
    return atLeast(this.callerRole(), 'TableManager');
  }

  protected isActive(table: TableSummary): boolean {
    return isActive(table.status);
  }

  protected load(): void {
    this.tables.list(this.championshipId()).subscribe({
      next: (items) => this.items.set(sortForDisplay(items)),
      error: (err: unknown) =>
        this.error.set(
          describeError(err, $localize`:@@tables.loadFailed:Não foi possível carregar as mesas.`),
        ),
    });

    this.championships.chipSets(this.championshipId()).subscribe({
      next: (sets) => this.chipSets.set(sets),
      error: () => this.chipSets.set([]),
    });
  }

  protected startNew(): void {
    this.form.reset({
      name: '',
      chipSetId: this.chipSets()[0]?.id ?? '',
      joinPolicy: 'AnyMember',
      allowLateEntry: true,
      smallChipReserve: 0,
    });
    this.creating.set(true);
  }

  protected cancel(): void {
    this.creating.set(false);
    this.error.set(null);
  }

  protected create(): void {
    if (this.form.invalid || this.busy()) {
      return;
    }

    const value = this.form.getRawValue();

    this.busy.set(true);
    this.error.set(null);

    this.tables
      .create(this.championshipId(), {
        name: value.name.trim(),
        chipSetId: value.chipSetId,
        // Null lets the API fall back to the championship's default, which is
        // also what it enforces when the championship says the stakes are fixed.
        buyIn: value.buyIn,
        rebuy: value.rebuy,
        joinPolicy: value.joinPolicy,
        allowLateEntry: value.allowLateEntry,
        smallChipReserve: value.smallChipReserve ?? 0,
      })
      .subscribe({
        next: (tableId) => {
          this.busy.set(false);
          this.creating.set(false);
          void this.router.navigate(['/championships', this.championshipId(), 'tables', tableId]);
        },
        error: (err: unknown) => {
          this.busy.set(false);
          this.error.set(
            describeError(err, $localize`:@@tables.createFailed:Não foi possível criar a mesa.`),
          );
        },
      });
  }
}
