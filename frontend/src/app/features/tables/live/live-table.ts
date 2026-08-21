import { DecimalPipe } from '@angular/common';
import { DestroyRef, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router } from '@angular/router';
import { Observable, Subject, merge, switchMap, timer } from 'rxjs';

import { describeError } from '../../../core/api/problem-details';
import { Member } from '../../../core/championships/championship.models';
import { ChampionshipsService } from '../../../core/championships/championships.service';
import {
  BlindLevel,
  BlindLevelInput,
  Blinds,
  ChipCountEntry,
  ClockAction,
  Reconciliation,
  Settlement,
  TableDetail,
  TablePlayer,
  formatDuration,
  issuedUnits,
  offBy,
  remainingUnits,
  secondsLeft,
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
import { AddTablePlayerDialog } from './add-table-player-dialog';
import { BlindLevelsDialog } from './blind-levels-dialog';
import { ChipTradeDialog, ChipTradeResult } from './chip-trade-dialog';
import { StackNoticeDialog } from './stack-notice-dialog';
import { CountDialog } from './count-dialog';

/**
 * Refresh interval while a table is live. Deliberately unhurried: the events
 * worth seeing — a rebuy, someone sitting down — happen at human speed, and this
 * runs on phones on a home wifi, several of them at once.
 */
const POLL_MS = 5000;

/**
 * How often the clock face redraws. Local only — it re-reads the last sample the
 * server sent rather than asking again, so a smooth second hand costs nothing on
 * the network.
 */
const TICK_MS = 1000;

@Component({
  selector: 'app-live-table',
  imports: [
    DecimalPipe,
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
  private readonly championships = inject(ChampionshipsService);
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

  protected readonly copiedHandle = signal<string | null>(null);

  protected readonly blinds = signal<Blinds | null>(null);

  /**
   * When the clock sample in `blinds` reached this phone. The remaining time is
   * worked out from the sample plus however long ago it landed, so it keeps
   * ticking between polls without drifting away from everyone else's screen.
   */
  private sampledAt = 0;

  /** Drives the redraw. Its value is the local time, not anything from the server. */
  private readonly tick = signal(0);

  /**
   * The stack notice on screen right now, if any. One at a time: they arrive as
   * a queue — a buy-in and a rebuy can both be waiting — and stacking dialogs on
   * a phone would bury the first one.
   */
  private showingNoticeFor: string | null = null;

  protected readonly reconciliation = signal<Reconciliation | null>(null);
  protected readonly settlement = signal<Settlement | null>(null);

  /**
   * Off by default: the players list reads clean, and everyone drives their
   * own rebuy and chip purchases from the fixed footer below. A manager flips
   * this on to reach the same controls for someone else's row.
   */
  protected readonly manageOthers = signal(false);

  protected readonly section = signal('players');

  protected readonly sections = computed<NavSection[]>(() => {
    const status = this.table()?.status;

    const sections: NavSection[] = [
      { id: 'players', label: $localize`:@@tableSection.players:Jogadores` },
      { id: 'case', label: $localize`:@@tableSection.case:Maleta` },
    ];

    // Only where there is something to show or set up: a table with no ladder
    // and nobody able to add one has no use for the section.
    if (this.blinds()?.levels.length || this.table()?.canManage) {
      sections.push({ id: 'blinds', label: $localize`:@@tableSection.blinds:Blinds` });
    }

    if (status === 'Counting') {
      sections.push({ id: 'counting', label: $localize`:@@tableSection.counting:Contagem` });
    }

    if (status === 'Settled' || status === 'Closed') {
      sections.push({ id: 'settlement', label: $localize`:@@tableSection.settlement:Acerto` });
    }

    return sections;
  });

  /** The chips whose count does not tally — the only ones worth recounting. */
  protected readonly offBy = computed(() => offBy(this.reconciliation()?.lines ?? []));

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

  /** The caller's own seat at this table, if they have one. */
  protected readonly ownPlayer = computed<TablePlayer | undefined>(() => {
    const table = this.table();

    return table?.players.find((p) => p.tablePlayerId === table.myPlayerId);
  });

  /**
   * The fixed footer is for topping up your own stack — it appears once you
   * are actually seated and playing, table-wide, not per section.
   */
  protected readonly canUseOwnFooter = computed(() => {
    const table = this.table();

    return table !== null && table.status === 'Running' && this.ownPlayer()?.status === 'Playing';
  });

  protected readonly currentLevel = computed<BlindLevel | undefined>(() => {
    const blinds = this.blinds();

    return blinds?.levels.find((level) => level.order === blinds.clock?.currentLevel);
  });

  protected readonly nextLevel = computed<BlindLevel | undefined>(() => {
    const blinds = this.blinds();

    return blinds?.levels.find((level) => level.order === (blinds.clock?.currentLevel ?? 0) + 1);
  });

  protected readonly remaining = computed(() => {
    const blinds = this.blinds();

    if (!blinds?.clock) {
      return 0;
    }

    // Reading the tick is what subscribes this to the redraw.
    const now = this.tick() || Date.now();

    return secondsLeft(this.currentLevel(), blinds.clock, (now - this.sampledAt) / 1000);
  });

  protected readonly clockFace = computed(() => formatDuration(this.remaining()));

  /** A level with no duration runs until someone moves it on, so there is nothing to count. */
  protected isTimed(): boolean {
    return (this.currentLevel()?.durationSeconds ?? 0) > 0;
  }

  protected isOver(): boolean {
    return this.isTimed() && this.remaining() === 0;
  }

  /**
   * Everyone who owes a count: anyone who was ever dealt in, including whoever
   * has already gone home. Their chips left the case just the same, and the
   * table cannot balance until those are accounted for.
   */
  protected readonly countable = computed(
    () => this.table()?.players.filter((p) => p.status !== 'Standby') ?? [],
  );

  protected hasCounted(player: TablePlayer): boolean {
    const reconciliation = this.reconciliation();

    return (
      reconciliation !== null &&
      !reconciliation.awaitingCountFrom.some((p) => p.tablePlayerId === player.tablePlayerId)
    );
  }

  /** Your own stack, or anyone's if you run the table. Mirrors the API rule. */
  protected canCountFor(player: TablePlayer): boolean {
    const table = this.table();

    return table !== null && (table.canManage || table.myPlayerId === player.tablePlayerId);
  }

  /** Resolves a stored colour token, or null for anything unrecognised. */
  protected colourOf(token: string | null | undefined): ChipColour | null {
    return chipColour(token);
  }

  ngOnInit(): void {
    timer(0, TICK_MS)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.tick.set(Date.now()));

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
          this.refreshClosing(table);
          this.refreshBlinds();
          this.followStatus();
          this.showNextNotice(table);
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

  /**
   * The manager's own door onto the table, open under the same window a
   * self-join gets — and the only door at all on an InviteOnly table, which
   * has no code and lets nobody self-serve.
   */
  protected canAddPlayer(): boolean {
    const table = this.table();

    return (
      table !== null &&
      table.canManage &&
      (table.status === 'Open' || (table.status === 'Running' && table.allowLateEntry))
    );
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

  /**
   * Fetches the championship roster fresh rather than reusing anything cached,
   * so someone invited moments ago is already offered — then filters to
   * whoever is not already at this table, since nobody else is worth showing.
   */
  protected openAddPlayer(): void {
    const table = this.table();

    if (!table) {
      return;
    }

    this.championships.members(this.championshipId()).subscribe({
      next: (members) => {
        const seated = new Set(table.players.map((p) => p.userId));
        const candidates = members.filter((m) => !seated.has(m.userId));

        this.dialog
          .open(AddTablePlayerDialog, { data: { candidates } })
          .afterClosed()
          .subscribe((userId: string | undefined) => {
            if (userId) {
              this.confirmAddPlayer(userId, candidates);
            }
          });
      },
      error: (err: unknown) =>
        this.error.set(
          describeError(
            err,
            $localize`:@@table.addPlayerLoadFailed:Não foi possível carregar os membros do campeonato.`,
          ),
        ),
    });
  }

  private confirmAddPlayer(userId: string, candidates: readonly Member[]): void {
    const member = candidates.find((c) => c.userId === userId);

    this.confirm
      .ask({
        title: $localize`:@@confirm.addPlayerTitle:Adicionar à mesa?`,
        details: member
          ? [{ label: $localize`:@@confirm.roleWho:membro`, value: member.displayName }]
          : [],
        confirmLabel: $localize`:@@table.addPlayerConfirm:Adicionar`,
      })
      .subscribe(() => {
        this.run(this.tables.addPlayer(this.championshipId(), this.tableId(), userId), {
          fallback: $localize`:@@table.addPlayerFailed:Não foi possível adicionar o jogador.`,
        });
      });
  }

  protected toggleManageOthers(): void {
    this.manageOthers.update((current) => !current);
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

  /**
   * Puts the next unconfirmed stack in front of the player.
   *
   * Driven by the poll rather than by the act of dealing, so it reaches whoever
   * was not looking at their phone when the table started: it is waiting the
   * moment they open the screen, and appears on its own for anyone already
   * there. Confirming drains one and the next tick brings the next.
   */
  private showNextNotice(table: TableDetail): void {
    const next = table.pendingStacks[0];

    if (!next || this.showingNoticeFor !== null) {
      return;
    }

    this.showingNoticeFor = next.ledgerEntryId;

    this.dialog
      .open(StackNoticeDialog, {
        data: next,
        // Not dismissable by tapping away: the whole point is that somebody
        // actually looked at the chips.
        disableClose: true,
      })
      .afterClosed()
      .subscribe((confirmed: boolean | undefined) => {
        this.showingNoticeFor = null;

        if (!confirmed) {
          return;
        }

        this.tables
          .acknowledgeStack(this.championshipId(), this.tableId(), next.ledgerEntryId)
          .subscribe({
            next: () => this.refreshNow.next(),
            // Left unacknowledged on failure, so it comes back round rather than
            // vanishing silently — an unconfirmed stack is the thing worth
            // chasing.
            error: (err: unknown) =>
              this.error.set(
                describeError(
                  err,
                  $localize`:@@notice.ackFailed:Não foi possível confirmar o recebimento das fichas.`,
                ),
              ),
          });
      });
  }

  /**
   * Pulls whichever closing view the table has reached. Kept on the same tick as
   * the table itself so the panel and the status can never disagree on screen.
   */
  private refreshClosing(table: TableDetail): void {
    if (table.status === 'Counting') {
      this.tables
        .reconciliation(this.championshipId(), this.tableId())
        .subscribe({ next: (value) => this.reconciliation.set(value) });

      return;
    }

    if (table.status === 'Settled' || table.status === 'Closed') {
      this.tables
        .settlement(this.championshipId(), this.tableId())
        .subscribe({ next: (value) => this.settlement.set(value) });
    }
  }

  private refreshBlinds(): void {
    this.tables.blinds(this.championshipId(), this.tableId()).subscribe({
      next: (blinds) => {
        this.blinds.set(blinds);
        this.sampledAt = Date.now();
      },
    });
  }

  protected controlClock(action: ClockAction): void {
    // Running the clock is banal and happens mid-hand: no confirmation. Getting
    // it wrong costs one more tap, and asking every time would be unbearable.
    this.run(this.tables.controlClock(this.championshipId(), this.tableId(), action), {
      fallback: $localize`:@@blinds.clockFailed:Não foi possível controlar o cronômetro.`,
    });
  }

  protected editLevels(): void {
    const table = this.table();

    if (!table) {
      return;
    }

    this.dialog
      .open(BlindLevelsDialog, {
        data: {
          levels: this.blinds()?.levels ?? [],
          smallestChip: this.smallestChip(table),
        },
      })
      .afterClosed()
      .subscribe((levels: BlindLevelInput[] | undefined) => {
        if (levels) {
          this.confirmLevels(levels);
        }
      });
  }

  private confirmLevels(levels: readonly BlindLevelInput[]): void {
    this.confirm
      .ask({
        title: levels.length
          ? $localize`:@@confirm.blindsTitle:Salvar a estrutura de blinds?`
          : $localize`:@@confirm.blindsOffTitle:Desligar o cronômetro?`,
        message: levels.length
          ? $localize`:@@confirm.blindsMessage:Substitui a estrutura atual e reinicia o cronômetro no primeiro nível.`
          : $localize`:@@confirm.blindsOffMessage:A mesa fica sem níveis e sem cronômetro.`,
        destructive: levels.length === 0,
        confirmLabel: $localize`:@@common.save:Salvar`,
      })
      .subscribe(() => {
        this.run(this.tables.setBlindLevels(this.championshipId(), this.tableId(), levels), {
          fallback: $localize`:@@blinds.saveFailed:Não foi possível salvar a estrutura de blinds.`,
        });
      });
  }

  /**
   * The least anyone can post. Taken from the case rather than guessed: a blind
   * below the smallest chip cannot be paid.
   */
  private smallestChip(table: TableDetail): number {
    return table.stock.reduce(
      (smallest, chip) => Math.min(smallest, chip.effectiveValue),
      table.stock[0]?.effectiveValue ?? 1,
    );
  }

  /**
   * Carries everyone forward when the table moves on. Whoever is looking at the
   * players list when counting starts wants the counting panel, and nobody
   * should have to be told to tap a tab that only just appeared.
   */
  private followStatus(): void {
    const available = this.sections().map((s) => s.id);

    if (!available.includes(this.section())) {
      this.section.set(available[available.length - 1]);
    }
  }

  protected startCounting(): void {
    this.confirm
      .ask({
        title: $localize`:@@confirm.countingTitle:Encerrar o jogo e contar?`,
        message: $localize`:@@confirm.countingMessage:Ninguém mais compra fichas. Cada jogador conta o que tem na frente e informa aqui.`,
        confirmLabel: $localize`:@@table.startCounting:Encerrar e contar`,
      })
      .subscribe(() => {
        this.run(this.tables.startCounting(this.championshipId(), this.tableId()), {
          fallback: $localize`:@@table.countingFailed:Não foi possível encerrar o jogo.`,
        });
      });
  }

  /** Own stack by default; a manager can count for whoever has already gone home. */
  protected reportCount(player: TablePlayer): void {
    const table = this.table();

    if (!table) {
      return;
    }

    this.dialog
      .open(CountDialog, {
        data: {
          playerName: player.displayName,
          chips: table.stock,
          moneyPerUnit: table.moneyPerUnit,
        },
      })
      .afterClosed()
      .subscribe((counts: ChipCountEntry[] | undefined) => {
        if (counts) {
          this.confirmCount(player, counts, table);
        }
      });
  }

  private confirmCount(
    player: TablePlayer,
    counts: readonly ChipCountEntry[],
    table: TableDetail,
  ): void {
    const held = counts.filter((c) => c.quantity > 0);

    const details: ConfirmDetail[] = held.map((count) => {
      const chip = table.stock.find((s) => s.denominationId === count.denominationId);
      const colour = this.colourOf(chip?.colour);

      return {
        label: $localize`:@@confirm.chipOf:ficha ${chip?.faceValue ?? ''}:VALUE:`,
        value: `${count.quantity}x`,
        swatch: colour?.swatch,
        ink: colour?.ink,
      };
    });

    this.confirm
      .ask({
        title: $localize`:@@confirm.countTitle:Confirmar a contagem?`,
        message: held.length
          ? $localize`:@@confirm.countMessage:É com isto que o acerto vai ser calculado. Dá para corrigir enquanto a mesa não fecha.`
          : $localize`:@@confirm.countEmptyMessage:Você está informando que não sobrou nenhuma ficha.`,
        details,
        confirmLabel: $localize`:@@count.confirm:Informar contagem`,
      })
      .subscribe(() => {
        this.run(
          this.tables.reportCount(
            this.championshipId(),
            this.tableId(),
            player.tablePlayerId,
            counts,
          ),
          { fallback: $localize`:@@table.countFailed:Não foi possível informar a contagem.` },
        );
      });
  }

  protected settle(): void {
    const reconciliation = this.reconciliation();

    this.confirm
      .ask({
        title: $localize`:@@confirm.settleTitle:Fechar a conta da mesa?`,
        message: $localize`:@@confirm.settleMessage:Calcula quem paga quem, com o menor número de Pix, e a posição de cada um. É feito uma vez só.`,
        // The API refuses this anyway; saying which half is missing saves the
        // manager tapping a button that was never going to work.
        blockedReason: reconciliation?.canSettle
          ? undefined
          : !reconciliation?.everyoneHasCounted
            ? $localize`:@@confirm.settleWaiting:Ainda falta gente informar a contagem.`
            : $localize`:@@confirm.settleUnbalanced:A contagem não bate com o que saiu da maleta.`,
        confirmLabel: $localize`:@@table.settle:Fechar a conta`,
      })
      .subscribe(() => {
        this.run(this.tables.settle(this.championshipId(), this.tableId()), {
          fallback: $localize`:@@table.settleFailed:Não foi possível fechar a conta.`,
        });
      });
  }

  /** Inert, so no confirmation: the key is already on screen. */
  protected copyHandle(handle: string): void {
    // Best effort: clipboard access needs a secure context and can be refused.
    void navigator.clipboard
      ?.writeText(handle)
      .then(() => this.copiedHandle.set(handle))
      .catch(() => undefined);
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
