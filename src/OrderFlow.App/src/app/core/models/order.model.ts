export enum OrderStatusEnum {
  Pending = 0,
  Confirmed = 1,
  Rejected = 2
}

export type OrderStatus = 'Pending' | 'Confirmed' | 'Rejected' | OrderStatusEnum | number;

export interface Order {
  id: string;
  clienteNombre: string;
  sku: string;
  cantidad: number;
  estado: OrderStatus;
  creadoEn: string;
}
