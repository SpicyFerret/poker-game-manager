import { DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';

import { describeError } from '../../../../core/api/problem-details';
import {
  RankingRow,
  Rankings,
  Statistics,
} from '../../../../core/championships/championship.models';
import { ChampionshipsService } from '../../../../core/championships/championships.service';

/** Which of the two rankings is on screen. */
type Basis = 'points' | 'balance';

/**
 * The two rankings the group asked for, over the whole championship — which is
 * the season, so there is no window to pick.
 *
 * One at a time rather than side by side: they hold the same people in a
 * different order, and two columns of near-identical names on a phone is how you
 * read the wrong one.
 */
@Component({
  selector: 'app-rankings-tab',
  imports: [DecimalPipe, MatCardModule, MatButtonModule],
  templateUrl: './rankings.html',
  styleUrl: './rankings.scss',
})
export class RankingsTab implements OnInit {
  private readonly championships = inject(ChampionshipsService);

  readonly championshipId = input.required<string>();

  protected readonly rankings = signal<Rankings | null>(null);
  protected readonly statistics = signal<Statistics | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly loading = signal(true);

  protected readonly basis = signal<Basis>('points');

  protected readonly rows = computed<RankingRow[]>(() => {
    const rankings = this.rankings();

    if (!rankings) {
      return [];
    }

    return this.basis() === 'points' ? rankings.byPoints : rankings.byBalance;
  });

  ngOnInit(): void {
    this.championships.rankings(this.championshipId()).subscribe({
      next: (rankings) => {
        this.rankings.set(rankings);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(
          describeError(
            err,
            $localize`:@@rankings.loadFailed:Não foi possível carregar o ranking.`,
          ),
        );
      },
    });

    // Failing quietly: the numbers are a nice-to-have next to the ranking, and
    // losing them should not replace the ranking with an error.
    this.championships.statistics(this.championshipId()).subscribe({
      next: (statistics) => this.statistics.set(statistics),
    });
  }

  protected show(basis: Basis): void {
    this.basis.set(basis);
  }
}
