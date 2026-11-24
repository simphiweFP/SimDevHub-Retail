import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';
import { Product } from '../models/product';

interface StorefrontResponse {
  storeName: string;
  storeSlug: string;
  products: Product[];
}

export type CatalogueSource = 'loading' | 'live' | 'demo';

@Injectable({ providedIn: 'root' })
export class CatalogueService {
  private readonly http = inject(HttpClient);
  private readonly demoProducts: Product[] = [
    { id: 'demo-ip16', name: 'iPhone 16 Pro', storage: '256GB', price: 26999, category: 'Phones', tag: 'New' },
    { id: 'demo-s24', name: 'Samsung Galaxy S24 Ultra', storage: '256GB', price: 23999, category: 'Phones', tag: 'Hot Deal' },
    { id: 'demo-a55', name: 'Samsung Galaxy A55', storage: '128GB', price: 6499, category: 'Phones', tag: 'Popular' },
    { id: 'demo-ip15', name: 'iPhone 15', storage: '128GB', price: 18999, category: 'Phones' }
  ];
  private readonly productsState = signal<readonly Product[]>([]);
  readonly products = this.productsState.asReadonly();
  readonly source = signal<CatalogueSource>('loading');
  readonly storeName = signal('Nova Mobile');
  readonly isDemo = computed(() => this.source() === 'demo');

  // The demo tenant is explicit until storefront host-based tenant resolution is implemented.
  async load(storeSlug = 'nova-mobile'): Promise<void> {
    this.source.set('loading');
    try {
      const data = await firstValueFrom(
        this.http.get<StorefrontResponse>(`/api/storefront/${encodeURIComponent(storeSlug)}/products`).pipe(timeout(4500))
      );
      this.productsState.set(data.products);
      this.storeName.set(data.storeName);
      this.source.set('live');
    } catch {
      // Never mistake fallback data for real retailer inventory.
      this.productsState.set(this.demoProducts);
      this.storeName.set('Nova Mobile');
      this.source.set('demo');
    }
  }
}
