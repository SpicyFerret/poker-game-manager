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
import { Confirm } from '../../../../shared/confirm/confirm.service';
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
  private readonly confirm = inject(Confirm);

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
        if (result) {
          this.confirmCreate(result);
        }
      });
  }

  private confirmCreate(result: CreateInviteResult): void {
    this.confirm
      .ask({
        title: $localize`:@@confirm.inviteTitle:Gerar este convite?`,
        message: $localize`:@@confirm.inviteMessage:Quem tiver o código entra no campeonato com esse cargo.`,
        details: [
          { label: $localize`:@@field.role:Cargo`, value: result.role },
          {
            label: $localize`:@@field.maxUses:Limite de usos`,
            value:
              result.maxUses === null
                ? $localize`:@@confirm.unlimited:ilimitado`
                : String(result.maxUses),
          },
        ],
        confirmLabel: $localize`:@@invites.create:Gerar convite`,
      })
      .subscribe(() => this.create(result));
  }

  private create(result: CreateInviteResult): void {
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
  }

  protected revoke(invite: Invite): void {
    this.confirm
      .ask({
        title: $localize`:@@confirm.revokeTitle:Revogar este convite?`,
        message: $localize`:@@confirm.revokeMessage:Quem já entrou continua no campeonato. O código para de funcionar e não volta.`,
        details: [{ label: $localize`:@@confirm.code:código`, value: invite.code }],
        destructive: true,
        confirmLabel: $localize`:@@invites.revoke:Revogar`,
      })
      .subscribe(() => this.revokeConfirmed(invite));
  }

  private revokeConfirmed(invite: Invite): void {
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

  /** Inert, so no confirmation: the code is already on screen. */
  protected copy(code: string): void {
    // Best effort: clipboard access needs a secure context and can be refused.
    void navigator.clipboard
      ?.writeText(code)
      .then(() => this.copied.set(code))
      .catch(() => undefined);
  }
}
