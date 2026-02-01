import { Routes } from '@angular/router';
import { authenticationGuard } from './guards/authentication.guard';
import { authorizationGuard } from './guards/authorization.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    canActivate: [authenticationGuard()],
    loadComponent: () => import('./pages/monitoring/monitoring.component').then((m) => m.MonitoringComponent),
  },
  {
    path: 'login',
    loadComponent: () => import('./pages/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'sso',
    loadComponent: () => import('./pages/sso/sso.component').then((m) => m.SsoComponent),
  },
  {
    path: 'setup',
    loadComponent: () => import('./pages/setup/setup.component').then((m) => m.SetupComponent),
  },
  {
    path: 'user-settings',
    loadComponent: () => import('./pages/user-settings/user-settings.component').then((m) => m.UserSettingsComponent),
  },
  {
    path: 'settings',
    canActivate: [authenticationGuard(), authorizationGuard()],
    loadComponent: () => import('./pages/configuration/configuration.component').then((m) => m.ConfigurationComponent),
    children: [
      {
        path: '',
        pathMatch: 'full',
        canActivate: [authenticationGuard(), authorizationGuard()],
        loadComponent: () => import('./pages/settings/settings.component').then((m) => m.SettingsComponent),
      },
      {
        path: 'machines',
        canActivate: [authenticationGuard(), authorizationGuard()],
        loadComponent: () => import('./pages/list-machines/list-machines.component').then((m) => m.ListMachinesComponent),
      },
      {
        path: 'machines/new',
        canActivate: [authenticationGuard(), authorizationGuard()],
        loadComponent: () => import('./pages/add-machine/add-machine.component').then((m) => m.AddMachineComponent),
      },
      {
        path: 'machines/:id/edit',
        canActivate: [authenticationGuard(), authorizationGuard()],
        loadComponent: () => import('./pages/edit-machine/edit-machine.component').then((m) => m.EditMachineComponent),
      },
      {
        path: 'users',
        canActivate: [authenticationGuard(), authorizationGuard()],
        loadComponent: () => import('./pages/list-users/list-users.component').then((m) => m.ListUsersComponent),
      },
      {
        path: 'users/new',
        canActivate: [authenticationGuard(), authorizationGuard()],
        loadComponent: () => import('./pages/add-user/add-user.component').then((m) => m.AddUserComponent),
      },
      {
        path: 'users/:id/edit',
        canActivate: [authenticationGuard(), authorizationGuard()],
        loadComponent: () => import('./pages/edit-user/edit-user.component').then((m) => m.EditUserComponent),
      },
      {
        path: 'plugins',
        canActivate: [authenticationGuard(), authorizationGuard()],
        loadComponent: () => import('./pages/plugins/plugins.component').then((m) => m.PluginsComponent),
      },
      {
        path: 'about',
        canActivate: [authenticationGuard(), authorizationGuard()],
        loadComponent: () => import('./pages/about/about.component').then((m) => m.AboutComponent),
      },
    ],
  },
];
