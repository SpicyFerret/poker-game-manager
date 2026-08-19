import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';

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
import { AddMemberDialog, AddMemberResult } from './add-member-dialog';

@Component({
  selector: 'app-members-tab',
  imports: [
    MatCardModule,
    MatButtonModule,
    MatMenuModule,
    MatDialogModule,
    MatFormFieldModule,
    MatSelectModule,
    RoleLabelPipe,
  ],
  templateUrl: './members.html',
  styleUrl: './members.scss',
})
export class MembersTab implements OnInit {
  private readonly championships = inject(ChampionshipsService);
  private readonly dialog = inject(MatDialog);

  readonly championshipId = input.required<string>();
  readonly callerRole = input.required<ChampionshipRole>();

  /** Fires when something changed that the parent's header may need to reflect. */
  readonly changed = output<void>();

  protected readonly members = signal<Member[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);

  ngOnInit(): void {
    this.load();
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
        if (!result) {
          return;
        }

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
      });
  }

  protected changeRole(member: Member, role: ChampionshipRole): void {
    if (role === member.role) {
      return;
    }

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
        // Puts the select back to what the server still believes.
        this.load();
      },
    });
  }

  protected remove(member: Member): void {
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
