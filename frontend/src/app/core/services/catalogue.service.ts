import { Injectable } from '@angular/core';
import { Product } from '../models/product';
@Injectable({providedIn:'root'})
export class CatalogueService {
  // Demo products only; production product data must be tenant-scoped and API-backed.
  readonly products: Product[] = [
    {id:'ip16',name:'iPhone 16 Pro',storage:'256GB',price:26999,category:'Phones',tag:'New'},
    {id:'s24',name:'Samsung Galaxy S24 Ultra',storage:'256GB',price:23999,category:'Phones',tag:'Hot Deal'},
    {id:'a55',name:'Samsung Galaxy A55',storage:'128GB',price:6499,category:'Phones',tag:'Popular'},
    {id:'ip15',name:'iPhone 15',storage:'128GB',price:18999,category:'Phones'}
  ];
}
