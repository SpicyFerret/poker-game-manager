import { Pipe, PipeTransform } from '@angular/core';

import { TablePlayerStatus, TableStatus } from './table.models';

/**
 * Status values arrive as stable identifiers ("Running"). They are never shown
 * raw, and keeping the strings in one place keeps them translatable.
 */
@Pipe({ name: 'tableStatusLabel' })
export class TableStatusLabelPipe implements PipeTransform {
  transform(status: TableStatus): string {
    switch (status) {
      case 'Draft':
        return $localize`:@@tableStatus.draft:Rascunho`;
      case 'Open':
        return $localize`:@@tableStatus.open:Aberta`;
      case 'Running':
        return $localize`:@@tableStatus.running:Em jogo`;
      case 'Counting':
        return $localize`:@@tableStatus.counting:Contando fichas`;
      case 'Reconciled':
        return $localize`:@@tableStatus.reconciled:Conferida`;
      case 'Settled':
        return $localize`:@@tableStatus.settled:Acertada`;
      case 'Closed':
        return $localize`:@@tableStatus.closed:Encerrada`;
      case 'Cancelled':
        return $localize`:@@tableStatus.cancelled:Cancelada`;
    }
  }
}

@Pipe({ name: 'playerStatusLabel' })
export class PlayerStatusLabelPipe implements PipeTransform {
  transform(status: TablePlayerStatus): string {
    switch (status) {
      case 'Standby':
        return $localize`:@@playerStatus.standby:Aguardando`;
      case 'Playing':
        return $localize`:@@playerStatus.playing:Jogando`;
      case 'Left':
        return $localize`:@@playerStatus.left:Saiu`;
    }
  }
}
