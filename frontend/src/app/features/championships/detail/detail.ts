import { Component, OnInit, inject, input, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';

import { describeError } from '../../../core/api/problem-details';
import { Championship, atLeast } from '../../../core/championships/championship.models';
import { ChampionshipsService } from '../../../core/championships/championships.service';
import { RoleLabelPipe } from '../../../core/championships/role-label.pipe';
import { ChipSetsTab } from './chip-sets/chip-sets';
import { InvitesTab } from './invites/invites';
import { MembersTab } from './members/members';
import { SeasonsTab } from './seasons/seasons';
import { SettingsTab } from './settings/settings';

@Component({
  selector: 'app-championship-detail',
  imports: [
    MatCardModule,
    MatTabsModule,
    MatProgressBarModule,
    RoleLabelPipe,
    MembersTab,
    InvitesTab,
    ChipSetsTab,
    SeasonsTab,
    SettingsTab,
  ],
  templateUrl: './detail.html',
})
export class ChampionshipDetail implements OnInit {
  private readonly championships = inject(ChampionshipsService);

  /** Bound from the route by withComponentInputBinding(). */
  readonly championshipId = input.required<string>();

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly championship = signal<Championship | null>(null);

  ngOnInit(): void {
    this.load();
  }

  /**
   * Settings edits change the header, and the members tab can change the
   * caller's own role, so tabs ask for a reload rather than each keeping its own
   * stale copy.
   */
  protected load(): void {
    this.championships.get(this.championshipId()).subscribe({
      next: (championship) => {
        this.championship.set(championship);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(
          describeError(
            err,
            $localize`:@@championship.loadFailed:Não foi possível carregar o campeonato.`,
          ),
        );
      },
    });
  }

  protected canManage(championship: Championship): boolean {
    return atLeast(championship.role, 'TableManager');
  }

  protected canAdminister(championship: Championship): boolean {
    return atLeast(championship.role, 'Admin');
  }
}
