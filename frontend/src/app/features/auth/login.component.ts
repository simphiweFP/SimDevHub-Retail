import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CustomerAuthService } from '../../core/services/customer-auth.service';

@Component({
  selector: 'sdh-login',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="login">
      <a routerLink="/">← Back to store</a>
      <section>
        <span class="mark">◆</span>
        <h1>Welcome to Sim DevHub Retail</h1>
        <p>Sign in to securely manage your orders and reservations.</p>
        @if (loading()) {
          <p role="status">Checking secure sign-in configuration…</p>
        } @else if (!auth.isConfigured()) {
          <p role="status">Secure customer sign-in is not enabled for this store yet.</p>
          <button type="button" disabled>Customer sign-in unavailable</button>
        } @else if (auth.isAuthenticated()) {
          <p>You are signed in.</p>
          <a class="account" routerLink="/account">Go to my account</a>
        } @else {
          <button type="button" (click)="signIn()" [disabled]="busy()">
            Continue with secure sign-in
          </button>
        }
        @if (error()) { <p role="alert" class="error">{{ error() }}</p> }
        <small>Authentication is handled by your trusted identity provider.</small>
      </section>
    </div>
  `,
  styles: [`
    :host{font-family:Inter,system-ui,sans-serif}
    .login{min-height:100vh;background:#f4f7f1;padding:28px}
    .login>a{color:#176a2d;text-decoration:none}
    .login section{max-width:430px;margin:10vh auto;background:white;padding:40px;border-radius:25px;text-align:center;box-shadow:0 15px 40px #21472314}
    .mark{font-size:40px;color:#4daa31}h1{font-size:27px}
    p{font-size:14px;color:#6b756c}
    button,.account{display:block;width:100%;margin:12px 0;padding:15px;border-radius:12px;border:1px solid #e5ebe0;background:#f8faf6;color:#315e32;text-decoration:none;cursor:pointer;box-sizing:border-box}
    button:disabled{cursor:not-allowed;color:#697267}
    .error{color:#a62525}small{color:#8a958a}
  `]
})
export class LoginComponent implements OnInit {
  readonly auth = inject(CustomerAuthService);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');

  async ngOnInit(): Promise<void> {
    try {
      await this.auth.initialize();
    } catch {
      this.error.set('Unable to load secure sign-in configuration.');
    } finally {
      this.loading.set(false);
    }
  }

  async signIn(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      await this.auth.signIn();
    } catch {
      this.error.set('Sign-in could not be started. Please try again.');
      this.busy.set(false);
    }
  }
}
