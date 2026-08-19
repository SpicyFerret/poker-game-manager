import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
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
import { TablesTab } from './tables/tables';

@Component({
  selector: 'app-championship-detail',
  imports: [
    MatCardModule,
    MatTabsModule,
    MatButtonModule,
    MatProgressBarModule,
    RoleLabelPipe,
    TablesTab,
    MembersTab,
    InvitesTab,
    ChipSetsTab,
    SeasonsTab,
    SettingsTab,
  ],
  templateUrl: './detail.html',
  styleUrl: './detail.scss',
})
export class ChampionshipDetail implements OnInit {
  private readonly championships = inject(ChampionshipsService);

  /** Bound from the route by withComponentInputBinding(). */
  readonly championshipId = input.required<string>();

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly championship = signal<Championship | null>(null);

  /**
   * Setup — members, invites, chip cases, seasons, settings — is folded away
   * behind the gear. It is configured once and then rarely touched, while the
   * tables are what someone opens this screen for on a game night.
   */
  protected readonly setupOpen = signal(false);

  protected readonly canManage = computed(() => {
    const championship = this.championship();
    return championship !== null && atLeast(championship.role, 'TableManager');
  });

  protected readonly canAdminister = computed(() => {
    const championship = this.championship();
    return championship !== null && atLeast(championship.role, 'Admin');
  });

  ngOnInit(): void {
    this.load();
  }

  /**
   * Settings edits change the header, and the members tab can change the caller's
   * own role, so children ask for a reload rather than each keeping a stale copy.
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

  protected toggleSetup(): void {
    this.setupOpen.update((open) => !open);
  }
}
