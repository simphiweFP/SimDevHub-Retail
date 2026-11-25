import { Component, inject, OnInit, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CustomerAuthService } from '../../core/services/customer-auth.service';

@Component({
  selector: 'sdh-auth-callback',
  standalone: true,
  imports: [RouterLink],
  template: `
    <main style="max-width:460px;margin:15vh auto;padding:28px;font-family:system-ui">
      <h1>Completing secure sign-in</h1>
      @if (error()) {
        <p role="alert">{{ error() }}</p>
        <a routerLink="/login">Back to sign-in</a>
      } @else {
        <p role="status">Verifying your sign-in response…</p>
      }
    </main>
  `
})
export class CallbackComponent implements OnInit {
  private readonly auth = inject(CustomerAuthService);
  private readonly router = inject(Router);
  readonly error = signal('');

  async ngOnInit(): Promise<void> {
    try {
      await this.auth.finishSignIn();
      // The OIDC library validates callback state, nonce and PKCE.
      // Do not honour arbitrary return URLs from query parameters.
      await this.router.navigateByUrl('/account', { replaceUrl: true });
    } catch {
      this.error.set('Sign-in could not be verified. Please start again.');
    }
  }
}
