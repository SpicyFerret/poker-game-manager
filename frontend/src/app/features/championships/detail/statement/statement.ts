import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, input, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { RouterLink } from '@angular/router';

import { describeError } from '../../../../core/api/problem-details';
import { Statement } from '../../../../core/championships/championship.models';
import { ChampionshipsService } from '../../../../core/championships/championships.service';

/**
 * Your own nights, and what they cost. Answers the question a ranking cannot:
 * not where you stand, but where the money went.
 *
 * The API only ever returns the caller's rows, so nothing here can leak someone
 * else's night.
 */
@Component({
  selector: 'app-statement-tab',
  imports: [DatePipe, DecimalPipe, RouterLink, MatCardModule],
  templateUrl: './statement.html',
  styleUrl: './statement.scss',
})
export class StatementTab implements OnInit {
  private readonly championships = inject(ChampionshipsService);

  readonly championshipId = input.required<string>();

  protected readonly statement = signal<Statement | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    this.championships.statement(this.championshipId()).subscribe({
      next: (statement) => {
        this.statement.set(statement);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(
          describeError(
            err,
            $localize`:@@statement.loadFailed:Não foi possível carregar o seu extrato.`,
          ),
        );
      },
    });
  }
}
