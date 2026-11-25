import { computed, Injectable, signal } from '@angular/core';
import { Product } from '../models/product';

export interface CartLine { product: Product; quantity: number; }

@Injectable({ providedIn: 'root' })
export class CartService {
  private readonly linesState = signal<CartLine[]>([]);
  private activeStoreSlug: string | null = null;
  /**
   * A cart belongs to exactly one storefront. Navigating to another shop must
   * never carry products or prices across tenants.
   */
  selectStore(storeSlug: string): void {
    if (!/^[a-z0-9-]{2,80}$/.test(storeSlug)) throw new Error('Invalid storefront');
    if (this.activeStoreSlug !== storeSlug) {
      this.linesState.set([]);
      this.activeStoreSlug = storeSlug;
    }
  }
  readonly lines = this.linesState.asReadonly();
  readonly count = computed(() => this.lines().reduce((total, line) => total + line.quantity, 0));
  readonly subtotal = computed(() => this.lines().reduce((total, line) => total + line.product.price * line.quantity, 0));
  add(product: Product): void {
    this.linesState.update(lines => {
      const existing = lines.find(line => line.product.id === product.id);
      return existing
        ? lines.map(line => line.product.id === product.id ? { ...line, quantity: line.quantity + 1 } : line)
        : [...lines, { product, quantity: 1 }];
    });
  }
  setQuantity(productId: string, quantity: number): void {
    const safeQuantity = Math.max(0, Math.min(20, Math.floor(quantity)));
    this.linesState.update(lines => lines
      .map(line => line.product.id === productId ? { ...line, quantity: safeQuantity } : line)
      .filter(line => line.quantity > 0));
  }
  clear(): void { this.linesState.set([]); }
}
