import { DestroyRef, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router } from '@angular/router';
import { Observable, Subject, merge, switchMap, timer } from 'rxjs';

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
import { ChipColour, chipColour } from '../../../shared/chip-colours';
import { ConfirmDetail } from '../../../shared/confirm/confirm-dialog';
import { Confirm } from '../../../shared/confirm/confirm.service';
import { NavSection, SectionNav } from '../../../shared/section-nav/section-nav';
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
  private readonly confirm = inject(Confirm);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly championshipId = input.required<string>();
  readonly tableId = input.required<string>();

  /** Forces an immediate refresh after an action, rather than waiting a tick. */
  private readonly refreshNow = new Subject<void>();

  protected readonly table = signal<TableDetail | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly loading = signal(true);

  protected readonly section = signal('players');

  protected readonly sections = computed<NavSection[]>(() => [
    { id: 'players', label: $localize`:@@tableSection.players:Jogadores` },
    { id: 'case', label: $localize`:@@tableSection.case:Maleta` },
  ]);

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

  protected readonly playing = computed(
    () => this.table()?.players.filter((p) => p.status === 'Playing') ?? [],
  );

  /** Resolves a stored colour token, or null for anything unrecognised. */
  protected colourOf(token: string | null | undefined): ChipColour | null {
    return chipColour(token);
  }

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
    const table = this.table();

    if (!table) {
      return;
    }

    const waiting = table.players.filter((p) => p.status === 'Standby').length;

    this.confirm
      .ask({
        title: $localize`:@@confirm.startTitle:Iniciar a mesa?`,
        message: $localize`:@@confirm.startMessage:Cada jogador recebe um stack e as fichas saem da maleta. Se ela não cobrir todos, nada é entregue.`,
        details: [
          {
            label: $localize`:@@confirm.startPlayers:jogadores aguardando`,
            value: String(waiting),
          },
          {
            label: $localize`:@@confirm.startBuyIn:buy-in cada`,
            value: `R$ ${table.buyIn}`,
          },
        ],
        confirmLabel: $localize`:@@table.start:Iniciar mesa`,
      })
      .subscribe(() => {
        this.run(this.tables.start(this.championshipId(), this.tableId()), {
          fallback: $localize`:@@table.startFailed:Não foi possível iniciar a mesa.`,
        });
      });
  }

  protected join(): void {
    const table = this.table();
    const code = table?.joinPolicy === 'Code' ? (prompt(this.codePrompt()) ?? '') : null;

    this.confirm
      .ask({
        title: $localize`:@@confirm.joinTitle:Entrar nesta mesa?`,
        message: $localize`:@@confirm.joinMessage:Você entra aguardando. As fichas só saem quando o gerente iniciar ou te distribuir.`,
        confirmLabel: $localize`:@@table.join:Entrar na mesa`,
      })
      .subscribe(() => {
        this.run(this.tables.join(this.championshipId(), this.tableId(), code), {
          fallback: $localize`:@@table.joinFailed:Não foi possível entrar na mesa.`,
        });
      });
  }

  protected rebuy(player: TablePlayer): void {
    this.confirmStack(player, true);
  }

  protected dealIn(player: TablePlayer): void {
    this.confirmStack(player, false);
  }

  /**
   * Asks the server what this stack would be made of, and puts that list in the
   * confirmation — because the person tapping the button is also the person who
   * has to count those chips out of the case.
   */
  private confirmStack(player: TablePlayer, isRebuy: boolean): void {
    if (this.busy()) {
      return;
    }

    this.busy.set(true);
    this.error.set(null);

    this.tables.stackPreview(this.championshipId(), this.tableId(), isRebuy).subscribe({
      next: (preview) => {
        this.busy.set(false);

        const details: ConfirmDetail[] = preview.chips.map((chip) => {
          const colour = this.colourOf(chip.colour);

          return {
            label: $localize`:@@confirm.chipOf:ficha ${chip.faceValue}:VALUE:`,
            value: `${chip.quantity}x`,
            swatch: colour?.swatch,
            ink: colour?.ink,
          };
        });

        this.confirm
          .ask({
            title: isRebuy
              ? $localize`:@@confirm.rebuyTitle:Rebuy de ${player.displayName}:NAME:?`
              : $localize`:@@confirm.dealInTitle:Dar fichas para ${player.displayName}:NAME:?`,
            message: $localize`:@@confirm.stackMessage:Entregue exatamente estas fichas da maleta:`,
            details,
            confirmLabel: $localize`:@@confirm.stackConfirm:Entreguei as fichas`,
            // The API would refuse this anyway; saying so here stops the manager
            // counting chips for a stack that was never going to be dealt.
            blockedReason: preview.isPossible
              ? undefined
              : $localize`:@@confirm.stackBlocked:A maleta não fecha este stack: faltam ${preview.shortfallUnits}:UNITS: unidades.`,
          })
          .subscribe(() => {
            this.run(
              this.tables.issueStack(
                this.championshipId(),
                this.tableId(),
                player.tablePlayerId,
                isRebuy,
              ),
              {
                fallback: isRebuy
                  ? $localize`:@@table.rebuyFailed:Não foi possível fazer o rebuy.`
                  : $localize`:@@table.dealInFailed:Não foi possível dar fichas ao jogador.`,
              },
            );
          });
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.error.set(
          describeError(
            err,
            $localize`:@@table.previewFailed:Não foi possível calcular as fichas deste stack.`,
          ),
        );
      },
    });
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

        const seller = table.players.find((p) => p.tablePlayerId === result.sellerPlayerId);

        this.confirm
          .ask({
            title: $localize`:@@confirm.tradeTitle:Registrar a compra de fichas?`,
            message: $localize`:@@confirm.tradeMessage:Nenhuma ficha sai da maleta. Quem vende é creditado no mesmo valor.`,
            details: [
              {
                label: $localize`:@@trade.buyer:Comprador`,
                value: buyer.displayName,
              },
              {
                label: $localize`:@@trade.seller:Vendedor`,
                value: seller?.displayName ?? '',
              },
              {
                label: $localize`:@@trade.amount:Valor (R$)`,
                value: String(result.amount),
              },
            ],
            confirmLabel: $localize`:@@trade.confirm:Registrar`,
          })
          .subscribe(() => {
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
      });
  }

  protected deleteTable(): void {
    const table = this.table();

    if (!table) {
      return;
    }

    this.confirm
      .ask({
        title: $localize`:@@confirm.deleteTableTitle:Excluir a mesa?`,
        message: $localize`:@@confirm.deleteTableMessage:Some tudo desta noite: jogadores, lançamentos, contagens e o acerto. Não dá para desfazer.`,
        destructive: true,
        requireTyped: table.name,
        requireTypedLabel: $localize`:@@confirm.typeTableName:Digite o nome da mesa`,
        confirmLabel: $localize`:@@common.delete:Excluir`,
      })
      .subscribe(() => {
        this.busy.set(true);

        this.tables.delete(this.championshipId(), this.tableId(), table.name).subscribe({
          next: () => void this.router.navigate(['/championships', this.championshipId()]),
          error: (err: unknown) => {
            this.busy.set(false);
            this.error.set(
              describeError(err, $localize`:@@table.deleteFailed:Não foi possível excluir a mesa.`),
            );
          },
        });
      });
  }

  private codePrompt(): string {
    return $localize`:@@table.codePrompt:Código da mesa`;
  }

  private run(action: Observable<void>, options: { fallback: string }): void {
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
