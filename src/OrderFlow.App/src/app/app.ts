import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { OrderForm } from './features/orders/components/order-form/order-form';
import { OrderList } from './features/orders/components/order-list/order-list';
import { Order } from './core/models/order.model';
import { SignalRService } from './core/services/signalr.services';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, OrderForm, OrderList],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit {
  private signalRService = inject(SignalRService);

  public orders = signal<Order[]>([]);
  public isApiOnline = signal<boolean>(true);

  public isSystemOnline = computed(() => this.isApiOnline() && this.signalRService.isConnected());

  ngOnInit(): void {
    // Iniciar conexión SignalR
    this.signalRService.startConnection();
  }

  public onOrderCreated(newOrder: Order): void {
    this.orders.update(currentOrders => [newOrder, ...currentOrders]);
  }

  public onStockStatusChanged(onlineStatus: boolean): void {
    this.isApiOnline.set(onlineStatus);
  }
}
