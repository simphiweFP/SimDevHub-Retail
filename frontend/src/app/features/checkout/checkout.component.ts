import { Component, inject, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { CartService } from '../../core/services/cart.service';

type DeliveryMethod = 'courier' | 'paxi' | 'fleet' | 'collection';
type PaymentMethod = 'card' | 'eft' | 'bnpl';
@Component({
  selector: 'sdh-checkout',
  standalone: true,
  imports: [CurrencyPipe, RouterLink],
  templateUrl: './checkout.component.html',
  styleUrl: './checkout.component.scss'
})
export class CheckoutComponent {
  readonly cart = inject(CartService);
  readonly delivery = signal<DeliveryMethod>('courier');
  readonly payment = signal<PaymentMethod>('card');
  readonly notice = signal('');
  readonly deliveryMethods: { id: DeliveryMethod; name: string; detail: string }[] = [
    { id: 'courier', name: 'The Courier Guy', detail: 'Door-to-door delivery · requires live quotation' },
    { id: 'paxi', name: 'PEP / PAXI', detail: 'Collection point · subject to availability' },
    { id: 'fleet', name: 'Store vehicle', detail: 'Retailer-managed local delivery' },
    { id: 'collection', name: 'Collect in store', detail: 'Pickup confirmation required' }
  ];
  readonly paymentMethods: { id: PaymentMethod; name: string; detail: string }[] = [
    { id: 'card', name: 'Bank card', detail: 'Gateway connection pending' },
    { id: 'eft', name: 'Pay by bank', detail: 'Gateway connection pending' },
    { id: 'bnpl', name: 'Pay in instalments', detail: 'Payflex / PayJustNow — eligibility and integration pending' }
  ];
  selectDelivery(id: DeliveryMethod): void { this.delivery.set(id); this.notice.set(''); }
  selectPayment(id: PaymentMethod): void { this.payment.set(id); this.notice.set(''); }
  previewOrder(): void {
    this.notice.set('Demo preview only. No order has been created, no stock reserved, and no payment processed.');
  }
}
