import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { OrderForm } from './features/orders/components/order-form/order-form';
import { OrderList } from './features/orders/components/order-list/order-list';
import { Order } from './core/models/order.model';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, OrderForm, OrderList],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  public orders = signal<Order[]>([]);

  public onOrderCreated(newOrder: Order): void {
    this.orders.update(currentOrders => [newOrder, ...currentOrders]);
  }
}
