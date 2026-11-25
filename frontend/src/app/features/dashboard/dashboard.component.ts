import { Component, computed, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
@Component({selector:'sdh-dashboard',standalone:true,imports:[RouterLink],templateUrl:'./dashboard.component.html',styleUrl:'./dashboard.component.scss'})
export class DashboardComponent {
  readonly nav=['Overview','Orders','Inventory','Team','Security','Audit Log','Suppliers','Deliveries','Settings'];
  readonly filter = signal('');
  readonly moduleData: Record<string, {metric:string; total:string; columns:string[]; rows:string[][]}> = {
    'Orders': {metric:'Orders requiring attention',total:'8',columns:['Order','Customer','Amount','Status'],rows:[['#NM-5842','Demo Customer A','R5,999','Refund review'],['#NM-5843','Demo Customer B','R18,999','Preparing'],['#NM-5844','Demo Customer C','R6,499','Dispatched']]},
    'Inventory': {metric:'Devices requiring attention',total:'3',columns:['Device','IMEI / Reference','Available','Condition'],rows:[['Samsung A55','Demo IMEI hidden','8','Stock discrepancy'],['iPhone 16 Pro','Demo IMEI hidden','4','Available'],['Samsung S24 Ultra','Demo IMEI hidden','2','Low stock']]},
    'Team': {metric:'Pending staff approvals',total:'2',columns:['Employee','Role','Branch','Status'],rows:[['Thabo M.','Cashier','Demo Branch','Active'],['Zanele N.','Sales Rep','Demo Branch','Active'],['Byron D.','Stock Controller','Demo Branch','Invitation pending']]},
    'Security': {metric:'Open investigations',total:'4',columns:['Case','Event','Risk','Status'],rows:[['SEC-104','Unbalanced stock count','High','Investigate'],['SEC-105','Large manual discount','Medium','Approval pending'],['SEC-106','Refund review','Medium','Under review']]},
    'Audit Log': {metric:'Events in demo history',total:'4',columns:['Event','Actor','Time','Result'],rows:[['Stock received','Thabo M.','10:24','Recorded'],['Discount requested','Zanele N.','10:18','Pending'],['Sale created','Byron D.','09:57','Recorded'],['Price updated','Demo Admin','09:41','Recorded']]},
    'Suppliers': {metric:'Open purchase orders',total:'5',columns:['Purchase Order','Supplier','Amount','Status'],rows:[['PO-1001','Demo Supplier A','R25,000','Partially received'],['PO-1002','Demo Supplier B','R12,500','Awaiting delivery'],['PO-1003','Demo Supplier C','R11,000','Awaiting approval']]},
    'Deliveries': {metric:'Deliveries in progress',total:'3',columns:['Shipment','Method','Destination','Status'],rows:[['SHP-2001','The Courier Guy','Johannesburg','Dispatched'],['SHP-2002','PAXI','Durban','At collection point'],['SHP-2003','Store vehicle','Durban','Assigned']]},
    'Settings': {metric:'Pending configuration',total:'4',columns:['Configuration','Current state','Owner','Next step'],rows:[['Payment gateways','Not connected','Retail Owner','Merchant onboarding'],['Delivery providers','Demo only','Retail Owner','Provider credentials'],['Social login','Not connected','Platform Admin','OAuth setup'],['Tax and invoicing','Not configured','Retail Owner','Business verification']]}
  };
  readonly selectedModule = computed(() => this.moduleData[this.active()]);
  readonly selectedRows = computed(() => (this.selectedModule()?.rows ?? []).filter(row => row.some(cell => cell.toLowerCase().includes(this.filter().trim().toLowerCase()))));
  readonly active=signal('Overview');
  readonly notice=signal('');
  readonly activity=[
    {name:'Thabo M. updated stock',detail:'Samsung A55 (+2)',time:'10:24'},
    {name:'Zanele N. applied discount',detail:'R3,500 (approval requested)',time:'10:18'},
    {name:'Byron D. created sale',detail:'Order #NM-5842',time:'09:57'},
    {name:'Admin changed device price',detail:'iPhone 15 price updated',time:'09:41'}
  ];
  selectNavigation(name:string):void { this.active.set(name); this.filter.set(''); this.notice.set(''); }
  action(label:string):void {this.notice.set(label+' is a demonstration; server-side verification and approval are not implemented yet.');}
}
