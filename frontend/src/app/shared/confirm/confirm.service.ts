import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Observable, filter, map } from 'rxjs';

import { ConfirmData, ConfirmDialog } from './confirm-dialog';

/**
 * Asks before anything that creates, edits or removes.
 *
 * Only genuinely inert actions skip it — copying a code, opening a card, moving
 * between sections. Everything that changes state gets a sentence saying what is
 * about to happen, because most of this app is operated one-handed at a table
 * while a game is going on.
 */
@Injectable({ providedIn: 'root' })
export class Confirm {
  private readonly dialog = inject(MatDialog);

  /** Emits only when confirmed. Dismissing simply produces nothing. */
  ask(data: ConfirmData): Observable<void> {
    return this.dialog
      .open(ConfirmDialog, { data, autoFocus: data.requireTyped ? 'dialog' : 'first-tabbable' })
      .afterClosed()
      .pipe(
        filter((confirmed: boolean | undefined) => confirmed === true),
        map(() => undefined),
      );
  }
}
