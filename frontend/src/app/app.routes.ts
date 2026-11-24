import { Routes } from '@angular/router';
export const routes: Routes = [
  {path:'',loadComponent:()=>import('./features/storefront/storefront.component').then(m=>m.StorefrontComponent)},
  {path:'checkout',loadComponent:()=>import('./features/checkout/checkout.component').then(m=>m.CheckoutComponent)},
  {path:'admin',loadComponent:()=>import('./features/dashboard/dashboard.component').then(m=>m.DashboardComponent)},
  {path:'login',loadComponent:()=>import('./features/auth/login.component').then(m=>m.LoginComponent)},
  {path:'**',redirectTo:''}
];
