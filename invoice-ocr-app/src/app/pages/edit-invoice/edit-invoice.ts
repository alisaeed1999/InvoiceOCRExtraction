import { CommonModule } from '@angular/common';
import { Component, EventEmitter, inject, Input, Output } from '@angular/core';
import { AbstractControl, FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatNativeDateModule } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

@Component({
  selector: 'app-edit-invoice',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './edit-invoice.html',
  styleUrl: './edit-invoice.scss'
})
export class EditInvoice {
  @Input() invoiceForm!: FormGroup;
  @Input() loading = false;
  @Output() submit = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();
  
  private fb = inject(FormBuilder);

  previewMode = false;

  get items(): FormArray {
    return this.invoiceForm.get('items') as FormArray;
  }

  togglePreviewMode(): void {
    this.previewMode = !this.previewMode;
    this.previewMode ? this.invoiceForm.disable() : this.invoiceForm.enable();
  }

  addItem(): void {
    this.items.push(this.createItem());
  }

  removeItem(index: number): void {
    if (this.items.length > 1) {
      this.items.removeAt(index);
    }
  }

  updateLineTotal(item: AbstractControl): void {
    const formGroup = item as FormGroup;
    const qty = formGroup.get('quantity')?.value || 0;
    const price = formGroup.get('unitPrice')?.value || 0;
    const total = qty * price;
    formGroup.get('lineTotal')?.setValue(total, { emitEvent: false });
    this.updateGrandTotal();
  }

  updateGrandTotal(): void {
    const items = this.items.controls;
    const subtotal = items.reduce((sum, item) => 
      sum + (item.get('lineTotal')?.value || 0), 0);
    
    const vat = this.invoiceForm.get('vat')?.value || 0;
    this.invoiceForm.get('totalAmount')?.setValue(subtotal + vat);
  }

  private createItem(): FormGroup {
    return this.fb.group({
      description: ['', Validators.required],
      quantity: [0, [Validators.required, Validators.min(0)]],
      unitPrice: [0, [Validators.required, Validators.min(0)]],
      lineTotal: [{ value: 0, disabled: true }]
    });
  }
}