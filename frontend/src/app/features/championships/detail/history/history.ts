import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, input, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { RouterLink } from '@angular/router';

import { describeError } from '../../../../core/api/problem-details';
import { HistoryRow } from '../../../../core/championships/championship.models';
import { ChampionshipsService } from '../../../../core/championships/championships.service';

/**
 * Every night that has been settled, newest first, each one a link back to its
 * table — the settlement and the results stay readable long after the night, and
 * that is usually what someone is looking for when they come here.
 */
@Component({
  selector: 'app-history-tab',
  imports: [DatePipe, DecimalPipe, RouterLink, MatCardModule],
  templateUrl: './history.html',
  styleUrl: './history.scss',
})
export class HistoryTab implements OnInit {
  private readonly championships = inject(ChampionshipsService);

  readonly championshipId = input.required<string>();

  protected readonly rows = signal<HistoryRow[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    this.championships.history(this.championshipId()).subscribe({
      next: (rows) => {
        this.rows.set(rows);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(
          describeError(
            err,
            $localize`:@@history.loadFailed:Não foi possível carregar o histórico.`,
          ),
        );
      },
    });
  }
}
