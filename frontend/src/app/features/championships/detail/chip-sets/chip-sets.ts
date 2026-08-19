import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Observable } from 'rxjs';

import { describeError } from '../../../../core/api/problem-details';
import {
  ChampionshipRole,
  ChipSet,
  atLeast,
} from '../../../../core/championships/championship.models';
import { ChampionshipsService } from '../../../../core/championships/championships.service';

@Component({
  selector: 'app-chip-sets-tab',
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  templateUrl: './chip-sets.html',
  styleUrl: './chip-sets.scss',
})
export class ChipSetsTab implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly championships = inject(ChampionshipsService);

  readonly championshipId = input.required<string>();
  readonly callerRole = input.required<ChampionshipRole>();

  protected readonly chipSets = signal<ChipSet[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);

  /** Which chip set is open in the editor, or 'new'. Null means the list. */
  protected readonly editing = signal<string | 'new' | null>(null);

  protected readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(80)]],
    denominations: this.formBuilder.array([this.denominationGroup()]),
  });

  ngOnInit(): void {
    this.load();
  }

  protected get denominations(): FormArray {
    return this.form.controls.denominations;
  }

  protected canAdminister(): boolean {
    return atLeast(this.callerRole(), 'Admin');
  }

  private denominationGroup(faceValue = 0, effectiveValue = 0, quantity = 0, colour = '') {
    return this.formBuilder.nonNullable.group({
      faceValue: [faceValue, [Validators.required, Validators.min(1)]],
      effectiveValue: [effectiveValue, [Validators.required, Validators.min(1)]],
      quantity: [quantity, [Validators.required, Validators.min(0)]],
      colour: [colour],
    });
  }

  protected load(): void {
    this.championships.chipSets(this.championshipId()).subscribe({
      next: (chipSets) => this.chipSets.set(chipSets),
      error: (err: unknown) =>
        this.error.set(
          describeError(
            err,
            $localize`:@@chipSets.loadFailed:Não foi possível carregar as maletas.`,
          ),
        ),
    });
  }

  protected startNew(): void {
    this.form.reset({ name: '' });
    this.denominations.clear();
    this.denominations.push(this.denominationGroup());
    this.editing.set('new');
  }

  protected startEdit(chipSet: ChipSet): void {
    this.form.patchValue({ name: chipSet.name });
    this.denominations.clear();

    for (const denomination of chipSet.denominations) {
      this.denominations.push(
        this.denominationGroup(
          denomination.faceValue,
          denomination.effectiveValue,
          denomination.quantity,
          denomination.colour ?? '',
        ),
      );
    }

    this.editing.set(chipSet.id);
  }

  protected cancel(): void {
    this.editing.set(null);
    this.error.set(null);
  }

  protected addDenomination(): void {
    this.denominations.push(this.denominationGroup());
  }

  protected removeDenomination(index: number): void {
    this.denominations.removeAt(index);
  }

  protected save(): void {
    const target = this.editing();

    if (this.form.invalid || this.busy() || target === null) {
      return;
    }

    const value = this.form.getRawValue();
    const denominations = value.denominations.map((d) => ({
      faceValue: Number(d.faceValue),
      effectiveValue: Number(d.effectiveValue),
      quantity: Number(d.quantity),
      colour: d.colour.trim() === '' ? null : d.colour.trim(),
    }));

    // Caught here as well as by the API: face value is how a player asks for a
    // chip at the table, so two chips in one case cannot share one.
    const faceValues = new Set(denominations.map((d) => d.faceValue));

    if (faceValues.size !== denominations.length) {
      this.error.set(
        $localize`:@@chipSets.duplicateFaceValue:Cada ficha da maleta precisa ter um valor impresso diferente.`,
      );
      return;
    }

    this.busy.set(true);
    this.error.set(null);

    // Widened to unknown: create answers with the new id and update with
    // nothing, and a union of the two observables has no callable subscribe.
    const request: Observable<unknown> =
      target === 'new'
        ? this.championships.createChipSet(this.championshipId(), value.name.trim(), denominations)
        : this.championships.updateChipSet(
            this.championshipId(),
            target,
            value.name.trim(),
            denominations,
          );

    request.subscribe({
      next: () => {
        this.busy.set(false);
        this.editing.set(null);
        this.load();
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.error.set(
          describeError(err, $localize`:@@chipSets.saveFailed:Não foi possível salvar a maleta.`),
        );
      },
    });
  }

  protected remove(chipSet: ChipSet): void {
    this.busy.set(true);
    this.error.set(null);

    this.championships.deleteChipSet(this.championshipId(), chipSet.id).subscribe({
      next: () => {
        this.busy.set(false);
        this.load();
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.error.set(
          describeError(
            err,
            $localize`:@@chipSets.deleteFailed:Não foi possível excluir a maleta.`,
          ),
        );
      },
    });
  }
}
