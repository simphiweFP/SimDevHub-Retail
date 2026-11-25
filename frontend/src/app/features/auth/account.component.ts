import { Component, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { CustomerAuthService, CustomerOrder } from '../../core/services/customer-auth.service';

@Component({
  selector: 'sdh-customer-account',
  standalone: true,
  imports: [RouterLink, CurrencyPipe, DatePipe],
  template: `
    <main class="account">
      <a routerLink="/">← Back to store</a>
      <h1>My account</h1>
      @if (loading()) { <p role="status">Loading account…</p> }
      @else if (!auth.isConfigured()) {
        <p>Customer accounts are not configured for this store.</p>
      } @else if (!auth.isAuthenticated()) {
        <p>Sign in to manage your purchases.</p><a routerLink="/login">Sign in</a>
      } @else {
        <p>Signed in securely</p>
        <button type="button" (click)="load()" [disabled]="loading()">Refresh orders</button>
        <button type="button" (click)="signOut()">Sign out on this device</button>
        @if (error()) { <p role="alert">{{ error() }}</p> }
        <h2>My orders</h2>
        @if (auth.isSandboxCheckoutEnabled()) {
          <p role="status">Testing mode: Payfast sandbox only. No live payments are available.</p>
        }
        @for (order of orders(); track order.orderId) {
          <article>
            <strong>{{order.status}}</strong>
            <p>{{order.amount | currency:order.currency:'symbol-narrow'}} · {{order.createdAt | date:'mediumDate'}}</p>
            <small>Order {{order.orderId}}</small>
            @if (auth.isSandboxCheckoutEnabled() && order.status === 'AwaitingPayment') {
              <button type="button" [disabled]="paying() === order.orderId"
                (click)="paySandbox(order)">Continue to Payfast sandbox</button>
            }
          </article>
        } @empty { <p>No orders to display.</p> }
      }
    </main>
  `,
  styles: [`
    :host{font-family:Inter,system-ui,sans-serif;color:#183a24}
    .account{max-width:760px;margin:auto;padding:32px 20px}
    a{color:#166a39}button{padding:12px 18px;border-radius:12px;border:1px solid #cbd8ca;background:#edf4ea;margin:8px 12px 16px 0;cursor:pointer}
    article{padding:18px;border:1px solid #dbe5db;border-radius:15px;margin-bottom:12px;background:#fafdfa}
    article p{margin-bottom:5px}small{color:#637364}
  `]
})
export class AccountComponent implements OnInit {
  readonly auth = inject(CustomerAuthService);
  private readonly router = inject(Router);
  readonly loading = signal(true);
  readonly orders = signal<CustomerOrder[]>([]);
  readonly error = signal('');
  readonly paying = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try { await this.auth.initialize(); }
    catch { this.error.set('Account configuration is unavailable.'); }
    if (this.auth.isAuthenticated()) await this.load();
    else this.loading.set(false);
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      // Onboarding may be deliberately disabled. A 404 is not interpreted
      // as successful registration and must not block read-only order history.
      try { await this.auth.onboard(); }
      catch (error) {
        if (this.auth.isUnauthorized(error))
          throw error;
      }
      this.orders.set(await this.auth.getOrders('nova-mobile'));
    } catch {
      this.orders.set([]);
      this.error.set('Could not load your orders. Please sign in again or try later.');
    } finally {
      this.loading.set(false);
    }
  }

  async paySandbox(order: CustomerOrder): Promise<void> {
    if (this.paying()) return;
    this.error.set('');
    this.paying.set(order.orderId);
    try {
      await this.auth.beginSandboxPayment('nova-mobile', order.orderId);
    } catch {
      this.error.set('Sandbox checkout is unavailable. No payment has been confirmed.');
    } finally {
      this.paying.set(null);
    }
  }

  async signOut(): Promise<void> {
    await this.auth.signOut();
    this.orders.set([]);
    await this.router.navigateByUrl('/login', { replaceUrl: true });
  }
}
