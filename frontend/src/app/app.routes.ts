import { Routes } from '@angular/router';

import { anonymousOnlyGuard, authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/championships/list/list').then((m) => m.ChampionshipList),
  },
  {
    path: 'championships/new',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/championships/create/create').then((m) => m.ChampionshipCreate),
  },
  {
    path: 'championships/join',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/championships/join/join').then((m) => m.ChampionshipJoin),
  },
  {
    // Sits after the literal routes above so 'new' and 'join' are not swallowed
    // as ids.
    path: 'championships/:championshipId',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/championships/detail/detail').then((m) => m.ChampionshipDetail),
  },
  {
    path: 'login',
    canActivate: [anonymousOnlyGuard],
    loadComponent: () => import('./features/auth/login/login').then((m) => m.Login),
  },
  {
    path: 'register',
    canActivate: [anonymousOnlyGuard],
    loadComponent: () => import('./features/auth/register/register').then((m) => m.Register),
  },
  {
    path: 'profile',
    canActivate: [authGuard],
    loadComponent: () => import('./features/profile/profile').then((m) => m.Profile),
  },
  { path: '**', redirectTo: '' },
];
