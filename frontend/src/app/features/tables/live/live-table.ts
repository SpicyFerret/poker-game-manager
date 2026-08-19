import { DestroyRef, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatListModule } from '@angular/material/list';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Subject, merge, switchMap, timer } from 'rxjs';

import { NavSection, SectionNav } from '../../../shared/section-nav/section-nav';
import { ChipColour, chipColour } from '../../../shared/chip-colours';

import { describeError } from '../../../core/api/problem-details';
import {
  TableDetail,
  TablePlayer,
  issuedUnits,
  remainingUnits,
  stacksLeft,
} from '../../../core/tables/table.models';
import {
  PlayerStatusLabelPipe,
  TableStatusLabelPipe,
} from '../../../core/tables/table-status-label.pipe';
import { TablesService } from '../../../core/tables/tables.service';
import { ChipTradeDialog, ChipTradeResult } from './chip-trade-dialog';

/**
 * Refresh interval while a table is live. Deliberately unhurried: the events
 * worth seeing — a rebuy, someone sitting down — happen at human speed, and this
 * runs on phones on a home wifi, several of them at once.
 */
const POLL_MS = 5000;

@Component({
  selector: 'app-live-table',
  imports: [
    MatCardModule,
    MatListModule,
    MatButtonModule,
    MatProgressBarModule,
    MatDialogModule,
    SectionNav,
    TableStatusLabelPipe,
    PlayerStatusLabelPipe,
  ],
  templateUrl: './live-table.html',
  styleUrl: './live-table.scss',
})
export class LiveTable implements OnInit {
  private readonly tables = inject(TablesService);
  private readonly dialog = inject(MatDialog);
  private readonly destroyRef = inject(DestroyRef);

  readonly championshipId = input.required<string>();
  readonly tableId = input.required<string>();

  /** Forces an immediate refresh after an action, rather than waiting a tick. */
  private readonly refreshNow = new Subject<void>();

  protected readonly table = signal<TableDetail | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly loading = signal(true);

  protected readonly remainingUnits = computed(() => {
    const table = this.table();
    return table ? remainingUnits(table.stock) : 0;
  });

  protected readonly issuedUnits = computed(() => {
    const table = this.table();
    return table ? issuedUnits(table.stock) : 0;
  });

  protected readonly stacksLeft = computed(() => {
    const table = this.table();
    return table ? stacksLeft(table.stock, table.buyInUnits) : 0;
  });

  protected readonly section = signal('players');

  protected readonly sections = computed<NavSection[]>(() => [
    { id: 'players', label: $localize`:@@tableSection.players:Jogadores` },
    { id: 'case', label: $localize`:@@tableSection.case:Maleta` },
  ]);

  /** Resolves a stored colour token, or null for anything unrecognised. */
  protected colourOf(token: string | null | undefined): ChipColour | null {
    return chipColour(token);
  }

  protected readonly playing = computed(
    () => this.table()?.players.filter((p) => p.status === 'Playing') ?? [],
  );

  ngOnInit(): void {
    merge(timer(0, POLL_MS), this.refreshNow)
      .pipe(
        // switchMap, so a slow response is dropped rather than queued behind the
        // next tick and applied out of order.
        switchMap(() => this.tables.get(this.championshipId(), this.tableId())),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (table) => {
          this.table.set(table);
          this.loading.set(false);
          this.error.set(null);
        },
        error: (err: unknown) => {
          this.loading.set(false);
          this.error.set(
            describeError(err, $localize`:@@table.loadFailed:Não foi possível carregar a mesa.`),
          );
        },
      });
  }

  protected isLive(): boolean {
    const status = this.table()?.status;
    return status === 'Open' || status === 'Running';
  }

  protected canStart(): boolean {
    const table = this.table();
    return (
      table !== null &&
      table.canManage &&
      table.status === 'Open' &&
      table.players.some((p) => p.status === 'Standby')
    );
  }

  protected canJoin(): boolean {
    const table = this.table();
    return table !== null && table.myPlayerId === null && this.isLive();
  }

  protected start(): void {
    this.run(this.tables.start(this.championshipId(), this.tableId()), {
      fallback: $localize`:@@table.startFailed:Não foi possível iniciar a mesa.`,
    });
  }

  protected join(): void {
    const table = this.table();
    const code = table?.joinPolicy === 'Code' ? (prompt(this.codePrompt()) ?? '') : null;

    this.run(this.tables.join(this.championshipId(), this.tableId(), code), {
      fallback: $localize`:@@table.joinFailed:Não foi possível entrar na mesa.`,
    });
  }

  protected rebuy(player: TablePlayer): void {
    this.run(
      this.tables.issueStack(this.championshipId(), this.tableId(), player.tablePlayerId, true),
      { fallback: $localize`:@@table.rebuyFailed:Não foi possível fazer o rebuy.` },
    );
  }

  protected dealIn(player: TablePlayer): void {
    this.run(
      this.tables.issueStack(this.championshipId(), this.tableId(), player.tablePlayerId, false),
      { fallback: $localize`:@@table.dealInFailed:Não foi possível dar fichas ao jogador.` },
    );
  }

  protected tradeChips(buyer: TablePlayer): void {
    const table = this.table();

    if (!table) {
      return;
    }

    this.dialog
      .open(ChipTradeDialog, {
        data: {
          buyer,
          sellers: this.playing().filter((p) => p.tablePlayerId !== buyer.tablePlayerId),
          defaultAmount: table.rebuy,
        },
      })
      .afterClosed()
      .subscribe((result: ChipTradeResult | undefined) => {
        if (!result) {
          return;
        }

        this.run(
          this.tables.buyChipsFromPlayer(
            this.championshipId(),
            this.tableId(),
            buyer.tablePlayerId,
            result.sellerPlayerId,
            result.amount,
          ),
          {
            fallback: $localize`:@@table.tradeFailed:Não foi possível registrar a compra de fichas.`,
          },
        );
      });
  }

  private codePrompt(): string {
    return $localize`:@@table.codePrompt:Código da mesa`;
  }

  private run(action: ReturnType<TablesService['start']>, options: { fallback: string }): void {
    if (this.busy()) {
      return;
    }

    this.busy.set(true);
    this.error.set(null);

    action.subscribe({
      next: () => {
        this.busy.set(false);
        this.refreshNow.next();
      },
      error: (err: unknown) => {
        this.busy.set(false);
        // The API's message is the useful one here: "the chip case cannot cover
        // this, N units short" tells the manager exactly what to do next.
        this.error.set(describeError(err, options.fallback));
      },
    });
  }
}
