import { Component, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
@Component({selector:'sdh-dashboard',standalone:true,imports:[RouterLink],templateUrl:'./dashboard.component.html',styleUrl:'./dashboard.component.scss'})
export class DashboardComponent {
  readonly nav=['Overview','Orders','Inventory','Team','Security','Audit Log','Suppliers','Deliveries','Settings'];
  readonly active=signal('Overview');
  readonly notice=signal('');
  readonly activity=[
    {name:'Thabo M. updated stock',detail:'Samsung A55 (+2)',time:'10:24'},
    {name:'Zanele N. applied discount',detail:'R3,500 (approval requested)',time:'10:18'},
    {name:'Byron D. created sale',detail:'Order #NM-5842',time:'09:57'},
    {name:'Admin changed device price',detail:'iPhone 15 price updated',time:'09:41'}
  ];
  selectNavigation(name:string):void { this.active.set(name); if(name!=='Overview') this.action(name); }
  action(label:string):void {this.notice.set(label+' is a demonstration; server-side verification and approval are not implemented yet.');}
}
