import { Component, Input, OnInit, OnDestroy, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { SignalRService } from '../../../../core/services/signalr.services';
import { Order, OrderStatus } from '../../../../core/models/order.model';

@Component({
  selector: 'app-order-list',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './order-list.html',
  styleUrl: './order-list.css',
})
export class OrderList implements OnInit, OnDestroy {
  private signalRService = inject(SignalRService);
  private cd = inject(ChangeDetectorRef);
  private signalRSubscription!: Subscription;

  @Input() orders: Order[] = [];

  ngOnInit(): void {
    // 1. Conectar a SignalR WebSocket al inicializar el componente
    this.signalRService.startConnection();

    // 2. Suscribirse a las actualizaciones en tiempo real recibidas por RabbitMQ -> API -> SignalR
    this.signalRSubscription = this.signalRService.orderUpdated$.subscribe(event => {
      console.log('⚡ Actualización de pedido recibida vía WebSocket:', event);
      
      // Buscar la orden por GUID sin importar diferencias de mayúsculas/minúsculas
      const targetOrder = this.orders.find(o => o.id.toLowerCase() === event.orderId.toLowerCase());
      if (targetOrder) {
        targetOrder.estado = event.estado;
        // Forzar a Angular a refrescar el DOM de la tabla de inmediato
        this.cd.markForCheck();
      }
    });
  }

  public getStatusString(estado: any): 'Pending' | 'Confirmed' | 'Rejected' {
    const val = String(estado);
    if (val === '0' || val === 'Pending') {
      return 'Pending';
    }
    if (val === '1' || val === 'Confirmed') {
      return 'Confirmed';
    }
    if (val === '2' || val === 'Rejected') {
      return 'Rejected';
    }
    return 'Pending';
  }

  ngOnDestroy(): void {
    if (this.signalRSubscription) {
      this.signalRSubscription.unsubscribe();
    }
    this.signalRService.stopConnection();
  }
}
