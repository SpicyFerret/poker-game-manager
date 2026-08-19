import { Component, OnInit, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';

import { describeError } from '../../../../core/api/problem-details';
import {
  ChampionshipRole,
  Invite,
  assignableRoles,
} from '../../../../core/championships/championship.models';
import { ChampionshipsService } from '../../../../core/championships/championships.service';
import { RoleLabelPipe } from '../../../../core/championships/role-label.pipe';
import { CreateInviteDialog, CreateInviteResult } from './create-invite-dialog';

@Component({
  selector: 'app-invites-tab',
  imports: [MatCardModule, MatButtonModule, MatDialogModule, RoleLabelPipe],
  templateUrl: './invites.html',
  styleUrl: './invites.scss',
})
export class InvitesTab implements OnInit {
  private readonly championships = inject(ChampionshipsService);
  private readonly dialog = inject(MatDialog);

  readonly championshipId = input.required<string>();
  readonly callerRole = input.required<ChampionshipRole>();

  protected readonly invites = signal<Invite[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly copied = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
  }

  protected rolesICanInvite(): ChampionshipRole[] {
    return assignableRoles(this.callerRole());
  }

  protected load(): void {
    this.championships.invites(this.championshipId()).subscribe({
      next: (invites) => this.invites.set(invites),
      error: (err: unknown) =>
        this.error.set(
          describeError(
            err,
            $localize`:@@invites.loadFailed:Não foi possível carregar os convites.`,
          ),
        ),
    });
  }

  protected openCreate(): void {
    this.dialog
      .open(CreateInviteDialog, { data: { assignableRoles: this.rolesICanInvite() } })
      .afterClosed()
      .subscribe((result: CreateInviteResult | undefined) => {
        if (!result) {
          return;
        }

        this.busy.set(true);
        this.error.set(null);

        this.championships
          .createInvite(this.championshipId(), result.role, result.maxUses, null)
          .subscribe({
            next: () => {
              this.busy.set(false);
              this.load();
            },
            error: (err: unknown) => {
              this.busy.set(false);
              this.error.set(
                describeError(
                  err,
                  $localize`:@@invites.createFailed:Não foi possível criar o convite.`,
                ),
              );
            },
          });
      });
  }

  protected revoke(invite: Invite): void {
    this.busy.set(true);
    this.error.set(null);

    this.championships.revokeInvite(this.championshipId(), invite.id).subscribe({
      next: () => {
        this.busy.set(false);
        this.load();
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.error.set(
          describeError(
            err,
            $localize`:@@invites.revokeFailed:Não foi possível revogar o convite.`,
          ),
        );
      },
    });
  }

  protected copy(code: string): void {
    // Best effort: clipboard access needs a secure context and can be refused.
    // The code is on screen either way, so a failure is not worth an error.
    void navigator.clipboard
      ?.writeText(code)
      .then(() => this.copied.set(code))
      .catch(() => undefined);
  }
}
