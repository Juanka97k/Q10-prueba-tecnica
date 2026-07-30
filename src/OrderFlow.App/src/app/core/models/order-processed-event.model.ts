export interface OrderProcessedEvent {
  orderId: string;
  estado: 'Confirmed' | 'Rejected';
  procesadoEn: string;
}
