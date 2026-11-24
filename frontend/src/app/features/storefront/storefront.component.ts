import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
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
  readonly catalogue = inject(CatalogueService);
  readonly cart = inject(CartService);
  readonly query = signal('');
  readonly category = signal('All');
  readonly categories = ['All', 'Phones', 'Tablets', 'Smartwatches', 'Accessories', 'Chargers', 'Deals'];
  readonly products = computed(() => this.catalogue.products().filter(product => {
    const category = this.category();
    const matchesCategory = category === 'All' || (category === 'Deals' ? !!product.tag : product.category === category);
    return matchesCategory && product.name.toLowerCase().includes(this.query().trim().toLowerCase());
  }));

  ngOnInit(): void { void this.catalogue.load(); }
  add(product: Product): void { this.cart.add(product); }
  enquire(product: Product): void {
    const message = encodeURIComponent('Hi, is ' + product.name + ' (' + product.storage + ') available for R' + product.price.toLocaleString('en-ZA') + '?');
    window.open('https://wa.me/?text=' + message, '_blank', 'noopener,noreferrer');
  }
}
