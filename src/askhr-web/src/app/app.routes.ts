import { Routes } from '@angular/router';
import { Roles } from './auth/auth.config';
import { authGuard, roleGuard } from './auth/auth.guards';
import { HomePage } from './pages/home.page';

export const routes: Routes = [
  { path: '', component: HomePage, canActivate: [authGuard], title: 'AskHR' },
  {
    path: 'documents',
    canActivate: [roleGuard(Roles.HrAdmin)],
    loadComponent: () => import('./pages/documents.page').then((m) => m.DocumentsPage),
    title: 'Documents · AskHR',
  },
  {
    path: 'forbidden',
    loadComponent: () => import('./pages/forbidden.page').then((m) => m.ForbiddenPage),
    title: 'Access denied · AskHR',
  },
  { path: '**', redirectTo: '' },
];
