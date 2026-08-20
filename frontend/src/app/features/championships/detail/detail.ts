import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressBarModule } from '@angular/material/progress-bar';

import { describeError } from '../../../core/api/problem-details';
import { Championship, atLeast } from '../../../core/championships/championship.models';
import { ChampionshipsService } from '../../../core/championships/championships.service';
import { RoleLabelPipe } from '../../../core/championships/role-label.pipe';
import { NavSection, SectionNav } from '../../../shared/section-nav/section-nav';
import { ChipSetsTab } from './chip-sets/chip-sets';
import { HistoryTab } from './history/history';
import { InvitesTab } from './invites/invites';
import { MembersTab } from './members/members';
import { RankingsTab } from './rankings/rankings';
import { SettingsTab } from './settings/settings';
import { StatementTab } from './statement/statement';
import { TablesTab } from './tables/tables';

@Component({
  selector: 'app-championship-detail',
  imports: [
    MatCardModule,
    MatButtonModule,
    MatProgressBarModule,
    RoleLabelPipe,
    SectionNav,
    TablesTab,
    MembersTab,
    InvitesTab,
    ChipSetsTab,
    SettingsTab,
    RankingsTab,
    HistoryTab,
    StatementTab,
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
   * The one thing folded open at a time behind the gear and the ranking icon.
   * Setup — members, invites, chip cases, settings — is configured once and
   * rarely touched; the ranking is read, not edited. Neither is what a game
   * night opens this screen for, so opening either tucks the tables away rather
   * than sitting alongside them.
   */
  protected readonly panel = signal<'none' | 'setup' | 'rankings'>('none');

  protected readonly section = signal('members');

  /**
   * The main view, shown once neither panel is open. Tables first: on a game
   * night that is the only thing anyone opens this screen for, and the rest is
   * read afterwards.
   */
  protected readonly view = signal('tables');

  protected readonly views = computed<NavSection[]>(() => [
    { id: 'tables', label: $localize`:@@view.tables:Mesas` },
    { id: 'history', label: $localize`:@@view.history:Histórico` },
    { id: 'statement', label: $localize`:@@view.statement:Meu extrato` },
  ]);

  protected readonly canManage = computed(() => {
    const championship = this.championship();
    return championship !== null && atLeast(championship.role, 'TableManager');
  });

  protected readonly canAdminister = computed(() => {
    const championship = this.championship();
    return championship !== null && atLeast(championship.role, 'Admin');
  });

  /** Invites are hidden from anyone who cannot issue them, since a code is a credential. */
  protected readonly sections = computed<NavSection[]>(() => {
    const sections: NavSection[] = [{ id: 'members', label: $localize`:@@tab.members:Membros` }];

    if (this.canManage()) {
      sections.push({ id: 'invites', label: $localize`:@@tab.invites:Convites` });
    }

    sections.push(
      { id: 'chipSets', label: $localize`:@@tab.chipSets:Maletas` },
      { id: 'settings', label: $localize`:@@tab.settings:Configurações` },
    );

    return sections;
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

  /** Opening one panel closes the other — they answer different questions, never both at once. */
  protected toggleSetup(): void {
    this.panel.update((current) => (current === 'setup' ? 'none' : 'setup'));
  }

  protected toggleRankings(): void {
    this.panel.update((current) => (current === 'rankings' ? 'none' : 'rankings'));
  }
}
