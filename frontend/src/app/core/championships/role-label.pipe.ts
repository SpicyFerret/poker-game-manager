import { Pipe, PipeTransform } from '@angular/core';

import { ChampionshipRole } from './championship.models';

/**
 * Role names arrive from the API as stable identifiers ("TableManager"). They
 * are never shown raw — this turns them into something a person reads, and keeps
 * the four strings in one place for translation.
 */
@Pipe({ name: 'roleLabel' })
export class RoleLabelPipe implements PipeTransform {
  transform(role: ChampionshipRole): string {
    switch (role) {
      case 'Owner':
        return $localize`:@@role.owner:Dono`;
      case 'Admin':
        return $localize`:@@role.admin:Administrador`;
      case 'TableManager':
        return $localize`:@@role.tableManager:Gerente de mesa`;
      case 'Player':
        return $localize`:@@role.player:Jogador`;
    }
  }
}
