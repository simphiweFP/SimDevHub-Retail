import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CurrencyPipe } from '@angular/common';
import { CatalogueService } from '../../core/services/catalogue.service';
import { CartService } from '../../core/services/cart.service';
import { Product } from '../../core/models/product';

@Component({
  selector: 'sdh-storefront',
  standalone: true,
  imports: [RouterLink, CurrencyPipe],
  templateUrl: './storefront.component.html',
  styleUrl: './storefront.component.scss'
})
export class StorefrontComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  readonly catalogue = inject(CatalogueService);
  readonly storeSlug = signal('nova-mobile');
  readonly homeLink = computed(() => '/s/' + this.storeSlug());
  readonly checkoutLink = computed(() => this.homeLink() + '/checkout');
  readonly accountLink = computed(() => this.homeLink() + '/account');
  readonly cart = inject(CartService);
  readonly query = signal('');
  readonly category = signal('All');
  readonly categories = ['All', 'Phones', 'Tablets', 'Smartwatches', 'Accessories', 'Chargers', 'Deals'];
  readonly featured = computed(() => this.catalogue.products()[0] ?? null);
  readonly products = computed(() => this.catalogue.products().filter(product => {
    const category = this.category();
    const matchesCategory = category === 'All' || (category === 'Deals' ? !!product.tag : product.category === category);
    return matchesCategory && product.name.toLowerCase().includes(this.query().trim().toLowerCase());
  }));

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const slug = params.get('storeSlug') ?? 'nova-mobile';
      if (!/^[a-z0-9-]{2,80}$/.test(slug)) {
        this.catalogue.source.set('error');
        return;
      }
      this.storeSlug.set(slug);
      this.cart.selectStore(slug);
      void this.catalogue.load(slug);
    });
  }
  add(product: Product): void { this.cart.add(product); }
  enquire(product: Product): void {
    const message = encodeURIComponent('Hi, is ' + product.name + ' (' + product.storage + ') available for R' + product.price.toLocaleString('en-ZA') + '?');
    window.open('https://wa.me/?text=' + message, '_blank', 'noopener,noreferrer');
  }
}
