import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { Subscription, debounceTime, merge } from 'rxjs';

import { describeError } from '../../../core/api/problem-details';
import { ChampionshipSummary } from '../../../core/championships/championship.models';
import { ChampionshipsService } from '../../../core/championships/championships.service';
import { RoleLabelPipe } from '../../../core/championships/role-label.pipe';
import { RealtimeService } from '../../../core/realtime/realtime.service';

@Component({
  selector: 'app-championship-list',
  imports: [
    RouterLink,
    DragDropModule,
    MatCardModule,
    MatButtonModule,
    MatProgressBarModule,
    RoleLabelPipe,
  ],
  templateUrl: './list.html',
  styleUrl: './list.scss',
})
export class ChampionshipList implements OnInit {
  private readonly championships = inject(ChampionshipsService);
  private readonly realtime = inject(RealtimeService);
  private readonly destroyRef = inject(DestroyRef);

  private watching: Subscription | null = null;

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly items = signal<ChampionshipSummary[]>([]);

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.championships.list().subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
        this.watchAll(items);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(
          describeError(
            err,
            $localize`:@@championships.loadFailed:Não foi possível carregar seus campeonatos.`,
          ),
        );
      },
    });
  }

  /**
   * A championship joined, left, or changed by someone else shows up here
   * without a manual refresh — including a card's leader after a table
   * settles elsewhere. Re-established after every reload, since joining or
   * leaving changes which championships are worth watching.
   */
  private watchAll(items: readonly ChampionshipSummary[]): void {
    this.watching?.unsubscribe();

    if (items.length === 0) {
      return;
    }

    this.watching = merge(...items.map((c) => this.realtime.watch(c.id)))
      .pipe(debounceTime(300), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.load());
  }

  /**
   * Reorders on the spot for instant feedback, then persists. A failed save
   * rolls the local order back rather than leaving the screen showing an
   * arrangement the server never actually kept.
   */
  protected drop(event: CdkDragDrop<ChampionshipSummary[]>): void {
    if (event.previousIndex === event.currentIndex) {
      return;
    }

    const before = this.items();
    const reordered = [...before];
    moveItemInArray(reordered, event.previousIndex, event.currentIndex);
    this.items.set(reordered);

    this.championships.reorder(reordered.map((c) => c.id)).subscribe({
      error: (err: unknown) => {
        this.items.set(before);
        this.error.set(
          describeError(
            err,
            $localize`:@@championships.reorderFailed:Não foi possível salvar a nova ordem.`,
          ),
        );
      },
    });
  }
}
