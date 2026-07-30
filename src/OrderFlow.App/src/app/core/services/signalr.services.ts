import { Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject, Observable } from 'rxjs';
import { OrderProcessedEvent } from '../models/order-processed-event.model';
import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root',
})
export class SignalRService {
  private hubConnection!: signalR.HubConnection;
  private orderUpdatedSubject = new Subject<OrderProcessedEvent>();

  public isConnected = signal<boolean>(false);
  public orderUpdated$: Observable<OrderProcessedEvent> = this.orderUpdatedSubject.asObservable();

  public startConnection(): void {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(environment.hubUrl)
      .withAutomaticReconnect()
      .build();

    this.hubConnection
      .start()
      .then(() => {
        console.log('✅ Conectado exitosamente a SignalR Hub');
        this.isConnected.set(true);
      })
      .catch((err) => {
        console.error('❌ Error al conectar con SignalR:', err);
        this.isConnected.set(false);
      });

    this.hubConnection.onreconnecting(() => {
      this.isConnected.set(false);
    });

    this.hubConnection.onreconnected(() => {
      this.isConnected.set(true);
    });

    this.hubConnection.onclose(() => {
      this.isConnected.set(false);
    });

    // Escuchar el evento que emite la API en OrderProcessedConsumerService
    this.hubConnection.on('OrderUpdated', (event: OrderProcessedEvent) => {
      console.log('⚡ Evento en tiempo real recibido vía WebSocket:', event);
      this.orderUpdatedSubject.next(event);
    });
  }

  public stopConnection(): void {
    if (this.hubConnection) {
      this.hubConnection.stop();
      this.isConnected.set(false);
    }
  }
}
