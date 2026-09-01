import { Component, DestroyRef, OnInit, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { debounceTime } from 'rxjs';

import { describeError } from '../../../../core/api/problem-details';
import {
  ChampionshipRole,
  ChipSet,
  atLeast,
} from '../../../../core/championships/championship.models';
import { ChampionshipsService } from '../../../../core/championships/championships.service';
import { RealtimeService } from '../../../../core/realtime/realtime.service';
import { TableStatusLabelPipe } from '../../../../core/tables/table-status-label.pipe';
import {
  JoinPolicy,
  LateEntryPolicy,
  TableMood,
  TableSummary,
  isActive,
  isFinished,
  sortForDisplay,
  tableMood,
} from '../../../../core/tables/table.models';
import { TablesService } from '../../../../core/tables/tables.service';
import { Confirm } from '../../../../shared/confirm/confirm.service';

@Component({
  selector: 'app-tables-tab',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
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
  private readonly confirm = inject(Confirm);
  private readonly realtime = inject(RealtimeService);
  private readonly destroyRef = inject(DestroyRef);

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
    lateEntry: this.formBuilder.nonNullable.control<LateEntryPolicy>('Request'),
    smallChipReserve: this.formBuilder.control<number | null>(0),
  });

  ngOnInit(): void {
    this.load();

    // A table someone else started, finished, or opened shows up here without
    // waiting for a manual refresh.
    this.realtime
      .watch(this.championshipId())
      .pipe(debounceTime(300), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.load());
  }

  protected canManage(): boolean {
    return atLeast(this.callerRole(), 'TableManager');
  }

  protected isActive(table: TableSummary): boolean {
    return isActive(table.status);
  }

  protected moodOf(table: TableSummary): TableMood {
    return tableMood(table.status);
  }

  protected load(): void {
    this.tables.list(this.championshipId()).subscribe({
      // A finished table's result lives in the history tab from here on;
      // showing it here too would just be clutter next to the ones still
      // worth acting on.
      next: (items) => this.items.set(sortForDisplay(items.filter((t) => !isFinished(t.status)))),
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
      lateEntry: 'Request',
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

    this.confirm
      .ask({
        title: $localize`:@@confirm.createTableTitle:Abrir esta mesa?`,
        message: $localize`:@@confirm.createTableMessage:Nenhuma ficha sai da maleta agora — só quando a mesa for iniciada.`,
        details: [
          { label: $localize`:@@field.tableName:Nome da mesa`, value: value.name.trim() },
          {
            label: $localize`:@@field.buyIn:Buy-in (R$)`,
            value:
              value.buyIn === null
                ? $localize`:@@confirm.championshipDefault:padrão do campeonato`
                : String(value.buyIn),
          },
        ],
        confirmLabel: $localize`:@@tables.createSubmit:Abrir mesa`,
      })
      .subscribe(() => this.persist(value));
  }

  private persist(value: {
    name: string;
    chipSetId: string;
    buyIn: number | null;
    rebuy: number | null;
    joinPolicy: JoinPolicy;
    lateEntry: LateEntryPolicy;
    smallChipReserve: number | null;
  }): void {
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
        lateEntry: value.lateEntry,
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
