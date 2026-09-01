import { Component, DestroyRef, OnInit, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatMenuModule } from '@angular/material/menu';
import { debounceTime } from 'rxjs';

import { describeError } from '../../../../core/api/problem-details';
import {
  ChampionshipRole,
  Member,
  assignableRoles,
  atLeast,
  rankOf,
} from '../../../../core/championships/championship.models';
import { ChampionshipsService } from '../../../../core/championships/championships.service';
import { RoleLabelPipe } from '../../../../core/championships/role-label.pipe';
import { RealtimeService } from '../../../../core/realtime/realtime.service';
import { Confirm } from '../../../../shared/confirm/confirm.service';
import { AddMemberDialog, AddMemberResult } from './add-member-dialog';

@Component({
  selector: 'app-members-tab',
  imports: [MatCardModule, MatButtonModule, MatMenuModule, MatDialogModule, RoleLabelPipe],
  templateUrl: './members.html',
  styleUrl: './members.scss',
})
export class MembersTab implements OnInit {
  private readonly championships = inject(ChampionshipsService);
  private readonly dialog = inject(MatDialog);
  private readonly confirm = inject(Confirm);
  private readonly realtime = inject(RealtimeService);
  private readonly destroyRef = inject(DestroyRef);

  readonly championshipId = input.required<string>();
  readonly callerRole = input.required<ChampionshipRole>();

  /** Fires when something changed that the parent's header may need to reflect. */
  readonly changed = output<void>();

  protected readonly members = signal<Member[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);

  ngOnInit(): void {
    this.load();

    // Someone else adding, removing, or re-ranking a member shows up here
    // without waiting for a manual refresh.
    this.realtime
      .watch(this.championshipId())
      .pipe(debounceTime(300), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.load());
  }

  protected canAdminister(): boolean {
    return atLeast(this.callerRole(), 'Admin');
  }

  /** Mirrors the API rule: only members strictly below the caller can be touched. */
  protected canActOn(member: Member): boolean {
    return this.canAdminister() && rankOf(member.role) < rankOf(this.callerRole());
  }

  protected rolesICanAssign(): ChampionshipRole[] {
    return assignableRoles(this.callerRole());
  }

  protected load(): void {
    this.championships.members(this.championshipId()).subscribe({
      next: (members) => this.members.set(members),
      error: (err: unknown) =>
        this.error.set(
          describeError(
            err,
            $localize`:@@members.loadFailed:Não foi possível carregar os membros.`,
          ),
        ),
    });
  }

  protected openAdd(): void {
    this.dialog
      .open(AddMemberDialog, { data: { assignableRoles: this.rolesICanAssign() } })
      .afterClosed()
      .subscribe((result: AddMemberResult | undefined) => {
        if (result) {
          this.confirmAdd(result);
        }
      });
  }

  private confirmAdd(result: AddMemberResult): void {
    this.confirm
      .ask({
        title: $localize`:@@confirm.addMemberTitle:Adicionar ao campeonato?`,
        details: [
          { label: $localize`:@@field.email:E-mail`, value: result.email },
          { label: $localize`:@@field.role:Cargo`, value: result.role },
        ],
        confirmLabel: $localize`:@@members.add:Adicionar`,
      })
      .subscribe(() => this.add(result));
  }

  private add(result: AddMemberResult): void {
    this.busy.set(true);
    this.error.set(null);

    this.championships.addMember(this.championshipId(), result.email, result.role).subscribe({
      next: () => {
        this.busy.set(false);
        this.load();
        this.changed.emit();
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.error.set(
          describeError(
            err,
            $localize`:@@members.addFailed:Não foi possível adicionar. Confira se essa pessoa já tem conta.`,
          ),
        );
      },
    });
  }

  protected changeRole(member: Member, role: ChampionshipRole): void {
    if (role === member.role) {
      return;
    }

    this.confirm
      .ask({
        title: $localize`:@@confirm.roleTitle:Alterar o cargo?`,
        details: [
          { label: $localize`:@@confirm.roleWho:membro`, value: member.displayName },
          { label: $localize`:@@confirm.roleNew:novo cargo`, value: role },
        ],
        confirmLabel: $localize`:@@common.save:Salvar`,
      })
      .subscribe({
        next: () => this.applyRole(member, role),
        // Reloads whether or not it was confirmed: dismissing would otherwise
        // leave the select showing a role the server never accepted.
        complete: () => this.load(),
      });
  }

  private applyRole(member: Member, role: ChampionshipRole): void {
    this.busy.set(true);
    this.error.set(null);

    this.championships.changeRole(this.championshipId(), member.userId, role).subscribe({
      next: () => {
        this.busy.set(false);
        this.load();
        this.changed.emit();
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.error.set(
          describeError(err, $localize`:@@members.roleFailed:Não foi possível alterar o cargo.`),
        );
        this.load();
      },
    });
  }

  protected remove(member: Member): void {
    this.confirm
      .ask({
        title: $localize`:@@confirm.removeMemberTitle:Remover do campeonato?`,
        message: $localize`:@@confirm.removeMemberMessage:As mesas que essa pessoa já jogou continuam registradas.`,
        details: [{ label: $localize`:@@confirm.roleWho:membro`, value: member.displayName }],
        destructive: true,
        confirmLabel: $localize`:@@members.remove:Remover`,
      })
      .subscribe(() => this.removeConfirmed(member));
  }

  private removeConfirmed(member: Member): void {
    this.busy.set(true);
    this.error.set(null);

    this.championships.removeMember(this.championshipId(), member.userId).subscribe({
      next: () => {
        this.busy.set(false);
        this.load();
        this.changed.emit();
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.error.set(
          describeError(err, $localize`:@@members.removeFailed:Não foi possível remover o membro.`),
        );
      },
    });
  }
}
