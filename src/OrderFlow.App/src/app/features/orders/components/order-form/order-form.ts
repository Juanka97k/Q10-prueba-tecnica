import { Component, OnInit, Output, EventEmitter, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { OrderService } from '../../../../core/services/order.services';
import { Order } from '../../../../core/models/order.model';
import { CreateOrderRequest } from '../../../../core/models/create-order-request.model';
import { Stock } from '../../../../core/models/stock.model';

@Component({
  selector: 'app-order-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './order-form.html',
  styleUrl: './order-form.css',
})
export class OrderForm implements OnInit {
  private fb = inject(FormBuilder);
  private orderService = inject(OrderService);

  @Output() orderCreated = new EventEmitter<Order>();

  public isSubmitting = signal<boolean>(false);
  public errorMessage = signal<string | null>(null);
  public successMessage = signal<string | null>(null);

  public stocks = signal<Stock[]>([]);

  public orderForm: FormGroup = this.fb.group({
    clienteNombre: ['', [Validators.required, Validators.maxLength(100)]],
    sku: ['', [Validators.required]],
    cantidad: [1, [Validators.required, Validators.min(1), Validators.max(100)]],
  });

  ngOnInit(): void {
    this.loadStocks();
  }

  public loadStocks(): void {
    this.orderService.getStocks().subscribe({
      next: (data) => {
        this.stocks.set(data);
        if (data.length > 0 && !this.orderForm.get('sku')?.value) {
          this.orderForm.patchValue({ sku: data[0].sku });
        }
      },
      error: (err) => {
        console.error('Error al cargar inventario de stock:', err);
      }
    });
  }

  public selectSku(sku: string): void {
    this.orderForm.patchValue({ sku });
  }

  public onSubmit(): void {
    if (this.orderForm.invalid) {
      this.orderForm.markAllAsTouched();
      return;
    }

    this.isSubmitting.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    const request: CreateOrderRequest = this.orderForm.value;

    this.orderService.createOrder(request).subscribe({
      next: (newOrder) => {
        this.isSubmitting.set(false);
        this.successMessage.set(`Pedido ${newOrder.id.substring(0, 8)}... registrado exitosamente.`);
        this.orderCreated.emit(newOrder);
        this.orderForm.patchValue({ clienteNombre: '' });
        // Recargar el stock dinámico tras hacer el pedido
        this.loadStocks();
      },
      error: (err) => {
        this.isSubmitting.set(false);
        if (err.error && err.error.detail) {
          this.errorMessage.set(err.error.detail);
        } else if (err.error && err.error.errors) {
          const firstKey = Object.keys(err.error.errors)[0];
          this.errorMessage.set(err.error.errors[firstKey][0]);
        } else {
          this.errorMessage.set('Error procesando la solicitud en el servidor.');
        }
      },
    });
  }
}
