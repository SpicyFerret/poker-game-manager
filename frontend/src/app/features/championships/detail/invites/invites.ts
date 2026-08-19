import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatListModule } from '@angular/material/list';
import { MatSelectModule } from '@angular/material/select';

import { describeError } from '../../../../core/api/problem-details';
import {
  ChampionshipRole,
  Invite,
  assignableRoles,
} from '../../../../core/championships/championship.models';
import { ChampionshipsService } from '../../../../core/championships/championships.service';
import { RoleLabelPipe } from '../../../../core/championships/role-label.pipe';

@Component({
  selector: 'app-invites-tab',
  imports: [
    ReactiveFormsModule,
    MatListModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    RoleLabelPipe,
  ],
  templateUrl: './invites.html',
  styleUrl: './invites.scss',
})
export class InvitesTab implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly championships = inject(ChampionshipsService);

  readonly championshipId = input.required<string>();
  readonly callerRole = input.required<ChampionshipRole>();

  protected readonly invites = signal<Invite[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly copied = signal<string | null>(null);

  protected readonly form = this.formBuilder.group({
    role: this.formBuilder.nonNullable.control<ChampionshipRole>('Player', [Validators.required]),
    // number | null, not string: the template binds this to <input type="number">,
    // so Angular's NumberValueAccessor puts a number in the control (or null when
    // the field is cleared) regardless of what it was initialised with. Typing it
    // as a string and calling .trim() threw inside the click handler, which meant
    // the button silently did nothing.
    // Null is the empty case, and it means unlimited — the usual choice for a code
    // pasted into the group's chat.
    maxUses: this.formBuilder.control<number | null>(null),
  });

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

  protected create(): void {
    if (this.form.invalid || this.busy()) {
      return;
    }

    const raw = this.form.getRawValue();
    const maxUses = raw.maxUses;

    if (maxUses !== null && (!Number.isInteger(maxUses) || maxUses < 1)) {
      this.error.set(
        $localize`:@@invites.maxUsesInvalid:O limite de usos precisa ser um número inteiro maior que zero.`,
      );
      return;
    }

    this.busy.set(true);
    this.error.set(null);

    this.championships.createInvite(this.championshipId(), raw.role, maxUses, null).subscribe({
      next: () => {
        this.busy.set(false);
        this.load();
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.error.set(
          describeError(err, $localize`:@@invites.createFailed:Não foi possível criar o convite.`),
        );
      },
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
