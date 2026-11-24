import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CurrencyPipe } from '@angular/common';
import { CatalogueService } from '../../core/services/catalogue.service';
import { Product } from '../../core/models/product';

@Component({
  selector:'sdh-storefront', standalone:true, imports:[RouterLink,CurrencyPipe],
  templateUrl:'./storefront.component.html', styleUrl:'./storefront.component.scss'
})
export class StorefrontComponent {
  private readonly catalogue=inject(CatalogueService);
  readonly query=signal('');
  readonly category=signal('Phones');
  readonly cart=signal<Product[]>([]);
  readonly categories=['Phones','Tablets','Smartwatches','Accessories','Chargers','Deals'];
  readonly products=computed(()=>this.catalogue.products.filter(x=>
    (this.category()==='Phones'||this.category()==='Deals'||x.category===this.category()) &&
    x.name.toLowerCase().includes(this.query().trim().toLowerCase())));
  readonly total=computed(()=>this.cart().reduce((sum,p)=>sum+p.price,0));
  add(product:Product):void { this.cart.update(items=>[...items,product]); }
  enquire(product:Product):void {
    const msg=encodeURIComponent('Hi, is '+product.name+' ('+product.storage+') available for R'+product.price.toLocaleString('en-ZA')+'?');
    window.open('https://wa.me/?text='+msg,'_blank','noopener,noreferrer');
  }
}
