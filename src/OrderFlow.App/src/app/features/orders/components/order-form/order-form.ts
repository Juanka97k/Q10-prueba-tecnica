import { Component, OnInit, Input, Output, EventEmitter, inject, signal } from '@angular/core';
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

  @Input() isSystemOnline: boolean = true;
  @Output() orderCreated = new EventEmitter<Order>();
  @Output() stockStatusChanged = new EventEmitter<boolean>();

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
        this.stockStatusChanged.emit(true);
        if (data.length > 0 && !this.orderForm.get('sku')?.value) {
          this.orderForm.patchValue({ sku: data[0].sku });
        }
      },
      error: (err) => {
        console.error('Error al cargar inventario de stock:', err);
        this.stocks.set([]);
        this.stockStatusChanged.emit(false);
      }
    });
  }

  public selectSku(sku: string): void {
    this.orderForm.patchValue({ sku });
  }

  public onSubmit(): void {
    if (this.orderForm.invalid || !this.isSystemOnline || this.stocks().length === 0) {
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
        
        // Reiniciar el formulario de forma limpia marcándolo como untouched para evitar falsas alertas rojas
        const currentSku = this.stocks().length > 0 ? this.stocks()[0].sku : '';
        this.orderForm.reset({
          clienteNombre: '',
          sku: currentSku,
          cantidad: 1
        });

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
