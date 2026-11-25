import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';
import { User, UserManager, WebStorageStateStore } from 'oidc-client-ts';

interface PublicOidcConfiguration {
  enabled: boolean;
  authority: string;
  clientId: string;
  scope: string;
  enableSandboxCheckout?: boolean;
}
export interface CustomerOrder {
  orderId: string;
  status: string;
  amount: number;
  currency: string;
  createdAt: string;
  reservedUntil: string;
}
export interface OnboardedCustomer { id: string; created: boolean; }

/**
 * Opt-in OIDC authorization-code + PKCE browser login. No passwords or
 * secret client credentials are ever handled by the Angular application.
 * Auth is not available until /auth-config.json is explicitly deployed.
 */
@Injectable({ providedIn: 'root' })
export class CustomerAuthService {
  private readonly http = inject(HttpClient);
  private readonly configured = signal(false);
  private readonly authenticated = signal(false);
  private readonly sandboxCheckout = signal(false);
  readonly isConfigured = this.configured.asReadonly();
  readonly isAuthenticated = this.authenticated.asReadonly();
  readonly isSandboxCheckoutEnabled = this.sandboxCheckout.asReadonly();

  private manager: UserManager | null = null;
  private initialized: Promise<void> | null = null;

  initialize(): Promise<void> {
    this.initialized ??= this.loadConfiguration();
    return this.initialized;
  }

  private async loadConfiguration(): Promise<void> {
    let config: PublicOidcConfiguration;
    try {
      config = await firstValueFrom(this.http.get<PublicOidcConfiguration>(
        '/auth-config.json').pipe(timeout(5000)));
    } catch {
      return; // No config deployed: disabled by default.
    }
    if (!config?.enabled || !config.authority || !config.clientId || !config.scope ||
        config.clientId.length > 200 || config.scope.length > 500 ||
        !/^[a-zA-Z0-9_ .:/-]+$/.test(config.scope) ||
        !/^[a-zA-Z0-9._:-]+$/.test(config.clientId)) return;

    let authority: URL;
    try {
      authority = new URL(config.authority);
    } catch {
      return;
    }
    if (authority.protocol !== 'https:' || authority.username || authority.password ||
        authority.hash || authority.search) return;

    const callback = new URL('/auth/callback', window.location.origin);
    this.manager = new UserManager({
      authority: authority.href.replace(/\/$/, ''),
      client_id: config.clientId,
      redirect_uri: callback.href,
      post_logout_redirect_uri: new URL('/login', window.location.origin).href,
      response_type: 'code',
      scope: config.scope,
      userStore: new WebStorageStateStore({ store: window.sessionStorage }),
      automaticSilentRenew: false,
      loadUserInfo: false
    });
    this.configured.set(true);
    this.sandboxCheckout.set(config.enableSandboxCheckout === true);
    const user = await this.manager.getUser();
    this.authenticated.set(!!user && !user.expired);
  }

  async signIn(): Promise<void> {
    await this.initialize();
    if (!this.manager) throw new Error('Sign-in is not configured');
    await this.manager.signinRedirect();
  }

  async finishSignIn(): Promise<void> {
    await this.initialize();
    if (!this.manager) throw new Error('Sign-in is not configured');
    const user = await this.manager.signinRedirectCallback();
    if (!user || user.expired || !user.access_token)
      throw new Error('No valid session was returned');
    this.authenticated.set(true);
  }

  async signOut(): Promise<void> {
    await this.initialize();
    if (this.manager) await this.manager.removeUser();
    this.authenticated.set(false);
  }

  private async token(): Promise<string> {
    await this.initialize();
    if (!this.manager) throw new Error('Sign-in is not configured');
    const user: User | null = await this.manager.getUser();
    if (!user || user.expired || !user.access_token) {
      this.authenticated.set(false);
      throw new Error('Sign-in required');
    }
    return user.access_token;
  }

  async onboard(): Promise<OnboardedCustomer> {
    const token = await this.token();
    return await firstValueFrom(this.http.post<OnboardedCustomer>(
      '/api/customers/me/onboard', null,
      { headers: { Authorization: 'Bearer ' + token } }).pipe(timeout(10000)));
  }

  async getOrders(storeSlug: string): Promise<CustomerOrder[]> {
    if (!/^[a-z0-9-]{2,80}$/.test(storeSlug)) throw new Error('Invalid store');
    const token = await this.token();
    return await firstValueFrom(this.http.get<CustomerOrder[]>(
      '/api/storefront/' + encodeURIComponent(storeSlug) + '/my-orders',
      { headers: { Authorization: 'Bearer ' + token } }).pipe(timeout(10000)));
  }


  async beginSandboxPayment(storeSlug: string, orderId: string): Promise<void> {
    if (!this.sandboxCheckout() ||
        !/^[a-z0-9-]{2,80}$/.test(storeSlug) ||
        !/^[0-9a-f-]{36}$/i.test(orderId))
      throw new Error('Sandbox checkout unavailable');

    const accessToken = await this.token();
    const keyName = 'retail-sandbox-payment-' + orderId;
    let key = window.sessionStorage.getItem(keyName);
    if (!key) {
      key = 'sandbox-' + window.crypto.randomUUID();
      window.sessionStorage.setItem(keyName, key);
    }

    const checkout = await firstValueFrom(this.http.post<{
      intentId: string;
      action: string;
      fields: Record<string, string>;
    }>(
      '/api/storefront/' + encodeURIComponent(storeSlug) + '/orders/' +
        encodeURIComponent(orderId) + '/payfast-sandbox',
      { idempotencyKey: key },
      { headers: { Authorization: 'Bearer ' + accessToken } }
    ).pipe(timeout(15000)));

    // Only post to the fixed, known Payfast sandbox destination, never a
    // callback URL or arbitrary target supplied through compromised API data.
    if (checkout.action !== 'https://sandbox.payfast.co.za/eng/process' ||
        !checkout.fields || Object.keys(checkout.fields).length < 4)
      throw new Error('Invalid sandbox checkout destination');

    const form = document.createElement('form');
    form.method = 'POST';
    form.action = checkout.action;
    form.style.display = 'none';
    for (const [name, value] of Object.entries(checkout.fields)) {
      if (!/^[a-z][a-z0-9_]{0,79}$/.test(name) || typeof value !== 'string' ||
          value.length > 4000) throw new Error('Invalid payment field');
      const field = document.createElement('input');
      field.type = 'hidden';
      field.name = name;
      field.value = value;
      form.appendChild(field);
    }
    document.body.appendChild(form);
    form.submit();
  }

  // Never log or surface access tokens in error messages.
  isUnauthorized(error: unknown): boolean {
    return error instanceof HttpErrorResponse && (error.status === 401 || error.status === 403);
  }
}
