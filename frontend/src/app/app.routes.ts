import { Routes } from '@angular/router';
import { adminGuard, authGuard, guestGuard, patientGuard } from './auth.guard';
import { AdminPage } from './admin.page';
import { HomePage } from './home.page';
import { LoginPage } from './login.page';
import { PatientPage } from './patient.page';
import { RegisterPage } from './register.page';

export const routes: Routes = [
  { path: 'login', component: LoginPage, canActivate: [guestGuard] },
  { path: 'register', component: RegisterPage, canActivate: [guestGuard] },
  { path: 'admin', component: AdminPage, canActivate: [adminGuard] },
  { path: '', component: HomePage, canActivate: [authGuard] },
  { path: 'requests', component: PatientPage, canActivate: [patientGuard] },
  { path: 'records', redirectTo: '', pathMatch: 'full' },
  { path: '**', redirectTo: '' },
];
