import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';

import { describeError } from '../../../core/api/problem-details';
import { ChampionshipSummary } from '../../../core/championships/championship.models';
import { ChampionshipsService } from '../../../core/championships/championships.service';
import { RoleLabelPipe } from '../../../core/championships/role-label.pipe';

@Component({
  selector: 'app-championship-list',
  imports: [RouterLink, MatCardModule, MatButtonModule, MatProgressBarModule, RoleLabelPipe],
  templateUrl: './list.html',
  styleUrl: './list.scss',
})
export class ChampionshipList implements OnInit {
  private readonly championships = inject(ChampionshipsService);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly items = signal<ChampionshipSummary[]>([]);

  ngOnInit(): void {
    this.championships.list().subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
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
}
